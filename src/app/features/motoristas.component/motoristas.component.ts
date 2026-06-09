import { Component, OnInit, OnDestroy, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Subject, debounceTime, distinctUntilChanged, takeUntil } from 'rxjs';
import { MotoristasService, Motorista as MotoristaModel, MotoristaCreateDto, MotoristaUpdateDto } from '../../core/services/motoristas.service';
import { PdfService, PdfField } from '../../core/services/pdf.service';

type ViewState = 'list' | 'create' | 'edit' | 'details';

type Motorista = MotoristaModel;

interface PagedResult<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
  totalPages: number;
}
@Component({
  selector: 'app-motoristas',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './motoristas.component.html',
  styleUrls: ['./motoristas.component.css']
})
export class MotoristasComponent implements OnInit, OnDestroy {
  private svc = inject(MotoristasService);
  private fb = inject(FormBuilder);
  private pdfService = inject(PdfService);
  private destroy$ = new Subject<void>();

  currentState = signal<ViewState>('list');
  editingId = signal<number | null>(null);
  isEditing = computed(() => this.currentState() === 'edit');
  isViewing = computed(() => this.currentState() === 'details');
  selectedMotorista = computed(() => this.motoristas().find(m => m.id === this.editingId()) ?? null);

  pagedResult = signal<PagedResult<Motorista> | null>(null);
  motoristas = computed(() => this.pagedResult()?.items ?? []);
  isLoading = signal(false);
  isSaving = signal(false);
  errorMsg = signal<string | null>(null);
  successMsg = signal<string | null>(null);

  filtroSearch = '';
  mostrarInativos = false;
  currentPage = 1;
  readonly pageSize = 15;
  private searchInput$ = new Subject<string>();

  form!: FormGroup;

  showDeleteConfirm = signal(false);
  motoristaParaDelete = signal<Motorista | null>(null);

  totalMotoristas = computed(() => this.pagedResult()?.total ?? 0);
  totalPages = computed(() => this.pagedResult()?.totalPages ?? 0);
  pages = computed(() => Array.from({ length: this.totalPages() }, (_, i) => i + 1));

  totalAtivos = computed(() => this.motoristas().filter(m => m.ativo).length);
  totalInativos = computed(() => this.motoristas().filter(m => !m.ativo).length);
  totalComCartas = computed(() => this.motoristas().filter(m => m.cartaConducao?.length > 0 && !this.isCartaVencida(m.validadeCartaConducao)).length);
  getPercentagemAtivos = computed(() => {
    const total = this.totalMotoristas();
    if (total === 0) return 0;
    return Math.round((this.totalAtivos() / total) * 100);
  });

  ngOnInit(): void {
    this.initForm();
    this.setupSearchDebounce();
    this.carregarMotoristas();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private initForm(): void {
    this.form = this.fb.group({
      nome: ['', [Validators.required, Validators.maxLength(200)]],
      telefone: ['', [Validators.required, Validators.maxLength(30)]],
      nif: ['', [Validators.maxLength(20), Validators.pattern(/^[A-Za-z0-9\-]{5,20}$/)]],
      cartaConducao: ['', [Validators.required, Validators.maxLength(50)]],
      validadeCartaConducao: ['', [Validators.required]],
      transportadoraId: ['', [Validators.required, Validators.maxLength(50)]],
      status: [{ value: '', disabled: true }],
      criadoEm: [{ value: '', disabled: true }]
    });
  }

  private setupSearchDebounce(): void {
    this.searchInput$
      .pipe(debounceTime(350), distinctUntilChanged(), takeUntil(this.destroy$))
      .subscribe(() => {
        this.currentPage = 1;
        this.carregarMotoristas();
      });
  }

  private populateForm(motorista: Motorista): void {
    this.form.enable();
    const isView = this.isViewing();

    this.form.patchValue({
      nome: motorista.nome,
      telefone: motorista.telefone,
      nif: motorista.nif ?? '',
      cartaConducao: motorista.cartaConducao,
      validadeCartaConducao: motorista.validadeCartaConducao ?? '',
      transportadoraId: motorista.transportadoraId?.toString() ?? '',
      status: motorista.ativo ? 'Ativo' : 'Inativo',
      criadoEm: motorista.criadoEm ? new Date(motorista.criadoEm).toLocaleString() : ''
    });

    if (isView) {
      this.form.disable();
    }
  }

  carregarMotoristas(): void {
    this.isLoading.set(true);
    this.svc.listar({
      search: this.filtroSearch || undefined,
      ativo: this.mostrarInativos ? undefined : true,
      page: this.currentPage,
      pageSize: this.pageSize,
      orderBy: 'nome',
      orderDir: 'asc'
    }).subscribe({
      next: (res) => {
        this.pagedResult.set(res);
        this.isLoading.set(false);
      },
      error: (err) => { this.errorMsg.set(err.message); this.isLoading.set(false); }
    });
  }

  onSearchChange(value: string): void {
    this.filtroSearch = value;
    this.searchInput$.next(value);
  }

  toggleInativos(): void {
    this.mostrarInativos = !this.mostrarInativos;
    this.currentPage = 1;
    this.carregarMotoristas();
  }

  goToPage(page: number): void {
    if (page < 1 || page > this.totalPages()) return;
    this.currentPage = page;
    this.carregarMotoristas();
  }

  goToCreate(): void {
    this.currentState.set('create');
    this.editingId.set(null);
    this.form.reset({ ativo: true, criadoEm: '' });
    this.form.enable();
    this.errorMsg.set(null);
  }

  goToEdit(motorista: Motorista): void {
    this.currentState.set('edit');
    this.editingId.set(motorista.id);
    this.populateForm(motorista);
    this.errorMsg.set(null);
  }

  goToDetails(motorista: Motorista, event?: Event): void {
    if (event) event.stopPropagation();
    this.currentState.set('details');
    this.editingId.set(motorista.id);
    this.populateForm(motorista);
    this.errorMsg.set(null);
  }

  cancel(): void {
    this.currentState.set('list');
    this.editingId.set(null);
    this.form.reset();
    this.errorMsg.set(null);
  }

  salvarMotorista(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid) {
      this.errorMsg.set('Corrija os erros no formulário.');
      return;
    }

    this.isSaving.set(true);
    const raw = this.form.getRawValue();

    if (this.isEditing() && this.editingId()) {
      const currentMotorista = this.motoristas().find(m => m.id === this.editingId());
      const dto: MotoristaUpdateDto = {
        nome: raw.nome.trim(),
        telefone: raw.telefone.trim(),
        nif: raw.nif?.trim() || undefined,
        cartaConducao: raw.cartaConducao.trim().toUpperCase(),
        validadeCartaConducao: raw.validadeCartaConducao,
        ativo: currentMotorista?.ativo ?? true,
        transportadoraId: raw.transportadoraId?.toString().trim() ?? ''
      };
      this.svc.atualizar(this.editingId()!, dto).subscribe({
        next: () => { this.onSaveSuccess('Motorista atualizado com sucesso'); },
        error: (err) => { this.onSaveError(err); }
      });
    } else {
      const dto: MotoristaCreateDto = {
        nome: raw.nome.trim(),
        telefone: raw.telefone.trim(),
        nif: raw.nif?.trim() || undefined,
        cartaConducao: raw.cartaConducao.trim().toUpperCase(),
        validadeCartaConducao: raw.validadeCartaConducao,
        transportadoraId: raw.transportadoraId?.toString().trim() ?? ''
      };
      this.svc.criar(dto).subscribe({
        next: () => { this.onSaveSuccess('Motorista criado com sucesso'); },
        error: (err) => { this.onSaveError(err); }
      });
    }
  }

  private onSaveSuccess(message: string): void {
    this.isSaving.set(false);
    this.cancel();
    this.carregarMotoristas();
    this.showToast(message);
  }

  private onSaveError(err: any): void {
    this.errorMsg.set(err.message);
    this.isSaving.set(false);
  }

  confirmarDesativar(motorista: Motorista): void {
    this.motoristaParaDelete.set(motorista);
    this.showDeleteConfirm.set(true);
  }

  cancelarDelete(): void {
    this.showDeleteConfirm.set(false);
    this.motoristaParaDelete.set(null);
  }

  executarDesativar(): void {
    const m = this.motoristaParaDelete();
    if (!m) return;
    this.svc.deletar(m.id).subscribe({
      next: () => {
        this.cancelarDelete();
        this.carregarMotoristas();
        this.showToast('Motorista desativado com sucesso');
      },
      error: (err) => this.errorMsg.set(err.message)
    });
  }

  ativarMotorista(motorista: Motorista): void {
    if (!confirm(`Ativar o motorista ${motorista.nome}?`)) return;
    this.svc.ativar(motorista.id).subscribe({
      next: () => {
        this.carregarMotoristas();
        this.showToast('Motorista ativado com sucesso');
      },
      error: (err) => this.errorMsg.set(err.message)
    });
  }

  imprimirPdf(motorista: Motorista, event?: Event): void {
    if (event) event.stopPropagation();
    const fields: PdfField[] = [
      { label: 'ID', value: motorista.id.toString() },
      { label: 'Nome', value: motorista.nome },
      { label: 'Telefone', value: motorista.telefone },
      { label: 'NIF', value: motorista.nif || '—' },
      { label: 'Carta de Condução', value: motorista.cartaConducao },
      { label: 'Validade da Carta', value: motorista.validadeCartaConducao ? this.formatarData(motorista.validadeCartaConducao) : '—' },
      { label: 'ID Transportadora', value: motorista.transportadoraId?.toString() ?? '—' },
      { label: 'Estado', value: motorista.ativo ? 'Ativo' : 'Inativo' },
      { label: 'Data de Registo', value: motorista.criadoEm ? new Date(motorista.criadoEm).toLocaleString() : '—' }
    ];
    const blob = this.pdfService.generateEntityPdf(`Motorista ${motorista.nome}`, fields);
    this.pdfService.downloadPdf(blob, `Motorista_${motorista.nome.replace(/\s+/g, '_')}.pdf`);
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

  formatarData(value?: string | null): string {
    if (!value) return '—';
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) return '—';
    return date.toLocaleDateString();
  }

  formatCartaConducao(): void {
    const control = this.form.get('cartaConducao');
    if (control?.value) {
      control.setValue(control.value.toUpperCase(), { emitEvent: false });
    }
  }

  private isDataVencida(value?: string | null): boolean {
    if (!value) return false;
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) return false;
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    return date < today;
  }

  isCartaVencida(validade?: string | null): boolean {
    return this.isDataVencida(validade);
  }

  isFormCartaVencida(): boolean {
    return this.isDataVencida(this.form.get('validadeCartaConducao')?.value);
  }
}