import { Component, OnInit, OnDestroy, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Subject, debounceTime, distinctUntilChanged, takeUntil } from 'rxjs';
import { UserService, MotoristaDto, CreateMotoristaRequest, UpdateMotoristaRequest } from '../../core/services/user.service';
import { PdfService, PdfField } from '../../core/services/pdf.service';

type ViewState = 'list' | 'create' | 'edit' | 'details';

type Motorista = MotoristaDto & { ativo: boolean; criadoEm?: string; atualizadoEm?: string };

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
  private svc = inject(UserService);
  private fb = inject(FormBuilder);
  private pdfService = inject(PdfService);
  private destroy$ = new Subject<void>();

  // State Management
  currentState = signal<ViewState>('list');
  editingId = signal<number | null>(null);
  isEditing = computed(() => this.currentState() === 'edit');
  isViewing = computed(() => this.currentState() === 'details');

  // Data Signals
  pagedResult = signal<PagedResult<Motorista> | null>(null);
  motoristas = computed(() => this.pagedResult()?.items ?? []);
  isLoading = signal(false);
  isSaving = signal(false);
  errorMsg = signal<string | null>(null);
  successMsg = signal<string | null>(null);

  // Filters
  filtroSearch = '';
  mostrarInativos = false;
  currentPage = 1;
  readonly pageSize = 15;
  private searchInput$ = new Subject<string>();

  // Form
  form!: FormGroup;

  // Delete confirm
  showDeleteConfirm = signal(false);
  motoristaParaDelete = signal<Motorista | null>(null);

  // Computed helpers for template
  totalMotoristas = computed(() => this.pagedResult()?.total ?? 0);
  totalPages = computed(() => this.pagedResult()?.totalPages ?? 0);
  pages = computed(() => Array.from({ length: this.totalPages() }, (_, i) => i + 1));

  // Stats for cards
  totalAtivos = computed(() => this.motoristas().filter(m => m.ativo).length);
  totalInativos = computed(() => this.motoristas().filter(m => !m.ativo).length);
  totalComCartas = computed(() => this.motoristas().filter(m => m.cartaConducao?.length > 0).length);
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
      cartaConducao: ['', [Validators.required, Validators.maxLength(50)]],
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
      cartaConducao: motorista.cartaConducao,
      transportadoraId: motorista.transportadoraId?.toString() ?? '',
      status: motorista.status ?? '',
      criadoEm: motorista.criadoEm ? new Date(motorista.criadoEm).toLocaleString() : ''
    });

    if (isView) {
      this.form.disable();
    }
  }

  // API Calls
  carregarMotoristas(): void {
    this.isLoading.set(true);
    this.svc.listarMotoristas(undefined, this.filtroSearch || undefined, this.mostrarInativos ? undefined : true).subscribe({
      next: (res) => {
        const items = res.map(m => ({ ...m, ativo: m.status?.toLowerCase() === 'ativo' }));
        this.pagedResult.set({
          items,
          total: items.length,
          page: 1,
          pageSize: items.length,
          totalPages: 1
        });
        this.isLoading.set(false);
      },
      error: (err) => { this.errorMsg.set(err.message); this.isLoading.set(false); }
    });
  }

  // User Actions (List)
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

  // Save Logic
  salvarMotorista(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid) {
      this.errorMsg.set('Corrija os erros no formulário.');
      return;
    }

    this.isSaving.set(true);
    const raw = this.form.getRawValue();

    if (this.isEditing() && this.editingId()) {
      const dto: UpdateMotoristaRequest = {
        nome: raw.nome.trim(),
        telefone: raw.telefone.trim(),
        cartaConducao: raw.cartaConducao.trim().toUpperCase(),
        transportadoraId: raw.transportadoraId ? Number(raw.transportadoraId) : undefined
      };
      this.svc.atualizarMotorista(this.editingId()!, dto).subscribe({
        next: () => { this.onSaveSuccess('Motorista actualizado com sucesso'); },
        error: (err) => { this.onSaveError(err); }
      });
    } else {
      const transportadoraId = Number(raw.transportadoraId);
      if (!transportadoraId || Number.isNaN(transportadoraId)) {
        this.errorMsg.set('Transportadora inválida. Introduza um ID numérico válido.');
        this.isSaving.set(false);
        return;
      }

      const dto: CreateMotoristaRequest = {
        nome: raw.nome.trim(),
        telefone: raw.telefone.trim(),
        cartaConducao: raw.cartaConducao.trim().toUpperCase(),
        transportadoraId
      };
      this.svc.criarMotorista(dto).subscribe({
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

  // Delete / Activate
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
    this.svc.eliminarMotorista(m.id).subscribe({
      next: () => {
        this.cancelarDelete();
        this.carregarMotoristas();
        this.showToast('Motorista desactivado com sucesso');
      },
      error: (err) => this.errorMsg.set(err.message)
    });
  }

  ativarMotorista(motorista: Motorista): void {
    if (!confirm(`Activar o motorista ${motorista.nome}?`)) return;
    this.svc.ativarMotorista(motorista.id).subscribe({
      next: () => {
        this.carregarMotoristas();
        this.showToast('Motorista activado com sucesso');
      },
      error: (err) => this.errorMsg.set(err.message)
    });
  }

  // PDF
  imprimirPdf(motorista: Motorista, event?: Event): void {
    if (event) event.stopPropagation();
    const fields: PdfField[] = [
      { label: 'ID', value: motorista.id.toString() },
      { label: 'Nome', value: motorista.nome },
      { label: 'Telefone', value: motorista.telefone },
      { label: 'Carta de Condução', value: motorista.cartaConducao },
      { label: 'ID Transportadora', value: motorista.transportadoraId?.toString() ?? '—' },
      { label: 'Estado', value: motorista.status ?? (motorista.ativo ? 'Activo' : 'Inactivo') },
      { label: 'Data de Registo', value: motorista.criadoEm ? new Date(motorista.criadoEm).toLocaleString() : '—' }
    ];
    const blob = this.pdfService.generateEntityPdf(`Motorista ${motorista.nome}`, fields);
    this.pdfService.downloadPdf(blob, `Motorista_${motorista.nome.replace(/\s+/g, '_')}.pdf`);
  }

  // Utils
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

  formatCartaConducao(): void {
    const control = this.form.get('cartaConducao');
    if (control?.value) {
      control.setValue(control.value.toUpperCase(), { emitEvent: false });
    }
  }
}