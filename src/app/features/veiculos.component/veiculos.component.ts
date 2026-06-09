import {
  Component, OnInit, OnDestroy, inject, signal, computed
} from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  ReactiveFormsModule, FormBuilder, FormGroup,
  Validators, AbstractControl, ValidationErrors
} from '@angular/forms';
import { Subject, forkJoin, of, throwError, debounceTime, distinctUntilChanged, takeUntil, switchMap, catchError } from 'rxjs';
import { VeiculosService, Veiculo, PagedResult, VeiculoAnexo } from '../../core/services/veiculos.service';
import { PdfService, PdfField } from '../../core/services/pdf.service';
import { ClientesCatalogoService, ClienteModel } from '../../core/services/clientes-catalogo.service';

// ── Regex ────────────────────────────────────────────────────────────────────
const MATRICULA_REGEX = /^([A-Z]{2}-\d{2}-[A-Z]{2}|\d{2}-[A-Z]{2}-\d{2}|\d{2}-\d{2}-[A-Z]{2})$/;

// ── Interfaces para ficheiros pendentes ─────────────────────────────────────
export interface PendingFile {
  file: File;
  id: string;
  previewUrl?: string;
  erro?: string;
}

/**
 * VIN Validator — ISO 3779 / FMVSS 115
 */
function vinValidator(control: AbstractControl): ValidationErrors | null {
  const value: string = (control.value ?? '').toString().toUpperCase().trim();
  if (!value) return null;

  if (value.length !== 17) {
    return { vinLength: { actual: value.length, expected: 17 } };
  }
  if (/[IOQ]/.test(value)) {
    return { vinForbiddenChars: true };
  }
  if (!/^[A-HJ-NPR-Z0-9]{17}$/.test(value)) {
    return { vinInvalidChars: true };
  }

  const TRANSLITERATION: Record<string, number> = {
    A:1,B:2,C:3,D:4,E:5,F:6,G:7,H:8,
    J:1,K:2,L:3,M:4,N:5,P:7,R:9,
    S:2,T:3,U:4,V:5,W:6,X:7,Y:8,Z:9,
    '0':0,'1':1,'2':2,'3':3,'4':4,'5':5,'6':6,'7':7,'8':8,'9':9
  };
  const WEIGHTS = [8,7,6,5,4,3,2,10,0,9,8,7,6,5,4,3,2];

  let sum = 0;
  for (let i = 0; i < 17; i++) {
    const ch = value[i];
    const val = TRANSLITERATION[ch];
    if (val === undefined) return { vinInvalidChars: true };
    sum += val * WEIGHTS[i];
  }

  const remainder = sum % 11;
  const checkDigit = remainder === 10 ? 'X' : String(remainder);
  if (value[8] !== checkDigit) {
    return { vinCheckDigit: { expected: checkDigit, actual: value[8] } };
  }
  return null;
}

type ViewState = 'list' | 'create' | 'edit' | 'details';

@Component({
  selector: 'app-veiculos',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './veiculos.component.html',
  styleUrls: ['./veiculos.component.css']
})
export class VeiculosComponent implements OnInit, OnDestroy {
  private svc    = inject(VeiculosService);
  private fb     = inject(FormBuilder);
  private pdfSvc = inject(PdfService);
  // ADICIONADO: serviço de clientes para pesquisa de proprietário
  private clientesSvc = inject(ClientesCatalogoService);
  private destroy$ = new Subject<void>();

  // ── View state ────────────────────────────────────────────────────────────
  currentState   = signal<ViewState>('list');
  editingId      = signal<number | null>(null);
  isEditing      = computed(() => this.currentState() === 'edit');
  isViewing      = computed(() => this.currentState() === 'details');

  selectedVeiculo = computed(() =>
    this.veiculos().find(v => v.id === this.editingId()) ?? null
  );

  // ── Data ──────────────────────────────────────────────────────────────────
  pagedResult  = signal<PagedResult<Veiculo> | null>(null);
  veiculos     = computed(() => this.pagedResult()?.items ?? []);
  isLoading    = signal(false);
  isSaving     = signal(false);
  errorMsg     = signal<string | null>(null);
  successMsg   = signal<string | null>(null);

  // ── List controls ─────────────────────────────────────────────────────────
  filtroSearch    = '';
  mostrarInativos = false;
  currentPage     = 1;
  readonly pageSize = 15;
  private searchInput$ = new Subject<string>();

  // ── Forms ─────────────────────────────────────────────────────────────────
  form!: FormGroup;
  readonly currentYear       = new Date().getFullYear();
  readonly combustivelOpcoes = ['Gasolina','Diesel','Híbrido','Eléctrico','GPL','Hidrogénio'];

  // ── Delete confirm ────────────────────────────────────────────────────────
  showDeleteConfirm   = signal(false);
  veiculoParaDelete   = signal<Veiculo | null>(null);

  // ── Matricula change modals ───────────────────────────────────────────────
  showMatriculaConfirmModal = signal(false);
  showMatriculaFormModal    = signal(false);
  matriculaFile             = signal<File | null>(null);
  matriculaFileError        = signal<string | null>(null);
  isSavingMatricula         = signal(false);
  originalMatricula         = signal<string>('');

  // ── Anexos ────────────────────────────────────────────────────────────────
  anexosExistentes    = signal<VeiculoAnexo[]>([]);
  removingAnexoId     = signal<number | null>(null);
  isDragOver          = signal(false);
  pendingFiles        = signal<PendingFile[]>([]);

  private readonly MAX_FILE_SIZE = 10 * 1024 * 1024;
  private readonly ALLOWED_TYPES = ['application/pdf', 'image/jpeg', 'image/jpg', 'image/png', 'image/webp'];
  private readonly ALLOWED_EXT = ['.pdf', '.jpg', '.jpeg', '.png', '.webp'];

  // ── NOVO: Pesquisa de proprietário ────────────────────────────────────────
  clientes = signal<ClienteModel[]>([]);
  proprietarioSearchTerm = signal('');
  showProprietarioDropdown = signal(false);

  filteredProprietarios = computed(() => {
    const term = this.proprietarioSearchTerm().trim().toLowerCase();
    if (term.length < 2) return [] as ClienteModel[];
    return this.clientes()
      .filter(c =>
        c.nome.toLowerCase().includes(term) ||
        (c.contribuinte ?? '').toLowerCase().includes(term) ||
        (c.telefone ?? '').toLowerCase().includes(term) ||
        c.id.toString().includes(term)
      ).slice(0, 10);
  });

  getProprietarioSelecionado(): ClienteModel | null {
    const id = this.form.get('proprietarioId')?.value;
    if (!id) return null;
    return this.clientes().find(c => c.id === id) ?? null;
  }

  onProprietarioSearch(value: string): void {
    this.proprietarioSearchTerm.set(value || '');
    this.showProprietarioDropdown.set(
      this.proprietarioSearchTerm().trim().length >= 2 &&
      this.filteredProprietarios().length > 0
    );
    if (!value?.trim()) {
      this.form.patchValue({ proprietarioId: null });
    }
  }

  onProprietarioFocus(): void {
    this.showProprietarioDropdown.set(this.filteredProprietarios().length > 0);
  }

  closeProprietarioDropdown(): void {
    setTimeout(() => this.showProprietarioDropdown.set(false), 150);
  }

  selecionarProprietario(c: ClienteModel): void {
    this.form.patchValue({ proprietarioId: c.id });
    this.proprietarioSearchTerm.set('');
    this.showProprietarioDropdown.set(false);
  }

  limparProprietario(): void {
    this.form.patchValue({ proprietarioId: null });
    this.proprietarioSearchTerm.set('');
    this.showProprietarioDropdown.set(false);
  }

  // ── Computed helpers ──────────────────────────────────────────────────────
  get totalVeiculos()   { return this.pagedResult()?.total ?? 0; }
  get totalPages()      { return this.pagedResult()?.totalPages ?? 0; }
  get pages(): number[] { return Array.from({ length: this.totalPages }, (_, i) => i + 1); }
  get totalAtivos()     { return this.veiculos().filter(v => v.ativo).length; }
  get totalInativos()   { return this.veiculos().filter(v => !v.ativo).length; }
  get veiculosPorCombustivel() {
    return new Set(this.veiculos().map(v => v.tipoCombustivel).filter(Boolean)).size;
  }

  // ── Lifecycle ─────────────────────────────────────────────────────────────
  ngOnInit(): void {
    this.initForm();
    this.setupSearchDebounce();
    this.carregarVeiculos();
    this.carregarClientes();
  }

  ngOnDestroy(): void {
    this.pendingFiles().forEach(pf => {
      if (pf.previewUrl) URL.revokeObjectURL(pf.previewUrl);
    });
    this.destroy$.next();
    this.destroy$.complete();
  }

  // ── Carregar clientes/proprietários ──────────────────────────────────────
  private carregarClientes(): void {
    this.clientesSvc.listar({ ativo: true, pageSize: 500 }).subscribe({
      next: r => this.clientes.set(r?.items ?? []),
      error: () => this.clientes.set([])
    });
  }

  // ── Form init ─────────────────────────────────────────────────────────────
  private initForm(): void {
    this.form = this.fb.group({
      matricula:       ['', [Validators.required, Validators.maxLength(20), Validators.pattern(MATRICULA_REGEX)]],
      marca:           ['', [Validators.required, Validators.maxLength(100)]],
      modelo:          ['', [Validators.required, Validators.maxLength(100)]],
      cor:             ['', Validators.maxLength(50)],
      ano:             [null, [Validators.min(1900), Validators.max(this.currentYear + 1)]],
      vin:             ['', [Validators.required, Validators.maxLength(50), vinValidator]],
      tipoCombustivel: [''],
      cilindrada:      [null, [Validators.min(0), Validators.max(99999)]],
      potencia:        [null, [Validators.min(0), Validators.max(9999)]],
      lugares:         [null, [Validators.min(1), Validators.max(200)]],
      peso:            [null, [Validators.min(0)]],
      observacoes:     [''],
      proprietarioId:  [null],
      ativo:           [true],
      criadoEm:        [{ value: '', disabled: true }],
      novaMatricula:   ['', []],
      motivoTroca:     ['', []],
    });
  }

  private setupSearchDebounce(): void {
    this.searchInput$
      .pipe(debounceTime(350), distinctUntilChanged(), takeUntil(this.destroy$))
      .subscribe(() => { this.currentPage = 1; this.carregarVeiculos(); });
  }

  private populateForm(veiculo: Veiculo): void {
    this.form.enable();
    const isView = this.isViewing();

    this.originalMatricula.set(veiculo.matricula);
    this.anexosExistentes.set(veiculo.anexos ?? []);

    this.form.patchValue({
      matricula:       veiculo.matricula,
      marca:           veiculo.marca,
      modelo:          veiculo.modelo,
      cor:             veiculo.cor ?? '',
      ano:             veiculo.ano ?? null,
      vin:             veiculo.vin ?? '',
      tipoCombustivel: veiculo.tipoCombustivel ?? '',
      cilindrada:      veiculo.cilindrada ?? null,
      potencia:        veiculo.potencia ?? null,
      lugares:         veiculo.lugares ?? null,
      peso:            veiculo.peso ?? null,
      observacoes:     veiculo.observacoes ?? '',
      proprietarioId:  veiculo.proprietarioId ?? null,
      ativo:           veiculo.ativo,
      criadoEm:        veiculo.criadoEm ? new Date(veiculo.criadoEm).toLocaleString() : '',
      novaMatricula:   '',
      motivoTroca:     '',
    });

    if (isView) {
      this.form.disable();
    } else if (this.isEditing()) {
      this.form.get('matricula')?.disable();
    } else {
      this.form.get('matricula')?.enable();
    }

    this.form.get('novaMatricula')?.disable();
    this.form.get('motivoTroca')?.disable();
  }

  // ── CRUD helpers ──────────────────────────────────────────────────────────
  carregarVeiculos(): void {
    this.isLoading.set(true);
    this.svc.listar({
      search:   this.filtroSearch || undefined,
      ativo:    this.mostrarInativos ? undefined : true,
      page:     this.currentPage,
      pageSize: this.pageSize,
    }).subscribe({
      next:  res  => { this.pagedResult.set(res); this.isLoading.set(false); },
      error: err  => { this.errorMsg.set(err.message); this.isLoading.set(false); }
    });
  }

  onSearchChange(value: string): void { this.filtroSearch = value; this.searchInput$.next(value); }
  toggleInativos(): void { this.mostrarInativos = !this.mostrarInativos; this.currentPage = 1; this.carregarVeiculos(); }
  goToPage(page: number): void { if (page < 1 || page > this.totalPages) return; this.currentPage = page; this.carregarVeiculos(); }

  goToCreate(): void {
    this.currentState.set('create');
    this.editingId.set(null);
    this.originalMatricula.set('');
    this.form.reset({ ativo: true, criadoEm: '' });
    this.form.enable();
    this.form.get('matricula')?.enable();
    this.form.get('novaMatricula')?.disable();
    this.form.get('motivoTroca')?.disable();
    this.showMatriculaConfirmModal.set(false);
    this.showMatriculaFormModal.set(false);
    this.pendingFiles.set([]);
    this.anexosExistentes.set([]);
    this.proprietarioSearchTerm.set('');
    this.showProprietarioDropdown.set(false);
    this.errorMsg.set(null);
  }

  goToEdit(v: Veiculo): void {
    this.loadVeiculo(v.id, 'edit', v.matricula);
  }

  goToDetails(v: Veiculo, event?: Event): void {
    if (event) event.stopPropagation();
    this.loadVeiculo(v.id, 'details', v.matricula);
  }

  private loadVeiculo(id: number, state: ViewState, currentMatricula: string): void {
    this.currentState.set(state);
    this.editingId.set(id);
    this.originalMatricula.set(currentMatricula);
    this.showMatriculaConfirmModal.set(false);
    this.showMatriculaFormModal.set(false);
    this.matriculaFile.set(null);
    this.matriculaFileError.set(null);
    this.pendingFiles.set([]);
    this.anexosExistentes.set([]);
    this.proprietarioSearchTerm.set('');
    this.showProprietarioDropdown.set(false);
    this.errorMsg.set(null);

    this.isLoading.set(true);
    this.svc.obter(id).subscribe({
      next: veiculo => {
        this.populateForm(veiculo);
        this.isLoading.set(false);
      },
      error: err => {
        this.errorMsg.set(err.error?.message || err.message || 'Erro ao carregar veículo.');
        this.isLoading.set(false);
        this.cancel();
      }
    });
  }

  cancel(): void {
    this.currentState.set('list');
    this.editingId.set(null);
    this.originalMatricula.set('');
    this.form.reset();
    this.showMatriculaConfirmModal.set(false);
    this.showMatriculaFormModal.set(false);
    this.matriculaFile.set(null);
    this.matriculaFileError.set(null);
    this.pendingFiles.set([]);
    this.anexosExistentes.set([]);
    this.proprietarioSearchTerm.set('');
    this.showProprietarioDropdown.set(false);
    this.errorMsg.set(null);
    this.carregarVeiculos();
  }

  salvarVeiculo(): void {
    const matriculaCtrl = this.form.get('matricula');
    const wasDisabled = matriculaCtrl?.disabled;
    if (wasDisabled) matriculaCtrl?.enable();

    this.form.markAllAsTouched();

    const novaMatriculaCtrl = this.form.get('novaMatricula')!;
    const motivoTrocaCtrl   = this.form.get('motivoTroca')!;
    novaMatriculaCtrl.clearValidators();
    novaMatriculaCtrl.updateValueAndValidity();
    motivoTrocaCtrl.clearValidators();
    motivoTrocaCtrl.updateValueAndValidity();

    if (this.form.invalid) {
      if (wasDisabled) matriculaCtrl?.disable();
      this.errorMsg.set('Corrija os erros no formulário.');
      return;
    }

    this.isSaving.set(true);
    const raw = this.form.getRawValue();

    const dto: Partial<Veiculo> = {
      matricula:       raw.matricula?.trim().toUpperCase(),
      marca:           raw.marca?.trim(),
      modelo:          raw.modelo?.trim(),
      cor:             raw.cor?.trim() || undefined,
      ano:             raw.ano || undefined,
      vin:             raw.vin?.trim().toUpperCase() || undefined,
      tipoCombustivel: raw.tipoCombustivel || undefined,
      cilindrada:      raw.cilindrada || undefined,
      potencia:        raw.potencia || undefined,
      lugares:         raw.lugares || undefined,
      peso:            raw.peso || undefined,
      observacoes:     raw.observacoes?.trim() || undefined,
      proprietarioId:  raw.proprietarioId || undefined,
      ativo:           raw.ativo,
    };

    const req$ = this.isEditing() && this.editingId()
      ? this.svc.atualizar(this.editingId()!, dto as Veiculo)
      : this.svc.criar(dto as Veiculo);

    req$.pipe(
      switchMap(veiculo => {
        const uploads = this.pendingFiles().map(pf => this.svc.uploadAnexo(veiculo.id, pf.file));
        if (uploads.length === 0) return of(veiculo);
        return forkJoin(uploads).pipe(switchMap(() => of(veiculo)));
      }),
      catchError(err => throwError(() => err))
    ).subscribe({
      next: () => {
        this.isSaving.set(false);
        this.pendingFiles.set([]);
        this.cancel();
        this.showToast(this.isEditing() ? 'Veículo atualizado com sucesso' : 'Veículo criado com sucesso');
      },
      error: err => {
        this.errorMsg.set(err.message);
        this.isSaving.set(false);
        if (wasDisabled) matriculaCtrl?.disable();
      }
    });
  }

  // ── Delete ────────────────────────────────────────────────────────────────
  desativarVeiculo(v: Veiculo): void { this.veiculoParaDelete.set(v); this.showDeleteConfirm.set(true); }
  cancelarDelete(): void { this.showDeleteConfirm.set(false); this.veiculoParaDelete.set(null); }
  executarDesativar(): void {
    const v = this.veiculoParaDelete();
    if (!v) return;
    this.svc.deletar(v.id).subscribe({
      next:  () => { this.cancelarDelete(); this.carregarVeiculos(); this.showToast('Veículo desactivado com sucesso'); },
      error: err => this.errorMsg.set(err.message)
    });
  }

  ativarVeiculo(v: Veiculo): void {
    if (!confirm(`Activar o veículo ${v.matricula}?`)) return;
    this.svc.ativar(v.id).subscribe({
      next:  () => { this.carregarVeiculos(); this.showToast('Veículo activado com sucesso'); },
      error: err => this.errorMsg.set(err.message)
    });
  }

  // ── Matricula change flow ─────────────────────────────────────────────────
  onMatriculaFieldClick(): void {
    if (!this.isEditing()) return;
    this.showMatriculaConfirmModal.set(true);
  }

  cancelarTrocaMatricula(): void {
    this.showMatriculaConfirmModal.set(false);
    this.showMatriculaFormModal.set(false);
    this.matriculaFile.set(null);
    this.matriculaFileError.set(null);
    const novaCtrl = this.form.get('novaMatricula')!;
    const motivoCtrl = this.form.get('motivoTroca')!;
    novaCtrl.reset(''); novaCtrl.clearValidators(); novaCtrl.disable();
    motivoCtrl.reset(''); motivoCtrl.clearValidators(); motivoCtrl.disable();
  }

  confirmarTrocaMatricula(): void {
    this.showMatriculaConfirmModal.set(false);
    this.showMatriculaFormModal.set(true);
    const novaCtrl = this.form.get('novaMatricula')!;
    const motivoCtrl = this.form.get('motivoTroca')!;
    novaCtrl.setValue('');
    motivoCtrl.setValue('');
    novaCtrl.enable();
    novaCtrl.setValidators([Validators.required, Validators.pattern(MATRICULA_REGEX)]);
    novaCtrl.updateValueAndValidity();
    motivoCtrl.enable();
    motivoCtrl.setValidators([Validators.required, Validators.minLength(10)]);
    motivoCtrl.updateValueAndValidity();
    this.matriculaFileError.set(null);
  }

  cancelarModoTrocaMatricula(): void {
    this.showMatriculaFormModal.set(false);
    this.matriculaFile.set(null);
    this.matriculaFileError.set(null);
    const novaCtrl = this.form.get('novaMatricula')!;
    const motivoCtrl = this.form.get('motivoTroca')!;
    novaCtrl.reset(''); novaCtrl.clearValidators(); novaCtrl.disable();
    motivoCtrl.reset(''); motivoCtrl.clearValidators(); motivoCtrl.disable();
  }

  onNovaMatriculaInput(event: Event): void {
    const input = event.target as HTMLInputElement;
    const value = input.value.toUpperCase();
    input.value = value;
    this.form.get('novaMatricula')?.setValue(value, { emitEvent: false });
  }

  onMatriculaDocChange(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file  = input.files?.[0] ?? null;
    this.matriculaFileError.set(null);
    if (!file) { this.matriculaFile.set(null); return; }
    if (!this.ALLOWED_TYPES.includes(file.type)) {
      this.matriculaFileError.set('Apenas PDF, JPEG, PNG ou WebP são aceites.');
      input.value = '';
      this.matriculaFile.set(null);
      return;
    }
    if (file.size > this.MAX_FILE_SIZE) {
      this.matriculaFileError.set('O ficheiro não pode exceder 10 MB.');
      input.value = '';
      this.matriculaFile.set(null);
      return;
    }
    this.matriculaFile.set(file);
  }

  guardarTrocaMatricula(): void {
    const novaCtrl   = this.form.get('novaMatricula')!;
    const motivoCtrl = this.form.get('motivoTroca')!;
    let novaMatriculaValue = (novaCtrl.value || '').trim().toUpperCase();
    const motivoValue = (motivoCtrl.value || '').trim();

    if (!novaMatriculaValue) { this.errorMsg.set('A nova matrícula é obrigatória.'); return; }
    if (!MATRICULA_REGEX.test(novaMatriculaValue)) { this.errorMsg.set('Formato inválido. Use: AA-00-AA, 00-AA-00 ou 00-00-AA.'); return; }
    if (novaMatriculaValue === this.originalMatricula()) { this.errorMsg.set('A nova matrícula deve ser diferente da atual.'); return; }
    if (!motivoValue || motivoValue.length < 10) { this.errorMsg.set('O motivo da troca deve ter pelo menos 10 caracteres.'); return; }
    if (!this.matriculaFile()) { this.matriculaFileError.set('O documento comprovativo é obrigatório.'); return; }

    const id = this.editingId();
    if (!id) return;

    this.isSavingMatricula.set(true);
    this.errorMsg.set(null);

    this.svc.alterarMatricula(id, novaMatriculaValue, motivoValue, this.matriculaFile()!).subscribe({
      next: (response) => {
        this.isSavingMatricula.set(false);
        this.cancelarModoTrocaMatricula();
        this.form.get('matricula')?.setValue(novaMatriculaValue);
        this.originalMatricula.set(novaMatriculaValue);
        this.carregarVeiculos();
        this.showToast(response.message || 'Matrícula alterada com sucesso');
      },
      error: err => {
        this.errorMsg.set(err.message ?? 'Erro ao alterar matrícula.');
        this.isSavingMatricula.set(false);
      }
    });
  }

  // ── Anexos ────────────────────────────────────────────────────────────────
  onDragOver(event: DragEvent): void {
    event.preventDefault(); event.stopPropagation(); this.isDragOver.set(true);
  }
  onDragLeave(event: DragEvent): void {
    event.preventDefault(); event.stopPropagation(); this.isDragOver.set(false);
  }
  onFileDrop(event: DragEvent): void {
    event.preventDefault(); event.stopPropagation(); this.isDragOver.set(false);
    const files = event.dataTransfer?.files;
    if (files) this.processarFicheiros(Array.from(files));
  }
  onFileSelect(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (input.files) { this.processarFicheiros(Array.from(input.files)); input.value = ''; }
  }

  private processarFicheiros(files: File[]): void {
    const novos: PendingFile[] = [];
    for (const file of files) {
      const ext = '.' + file.name.split('.').pop()?.toLowerCase();
      if (file.size > this.MAX_FILE_SIZE) { this.errorMsg.set(`"${file.name}" excede o limite de 10 MB.`); continue; }
      if (!this.ALLOWED_TYPES.includes(file.type) && !this.ALLOWED_EXT.includes(ext)) {
        this.errorMsg.set(`"${file.name}" — tipo não suportado. Use PDF, JPG, PNG ou WebP.`); continue;
      }
      const pending: PendingFile = { file, id: crypto.randomUUID() };
      if (file.type.startsWith('image/')) pending.previewUrl = URL.createObjectURL(file);
      novos.push(pending);
    }
    if (novos.length > 0) { this.pendingFiles.update(prev => [...prev, ...novos]); this.errorMsg.set(null); }
  }

  removerFicheiroPendente(id: string): void {
    const file = this.pendingFiles().find(f => f.id === id);
    if (file?.previewUrl) URL.revokeObjectURL(file.previewUrl);
    this.pendingFiles.update(prev => prev.filter(f => f.id !== id));
  }

  removerAnexoExistente(anexo: VeiculoAnexo): void {
    const veiculoId = this.editingId();
    if (!veiculoId) return;
    this.removingAnexoId.set(anexo.id);
    this.svc.removerAnexo(veiculoId, anexo.id).subscribe({
      next: () => {
        this.anexosExistentes.update(prev => prev.filter(a => a.id !== anexo.id));
        this.removingAnexoId.set(null);
        this.showToast('Anexo removido com sucesso');
      },
      error: err => { this.errorMsg.set(err.error?.message || 'Erro ao remover anexo.'); this.removingAnexoId.set(null); }
    });
  }

  downloadAnexo(anexo: VeiculoAnexo): void {
    const veiculoId = this.editingId();
    if (!veiculoId) return;
    this.svc.downloadAnexo(veiculoId, anexo.id).subscribe({
      next: blob => {
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url; a.download = anexo.nomeOriginal; a.click();
        URL.revokeObjectURL(url);
      },
      error: () => this.errorMsg.set('Erro ao descarregar o ficheiro.')
    });
  }

  getFileIcon(mimeType: string): string {
    if (mimeType === 'application/pdf') return 'la-file-pdf';
    if (mimeType.startsWith('image/')) return 'la-file-image';
    return 'la-file-alt';
  }

  formatarTamanho(bytes: number): string {
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  }

  formatarData(data: string): string {
    if (!data) return '—';
    return new Date(data).toLocaleDateString('pt-PT');
  }

  // ── PDF ───────────────────────────────────────────────────────────────────
  imprimirPdf(veiculo: Veiculo, event?: Event): void {
    if (event) event.stopPropagation();
    const fields: PdfField[] = [
      { label: 'Matrícula',   value: veiculo.matricula },
      { label: 'Marca',       value: veiculo.marca },
      { label: 'Modelo',      value: veiculo.modelo },
      { label: 'Cor',         value: veiculo.cor || '—' },
      { label: 'Ano',         value: veiculo.ano || '—' },
      { label: 'VIN',         value: veiculo.vin || '—' },
      { label: 'Combustível', value: veiculo.tipoCombustivel || '—' },
      { label: 'Cilindrada',  value: veiculo.cilindrada ?? '—' },
      { label: 'Potência',    value: veiculo.potencia ?? '—' },
      { label: 'Lugares',     value: veiculo.lugares ?? '—' },
      { label: 'Peso',        value: veiculo.peso ?? '—' },
      { label: 'Ativo',       value: veiculo.ativo ? 'Sim' : 'Não' },
    ];
    const blob = this.pdfSvc.generateEntityPdf(`Veículo ${veiculo.matricula}`, fields);
    this.pdfSvc.downloadPdf(blob, `Veiculo_${veiculo.matricula}.pdf`);
  }

  // ── UI helpers ────────────────────────────────────────────────────────────
  showToast(msg: string): void { this.successMsg.set(msg); setTimeout(() => this.successMsg.set(null), 3500); }
  clearError(): void { this.errorMsg.set(null); }

  hasError(name: string, error?: string): boolean {
    const c = this.form.get(name);
    if (!c || !c.invalid || !c.touched) return false;
    return error ? c.hasError(error) : true;
  }

  getMatriculaErrorMsg(): string {
    const c = this.form.get('matricula');
    if (c?.hasError('required')) return 'Matrícula é obrigatória.';
    if (c?.hasError('pattern'))  return 'Formato inválido. Use: AA-00-AA, 00-AA-00 ou 00-00-AA.';
    return 'Matrícula inválida.';
  }

  getNovaMatriculaErrorMsg(): string {
    const c = this.form.get('novaMatricula');
    if (c?.hasError('required')) return 'Nova matrícula é obrigatória.';
    if (c?.hasError('pattern'))  return 'Formato inválido. Use: AA-00-AA, 00-AA-00 ou 00-00-AA.';
    return 'Matrícula inválida.';
  }

  getVinErrorMsg(): string {
    const c = this.form.get('vin');
    if (!c) return '';
    if (c.hasError('required'))          return 'VIN/Chassis é obrigatório.';
    if (c.hasError('vinLength'))         return `O VIN deve ter exactamente 17 caracteres (atual: ${c.errors?.['vinLength']?.actual}).`;
    if (c.hasError('vinForbiddenChars')) return 'O VIN não pode conter as letras I, O ou Q.';
    if (c.hasError('vinInvalidChars'))   return 'O VIN contém caracteres inválidos. Use apenas letras e números (exceto I, O, Q).';
    if (c.hasError('vinCheckDigit'))     return `Dígito de controlo inválido (posição 9). Esperado: "${c.errors?.['vinCheckDigit']?.expected}".`;
    return 'VIN inválido.';
  }

  get motivoTrocaErrorMsg(): string {
    const c = this.form.get('motivoTroca');
    if (c?.hasError('required'))   return 'O motivo é obrigatório.';
    if (c?.hasError('minlength'))  return 'Descreva o motivo com pelo menos 10 caracteres.';
    return '';
  }

  get selectedFileName(): string { return this.matriculaFile()?.name ?? ''; }

  get selectedFileSize(): string {
    const f = this.matriculaFile();
    if (!f) return '';
    if (f.size < 1024)        return `${f.size} B`;
    if (f.size < 1024 * 1024) return `${(f.size / 1024).toFixed(1)} KB`;
    return `${(f.size / (1024 * 1024)).toFixed(1)} MB`;
  }
}