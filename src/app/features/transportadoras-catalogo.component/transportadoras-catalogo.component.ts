import { Component, OnInit, OnDestroy, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Subject, debounceTime, distinctUntilChanged, takeUntil } from 'rxjs';
import { TransportadorasService, Transportadora, TransportadoraCreateDto, TransportadoraUpdateDto, PagedResult } from '../../core/services/transportadoras-catalogo.service';
import { PdfService, PdfField } from '../../core/services/pdf.service';

type ViewState = 'list' | 'create' | 'edit' | 'details';

const NIF_PT_REGEX = /^[0-9]{9}$/;
const NIF_INTL_REGEX = /^[A-Z0-9]{5,20}$/;
const PHONE_REGEX = /^(\+?\d{1,4}[\s-]?)?\(?\d{1,4}\)?[\s-]?\d{1,9}[\s-]?\d{1,9}$/;
const POSTAL_CODE_REGEX = /^\d{4}-\d{3}$/;

@Component({
  selector: 'app-transportadoras',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './transportadoras-catalogo.component.html',
  styleUrls: ['./transportadoras-catalogo.component.css']
})
export class TransportadorasComponent implements OnInit, OnDestroy {
  private svc = inject(TransportadorasService);
  private fb = inject(FormBuilder);
  private pdfService = inject(PdfService);
  private destroy$ = new Subject<void>();

  currentState = signal<ViewState>('list');
  editingId = signal<number | null>(null);
  isEditing = computed(() => this.currentState() === 'edit');
  isViewing = computed(() => this.currentState() === 'details');
  selectedTransportadora = computed(() => this.transportadoras().find(t => t.id === this.editingId()) ?? null);

  pagedResult = signal<PagedResult<Transportadora> | null>(null);
  transportadoras = computed(() => this.pagedResult()?.items ?? []);
  isLoading = signal(false);
  isSaving = signal(false);
  errorMsg = signal<string | null>(null);
  successMsg = signal<string | null>(null);

  filtroSearch = '';
  mostrarInativos = signal(false);
  currentPage = 1;
  readonly pageSize = 15;
  private searchInput$ = new Subject<string>();

  form!: FormGroup;

  showDeleteConfirm = signal(false);
  transportadoraParaDelete = signal<Transportadora | null>(null);

  totalTransportadoras = computed(() => this.pagedResult()?.total ?? 0);
  totalPages = computed(() => this.pagedResult()?.totalPages ?? 0);
  pages = computed(() => Array.from({ length: this.totalPages() }, (_, i) => i + 1));

  totalAtivas = computed(() => this.transportadoras().filter(t => t.ativo).length);
  totalInativas = computed(() => this.transportadoras().filter(t => !t.ativo).length);
  totalComContacto = computed(() => this.transportadoras().filter(t => t.telefone || t.email).length);
  percentagemAtivas = computed(() => {
    const total = this.totalTransportadoras();
    if (total === 0) return 0;
    return Math.round((this.totalAtivas() / total) * 100);
  });

  ngOnInit(): void {
    this.initForm();
    this.setupSearchDebounce();
    this.carregarTransportadoras();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private initForm(): void {
    this.form = this.fb.group({
      codigo: [{ value: '', disabled: true }],
      nome: ['', [Validators.required, Validators.maxLength(200)]],
      nif: ['', [Validators.maxLength(20), Validators.pattern(NIF_PT_REGEX)]],
      telefone: ['', [Validators.maxLength(30), Validators.pattern(PHONE_REGEX)]],
      email: ['', [Validators.maxLength(200), Validators.email]],
      localidade: ['', Validators.maxLength(100)],
      codigoPostal: ['', [Validators.maxLength(20), Validators.pattern(POSTAL_CODE_REGEX)]],
      pais: ['Portugal', Validators.maxLength(100)],
      contactoNome: ['', Validators.maxLength(150)],
      contactoTelefone: ['', [Validators.maxLength(30), Validators.pattern(PHONE_REGEX)]],
      observacoes: [''],
      ativo: [true],
      criadoEm: [{ value: '', disabled: true }]
    });
  }

  private setupSearchDebounce(): void {
    this.searchInput$
      .pipe(debounceTime(350), distinctUntilChanged(), takeUntil(this.destroy$))
      .subscribe(() => {
        this.currentPage = 1;
        this.carregarTransportadoras();
      });
  }

  private populateForm(transportadora: Transportadora): void {
    this.form.enable();
    const isView = this.isViewing();

    this.form.patchValue({
      codigo: transportadora.codigo,
      nome: transportadora.nome,
      nif: transportadora.nif || '',
      telefone: transportadora.telefone || '',
      email: transportadora.email || '',
      localidade: transportadora.localidade || '',
      codigoPostal: transportadora.codigoPostal || '',
      pais: transportadora.pais || 'Portugal',
      contactoNome: transportadora.contactoNome || '',
      contactoTelefone: transportadora.contactoTelefone || '',
      observacoes: transportadora.observacoes || '',
      ativo: transportadora.ativo,
      criadoEm: transportadora.criadoEm ? new Date(transportadora.criadoEm).toLocaleString() : ''
    });

    if (isView) {
      this.form.disable();
    }
  }

  carregarTransportadoras(): void {
    this.isLoading.set(true);
    this.svc.listar({
      search: this.filtroSearch || undefined,
      ativo: this.mostrarInativos() ? undefined : true,
      page: this.currentPage,
      pageSize: this.pageSize,
      orderBy: 'nome',
      orderDir: 'asc'
    }).subscribe({
      next: (res) => { this.pagedResult.set(res); this.isLoading.set(false); },
      error: (err) => { this.errorMsg.set(err.message); this.isLoading.set(false); }
    });
  }

  onSearchChange(value: string): void {
    this.filtroSearch = value;
    this.searchInput$.next(value);
  }

  toggleInativos(): void {
    this.mostrarInativos.update(v => !v);
    this.currentPage = 1;
    this.carregarTransportadoras();
  }

  goToPage(page: number): void {
    if (page < 1 || page > this.totalPages()) return;
    this.currentPage = page;
    this.carregarTransportadoras();
  }

  goToCreate(): void {
    this.currentState.set('create');
    this.editingId.set(null);
    this.form.reset({ ativo: true, pais: 'Portugal', criadoEm: '' });
    this.form.enable();
    this.form.get('codigo')?.disable();
    this.errorMsg.set(null);

    // show placeholder and fetch next transportadora code
    try { this.form.get('codigo')?.setValue('A gerar...'); } catch {}
    this.svc.getNextCodigo().subscribe({
      next: code => {
        console.log('Next transportadora code', code);
        try { this.form.get('codigo')?.setValue(code); } catch {}
      },
      error: err => {
        console.error('Erro getNextCodigo transportadora', err);
        this.errorMsg.set('Não foi possível gerar o código da transportadora. Faça login novamente ou recarregue a página.');
        try { this.form.get('codigo')?.setValue(''); } catch {}
      }
    });
  }

  goToEdit(transportadora: Transportadora): void {
    this.currentState.set('edit');
    this.editingId.set(transportadora.id);
    this.populateForm(transportadora);
    this.errorMsg.set(null);
  }

  goToDetails(transportadora: Transportadora, event?: Event): void {
    if (event) event.stopPropagation();
    this.currentState.set('details');
    this.editingId.set(transportadora.id);
    this.populateForm(transportadora);
    this.errorMsg.set(null);
  }

  cancel(): void {
    this.currentState.set('list');
    this.editingId.set(null);
    this.form.reset();
    this.errorMsg.set(null);
  }

  salvarTransportadora(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid) {
      this.errorMsg.set('Corrija os erros no formulário.');
      return;
    }

    this.isSaving.set(true);
    const raw = this.form.getRawValue();

    if (this.isEditing() && this.editingId()) {
      const dto: TransportadoraUpdateDto = {
        nome: raw.nome.trim(),
        nif: raw.nif?.trim() || undefined,
        telefone: raw.telefone?.trim() || undefined,
        email: raw.email?.trim() || undefined,
        localidade: raw.localidade?.trim() || undefined,
        codigoPostal: raw.codigoPostal?.trim() || undefined,
        pais: raw.pais?.trim() || 'Portugal',
        contactoNome: raw.contactoNome?.trim() || undefined,
        contactoTelefone: raw.contactoTelefone?.trim() || undefined,
        observacoes: raw.observacoes?.trim() || undefined,
        ativo: raw.ativo
      };
      this.svc.atualizar(this.editingId()!, dto).subscribe({
        next: () => { this.onSaveSuccess('Transportadora atualizada com sucesso'); },
        error: (err) => { this.onSaveError(err); }
      });
    } else {
      const dto: TransportadoraCreateDto = {
        nome: raw.nome.trim(),
        nif: raw.nif?.trim() || undefined,
        telefone: raw.telefone?.trim() || undefined,
        email: raw.email?.trim() || undefined,
        localidade: raw.localidade?.trim() || undefined,
        codigoPostal: raw.codigoPostal?.trim() || undefined,
        pais: raw.pais?.trim() || 'Portugal',
        contactoNome: raw.contactoNome?.trim() || undefined,
        contactoTelefone: raw.contactoTelefone?.trim() || undefined,
        observacoes: raw.observacoes?.trim() || undefined
      };
      this.svc.criar(dto).subscribe({
        next: transportadora => this._onCreateSuccess(transportadora, 'Transportadora criada com sucesso'),
        error: (err) => { this.onSaveError(err); }
      });
    }
  }

  private onSaveSuccess(message: string): void {
    this.isSaving.set(false);
    this.cancel();
    this.carregarTransportadoras();
    this.showToast(message);
  }

  private _onCreateSuccess(transportadora: Transportadora, message: string): void {
    this.isSaving.set(false);
    this.populateForm(transportadora);
    this.currentState.set('details');
    this.editingId.set(transportadora.id);
    this.showToast(message);
  }

  private onSaveError(err: any): void {
    this.errorMsg.set(err.message);
    this.isSaving.set(false);
  }

  confirmarDesativar(transportadora: Transportadora): void {
    this.transportadoraParaDelete.set(transportadora);
    this.showDeleteConfirm.set(true);
  }

  cancelarDelete(): void {
    this.showDeleteConfirm.set(false);
    this.transportadoraParaDelete.set(null);
  }

  executarDesativar(): void {
    const t = this.transportadoraParaDelete();
    if (!t) return;
    this.svc.deletar(t.id).subscribe({
      next: () => {
        this.cancelarDelete();
        this.carregarTransportadoras();
        this.showToast('Transportadora desactivada com sucesso');
      },
      error: (err) => this.errorMsg.set(err.message)
    });
  }

  ativarTransportadora(transportadora: Transportadora): void {
    if (!confirm(`Activar a transportadora ${transportadora.nome}?`)) return;
    this.svc.ativar(transportadora.id).subscribe({
      next: () => {
        this.carregarTransportadoras();
        this.showToast('Transportadora activada com sucesso');
      },
      error: (err) => this.errorMsg.set(err.message)
    });
  }

  imprimirPdf(transportadora: Transportadora, event?: Event): void {
    if (event) event.stopPropagation();
    const fields: PdfField[] = [
      { label: 'Código', value: transportadora.codigo },
      { label: 'Nome', value: transportadora.nome },
      { label: 'NIF', value: transportadora.nif || '—' },
      { label: 'Telefone', value: transportadora.telefone || '—' },
      { label: 'Email', value: transportadora.email || '—' },
      { label: 'Localidade', value: transportadora.localidade || '—' },
      { label: 'Código Postal', value: transportadora.codigoPostal || '—' },
      { label: 'País', value: transportadora.pais || '—' },
      { label: 'Contacto', value: transportadora.contactoNome || '—' },
      { label: 'Telefone Contacto', value: transportadora.contactoTelefone || '—' },
      { label: 'Estado', value: transportadora.ativo ? 'Ativo' : 'Inativo' },
      { label: 'Data de Registo', value: new Date(transportadora.criadoEm).toLocaleString() }
    ];
    const blob = this.pdfService.generateEntityPdf(`Transportadora ${transportadora.codigo}`, fields);
    this.pdfService.downloadPdf(blob, `Transportadora_${transportadora.codigo}.pdf`);
  }

  showToast(msg: string): void {
    this.successMsg.set(msg);
    setTimeout(() => this.successMsg.set(null), 3500);
  }
  clearError(): void {
    this.errorMsg.set(null);
  }

  hasError(name: string, error?: string): boolean {
    const c = this.form.get(name);
    if (!c || !c.invalid || !c.touched) return false;
    return error ? c.hasError(error) : true;
  }
}