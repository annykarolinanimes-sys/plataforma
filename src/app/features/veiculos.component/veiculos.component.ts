import { Component, OnInit, OnDestroy, inject, signal, computed, effect } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Subject, debounceTime, distinctUntilChanged, takeUntil } from 'rxjs';
import { VeiculosService, Veiculo, PagedResult } from '../../core/services/veiculos.service';
import { PdfService, PdfField } from '../../core/services/pdf.service';

const MATRICULA_REGEX = /^([A-Z]{2}-\d{2}-[A-Z]{2}|\d{2}-[A-Z]{2}-\d{2}|\d{2}-\d{2}-[A-Z]{2})$/;

type ViewState = 'list' | 'create' | 'edit' | 'details';

@Component({
  selector: 'app-veiculos',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './veiculos.component.html',
  styleUrls: ['./veiculos.component.css']
})
export class VeiculosComponent implements OnInit, OnDestroy {
  private svc = inject(VeiculosService);
  private fb = inject(FormBuilder);
  private pdfService = inject(PdfService);
  private destroy$ = new Subject<void>();

  // --- State Management ---
  currentState = signal<ViewState>('list');
  editingId = signal<number | null>(null);
  isEditing = computed(() => this.currentState() === 'edit');
  isViewing = computed(() => this.currentState() === 'details');

  // --- Data Signals ---
  pagedResult = signal<PagedResult<Veiculo> | null>(null);
  veiculos = computed(() => this.pagedResult()?.items ?? []);
  isLoading = signal(false);
  isSaving = signal(false);
  errorMsg = signal<string | null>(null);
  successMsg = signal<string | null>(null);

  // --- Filters ---
  filtroSearch = '';
  mostrarInativos = false;
  currentPage = 1;
  readonly pageSize = 15;
  private searchInput$ = new Subject<string>();

  // --- Form ---
  form!: FormGroup;
  readonly currentYear = new Date().getFullYear();
  readonly combustivelOpcoes = ['Gasolina', 'Diesel', 'Híbrido', 'Eléctrico', 'GPL', 'Hidrogénio'];

  // --- UI for Delete Confirm ---
  showDeleteConfirm = signal(false);
  veiculoParaDelete = signal<Veiculo | null>(null);

  // --- Computed Helpers for Template ---
  get totalVeiculos(): number { return this.pagedResult()?.total ?? 0; }
  get totalPages(): number { return this.pagedResult()?.totalPages ?? 0; }
  get pages(): number[] { return Array.from({ length: this.totalPages }, (_, i) => i + 1); }

  // Stats for Cards
  get totalAtivos(): number { return this.veiculos().filter(v => v.ativo).length; }
  get totalInativos(): number { return this.veiculos().filter(v => !v.ativo).length; }
  get veiculosPorCombustivel(): number {
    const combustiveis = new Set(this.veiculos().map(v => v.tipoCombustivel).filter(Boolean));
    return combustiveis.size;
  }

  ngOnInit(): void {
    this.initForm();
    this.setupSearchDebounce();
    this.carregarVeiculos();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private initForm(): void {
    this.form = this.fb.group({
      matricula: ['', [Validators.required, Validators.maxLength(20), Validators.pattern(MATRICULA_REGEX)]],
      marca: ['', [Validators.required, Validators.maxLength(100)]],
      modelo: ['', [Validators.required, Validators.maxLength(100)]],
      cor: ['', Validators.maxLength(50)],
      ano: [null, [Validators.min(1900), Validators.max(this.currentYear + 1)]],
      vin: ['', Validators.maxLength(50)],
      tipoCombustivel: [''],
      cilindrada: [null, [Validators.min(0), Validators.max(99999)]],
      potencia: [null, [Validators.min(0), Validators.max(9999)]],
      lugares: [null, [Validators.min(1), Validators.max(200)]],
      peso: [null, [Validators.min(0)]],
      observacoes: [''],
      proprietarioId: [null],
      ativo: [true],
      criadoEm: [{ value: '', disabled: true }]
    });
  }

  private setupSearchDebounce(): void {
    this.searchInput$.pipe(debounceTime(350), distinctUntilChanged(), takeUntil(this.destroy$))
      .subscribe(() => { this.currentPage = 1; this.carregarVeiculos(); });
  }

  private populateForm(veiculo: Veiculo): void {
    // Enable the form if it was disabled (for details mode)
    this.form.enable();
    // If it's a view, we will disable it AFTER populating
    const isView = this.isViewing();

    this.form.patchValue({
      matricula: veiculo.matricula,
      marca: veiculo.marca,
      modelo: veiculo.modelo,
      cor: veiculo.cor ?? '',
      ano: veiculo.ano ?? null,
      vin: veiculo.vin ?? '',
      tipoCombustivel: veiculo.tipoCombustivel ?? '',
      cilindrada: veiculo.cilindrada ?? null,
      potencia: veiculo.potencia ?? null,
      lugares: veiculo.lugares ?? null,
      peso: veiculo.peso ?? null,
      observacoes: veiculo.observacoes ?? '',
      proprietarioId: veiculo.proprietarioId ?? null,
      ativo: veiculo.ativo,
      criadoEm: veiculo.criadoEm ? new Date(veiculo.criadoEm).toLocaleString() : ''
    });

    if (isView) {
      this.form.disable();
    } else if (this.isEditing()) {
      // Make matricula readonly in edit mode via form control, not HTML attribute
      this.form.get('matricula')?.disable();
    } else {
      this.form.get('matricula')?.enable();
    }
  }

  // --- API Calls ---
  carregarVeiculos(): void {
    this.isLoading.set(true);
    this.svc.listar({
      search: this.filtroSearch || undefined,
      ativo: this.mostrarInativos ? undefined : true,
      page: this.currentPage,
      pageSize: this.pageSize,
    }).subscribe({
      next: (res) => { this.pagedResult.set(res); this.isLoading.set(false); },
      error: (err) => { this.errorMsg.set(err.message); this.isLoading.set(false); }
    });
  }

  // --- User Actions (List) ---
  onSearchChange(value: string): void {
    this.filtroSearch = value;
    this.searchInput$.next(value);
  }
  toggleInativos(): void { this.mostrarInativos = !this.mostrarInativos; this.currentPage = 1; this.carregarVeiculos(); }
  goToPage(page: number): void { if (page < 1 || page > this.totalPages) return; this.currentPage = page; this.carregarVeiculos(); }

  goToCreate(): void {
    this.currentState.set('create');
    this.editingId.set(null);
    this.form.reset({ ativo: true, criadoEm: '' });
    this.form.enable();
    this.form.get('matricula')?.enable();
    this.errorMsg.set(null);
  }

  goToEdit(v: Veiculo): void {
    this.currentState.set('edit');
    this.editingId.set(v.id);
    this.populateForm(v);
    this.errorMsg.set(null);
  }

  goToDetails(v: Veiculo, event?: Event): void {
    if (event) event.stopPropagation();
    this.currentState.set('details');
    this.editingId.set(v.id);
    this.populateForm(v);
    this.errorMsg.set(null);
  }

  cancel(): void {
    this.currentState.set('list');
    this.editingId.set(null);
    this.form.reset();
    this.errorMsg.set(null);
  }

  // --- Save Logic ---
  salvarVeiculo(): void {
    // Re-enable matricula temporarily if it was disabled for validation
    const matriculaControl = this.form.get('matricula');
    const wasMatriculaDisabled = matriculaControl?.disabled;
    if (wasMatriculaDisabled) matriculaControl?.enable();

    this.form.markAllAsTouched();
    if (this.form.invalid) {
      if (wasMatriculaDisabled) matriculaControl?.disable();
      this.errorMsg.set('Corrija os erros no formulário.');
      return;
    }

    this.isSaving.set(true);
    const raw = this.form.getRawValue();
    const dto: Partial<Veiculo> = {
      matricula: raw.matricula?.trim().toUpperCase(),
      marca: raw.marca?.trim(),
      modelo: raw.modelo?.trim(),
      cor: raw.cor?.trim() || undefined,
      ano: raw.ano || undefined,
      vin: raw.vin?.trim() || undefined,
      tipoCombustivel: raw.tipoCombustivel || undefined,
      cilindrada: raw.cilindrada || undefined,
      potencia: raw.potencia || undefined,
      lugares: raw.lugares || undefined,
      peso: raw.peso || undefined,
      observacoes: raw.observacoes?.trim() || undefined,
      proprietarioId: raw.proprietarioId || undefined,
      ativo: raw.ativo,
    };

    const req$ = this.isEditing() && this.editingId()
      ? this.svc.atualizar(this.editingId()!, dto as Veiculo)
      : this.svc.criar(dto as Veiculo);

    req$.subscribe({
      next: () => {
        this.isSaving.set(false);
        this.cancel(); // Go back to list
        this.carregarVeiculos();
        this.showToast(this.isEditing() ? 'Veículo actualizado com sucesso' : 'Veículo criado com sucesso');
      },
      error: (err) => {
        this.errorMsg.set(err.message);
        this.isSaving.set(false);
        if (wasMatriculaDisabled) matriculaControl?.disable();
      }
    });
  }

  // --- Delete / Activate ---
  desativarVeiculo(v: Veiculo): void {
    this.veiculoParaDelete.set(v);
    this.showDeleteConfirm.set(true);
  }
  cancelarDelete(): void { this.showDeleteConfirm.set(false); this.veiculoParaDelete.set(null); }
  executarDesativar(): void {
    const v = this.veiculoParaDelete();
    if (!v) return;
    this.svc.deletar(v.id).subscribe({
      next: () => { this.cancelarDelete(); this.carregarVeiculos(); this.showToast('Veículo desactivado com sucesso'); },
      error: (err) => this.errorMsg.set(err.message)
    });
  }
  ativarVeiculo(v: Veiculo): void {
    if (!confirm(`Activar o veículo ${v.matricula}?`)) return;
    this.svc.ativar(v.id).subscribe({
      next: () => { this.carregarVeiculos(); this.showToast('Veículo activado com sucesso'); },
      error: (err) => this.errorMsg.set(err.message)
    });
  }

  // --- PDF ---
  imprimirPdf(veiculo: Veiculo, event?: Event): void {
    if (event) event.stopPropagation();
    const fields: PdfField[] = [
      { label: 'Matrícula', value: veiculo.matricula }, { label: 'Marca', value: veiculo.marca },
      { label: 'Modelo', value: veiculo.modelo }, { label: 'Cor', value: veiculo.cor || '—' },
      { label: 'Ano', value: veiculo.ano || '—' }, { label: 'VIN', value: veiculo.vin || '—' },
      { label: 'Combustível', value: veiculo.tipoCombustivel || '—' }, { label: 'Cilindrada', value: veiculo.cilindrada ?? '—' },
      { label: 'Potência', value: veiculo.potencia ?? '—' }, { label: 'Lugares', value: veiculo.lugares ?? '—' },
      { label: 'Peso', value: veiculo.peso ?? '—' }, { label: 'Activo', value: veiculo.ativo ? 'Sim' : 'Não' }
    ];
    const blob = this.pdfService.generateEntityPdf(`Veículo ${veiculo.matricula}`, fields);
    this.pdfService.downloadPdf(blob, `Veiculo_${veiculo.matricula}.pdf`);
  }

  // --- Utils ---
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
    if (c?.hasError('pattern')) return 'Formato inválido. Use: AA-00-AA, 00-AA-00 ou 00-00-AA.';
    return 'Matrícula inválida.';
  }
}