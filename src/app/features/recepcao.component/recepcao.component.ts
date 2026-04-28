import { Component, OnInit, OnDestroy, inject, signal, computed} from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators, AbstractControl, FormArray} from '@angular/forms';
import { Subject, debounceTime, distinctUntilChanged, takeUntil } from 'rxjs';
import { RecepcaoService, Recepcao, RecepcaoItem, PagedResult } from '../../core/services/recepcao.service';
import { UiStateService } from '../../core/services/ui-state.service';

@Component({
  selector: 'app-recepcao',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './recepcao.component.html',
  styleUrls: ['./recepcao.component.css']
})
export class RecepcaoComponent implements OnInit, OnDestroy {
  private readonly svc = inject(RecepcaoService);
  private readonly fb = inject(FormBuilder);
  private readonly uiState = inject(UiStateService);
  private readonly destroy$ = new Subject<void>();

  // Estado da UI
  currentState = this.uiState.currentRecepcaoState;
  editingId = this.uiState.currentRecepcaoId;

  // Dados
  pagedResult = signal<PagedResult<Recepcao> | null>(null);
  rececoes = computed(() => this.pagedResult()?.items ?? []);
  isLoading = signal(false);
  isSaving = signal(false);
  errorMsg = signal<string | null>(null);
  successMsg = signal<string | null>(null);

  // Filtros
  filtroStatus = '';
  filtroSearch = '';
  currentPage = 1;
  readonly pageSize = 15;

  // Formulário
  form!: FormGroup;

  // Modal apenas para ELIMINAR
  showDeleteConfirm = signal(false);
  recepcaoParaDelete = signal<Recepcao | null>(null);

  private readonly searchInput$ = new Subject<string>();

  // Prioridades disponíveis
  readonly prioridades = ['Baixa', 'Media', 'Alta'];
  readonly statusList = ['Pendente', 'EmConferencia', 'Concluida', 'Cancelada'];

  totalRecepcoes = computed(() => this.pagedResult()?.total ?? 0);
  totalPages = computed(() => this.pagedResult()?.totalPages ?? 0);
  pages = computed(() => Array.from({ length: this.totalPages() }, (_, i) => i + 1));

  ngOnInit(): void {
    this.initForm();

    this.searchInput$
      .pipe(debounceTime(400), distinctUntilChanged(), takeUntil(this.destroy$))
      .subscribe(() => {
        this.currentPage = 1;
        this.carregarRecepcoes();
      });

    this.carregarRecepcoes();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private initForm(): void {
    this.form = this.fb.group({
      fornecedor: ['', [Validators.required, Validators.maxLength(200)]],
      tipoEntrada: ['Fornecedor', Validators.required],
      prioridade: ['Media', Validators.required],
      documentoReferencia: ['', Validators.maxLength(100)],
      itens: this.fb.array([])
    });
    this.adicionarItem();
  }

  get itensArray(): FormArray {
    return this.form.get('itens') as FormArray;
  }

  criarItemForm(item?: RecepcaoItem): FormGroup {
    return this.fb.group({
      sku: [item?.sku ?? '', [Validators.required, Validators.maxLength(50)]],
      produtoNome: [item?.produtoNome ?? '', [Validators.required, Validators.maxLength(300)]],
      quantidadeEsperada: [item?.quantidadeEsperada ?? 1, [Validators.required, Validators.min(1)]],
      quantidadeRecebida: [item?.quantidadeRecebida ?? 0, [Validators.required, Validators.min(0)]],
      quantidadeRejeitada: [item?.quantidadeRejeitada ?? 0, [Validators.required, Validators.min(0)]],
      lote: [item?.lote ?? '', Validators.maxLength(100)],
      validade: [item?.validade ?? ''],
      localizacao: [item?.localizacao ?? '', Validators.maxLength(50)],
      observacoes: [item?.observacoes ?? '', Validators.maxLength(500)]
    });
  }

  adicionarItem(): void {
    this.itensArray.push(this.criarItemForm());
  }

  removerItem(index: number): void {
    this.itensArray.removeAt(index);
    if (this.itensArray.length === 0) {
      this.adicionarItem();
    }
  }

  ctrl(name: string): AbstractControl { return this.form.get(name)!; }

  hasError(name: string, error?: string): boolean {
    const c = this.ctrl(name);
    if (!c.invalid || !c.touched) return false;
    return error ? c.hasError(error) : true;
  }

  // ─── Listagem e Filtros ─────────────────────────────────────────────────

  carregarRecepcoes(): void {
    this.isLoading.set(true);
    this.svc.listar({
      status: this.filtroStatus || undefined,
      search: this.filtroSearch || undefined,
      page: this.currentPage,
      pageSize: this.pageSize,
    }).subscribe({
      next: (result) => {
        this.pagedResult.set(result);
        this.isLoading.set(false);
      },
      error: (err) => {
        this.errorMsg.set(err.message ?? 'Erro ao carregar recepções');
        this.isLoading.set(false);
      },
    });
  }

  onSearchChange(value: string): void {
    this.filtroSearch = value;
    this.searchInput$.next(value);
  }

  onStatusChange(value: string): void {
    this.filtroStatus = value;
    this.currentPage = 1;
    this.carregarRecepcoes();
  }

  toggleInativos(): void {
    // Para recepções, usamos o status "Cancelada" como inativo
    this.filtroStatus = this.filtroStatus === 'Cancelada' ? '' : 'Cancelada';
    this.currentPage = 1;
    this.carregarRecepcoes();
  }

  goToPage(page: number): void {
    const total = this.totalPages();
    if (page < 1 || page > total) return;
    this.currentPage = page;
    this.carregarRecepcoes();
  }

  // ─── Ações de navegação ─────────────────────────────────────────────────

  goToCreate(): void {
    this.resetForm();
    this.uiState.goToRecepcaoCreate();
  }

  goToEdit(recepcao: Recepcao, event?: Event): void {
    if (event) event.stopPropagation();
    this.carregarDadosParaEdicao(recepcao);
    this.uiState.goToRecepcaoEdit(recepcao.id);
  }

  goToList(): void {
    this.uiState.goToRecepcaoList();
    this.resetForm();
    this.carregarRecepcoes();
  }

  cancel(): void {
    this.goToList();
  }

  // ─── Formulário inline ──────────────────────────────────────────────────

  private resetForm(): void {
    while (this.itensArray.length) {
      this.itensArray.removeAt(0);
    }
    this.form.reset({
      fornecedor: '',
      prioridade: 'Media',
      documentoReferencia: '',
    });
    this.adicionarItem();
    this.errorMsg.set(null);
  }

  private carregarDadosParaEdicao(recepcao: Recepcao): void {
    while (this.itensArray.length) {
      this.itensArray.removeAt(0);
    }

    this.form.patchValue({
      fornecedor: recepcao.fornecedor,
      tipoEntrada: recepcao.tipoEntrada || 'Fornecedor',
      prioridade: recepcao.prioridade,
      documentoReferencia: recepcao.documentoReferencia ?? '',
    });

    recepcao.itens.forEach(item => {
      this.itensArray.push(this.criarItemForm(item));
    });

    if (this.itensArray.length === 0) {
      this.adicionarItem();
    }

    this.errorMsg.set(null);
  }

  salvarRecepcao(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid) {
      this.errorMsg.set('Corrija os erros no formulário antes de continuar.');
      return;
    }

    this.isSaving.set(true);
    this.errorMsg.set(null);
    const v = this.form.getRawValue();

    const itens = v.itens.map((item: any) => ({
      sku: item.sku.trim().toUpperCase(),
      produtoNome: item.produtoNome.trim(),
      quantidadeEsperada: +item.quantidadeEsperada,
      quantidadeRecebida: +item.quantidadeRecebida,
      quantidadeRejeitada: +item.quantidadeRejeitada,
      lote: item.lote?.trim() || undefined,
      validade: item.validade || undefined,
      localizacao: item.localizacao?.trim() || undefined,
      observacoes: item.observacoes?.trim() || undefined,
    }));

    const dto = {
      fornecedor: v.fornecedor.trim(),
      tipoEntrada: v.tipoEntrada,
      prioridade: v.prioridade,
      documentoReferencia: v.documentoReferencia?.trim() || undefined,
      itens: itens
    };

    if (this.uiState.isRecepcaoEdit() && this.editingId()) {
      this.svc.atualizar(this.editingId()!, dto).subscribe({
        next: () => this.onSaveSuccess('Recepção actualizada com sucesso.'),
        error: (err) => this.onSaveError(err.message)
      });
    } else {
      this.svc.criar(dto).subscribe({
        next: () => this.onSaveSuccess('Recepção criada com sucesso.'),
        error: (err) => this.onSaveError(err.message)
      });
    }
  }

  concluirRecepcao(recepcao: Recepcao, event?: Event): void {
    if (event) event.stopPropagation();
    if (!confirm(`Concluir recepção ${recepcao.numeroRecepcao}?`)) return;

    this.svc.concluir(recepcao.id).subscribe({
      next: () => {
        this.carregarRecepcoes();
        this.showToast('Recepção concluída com sucesso.');
      },
      error: (err) => this.errorMsg.set(err.message)
    });
  }

  // ─── Modal apenas para ELIMINAR (exceção) ───────────────────────────────

  confirmarDelete(recepcao: Recepcao, event?: Event): void {
    if (event) event.stopPropagation();
    this.recepcaoParaDelete.set(recepcao);
    this.showDeleteConfirm.set(true);
  }

  cancelarDelete(): void {
    this.showDeleteConfirm.set(false);
    this.recepcaoParaDelete.set(null);
  }

  executarDelete(): void {
    const r = this.recepcaoParaDelete();
    if (!r) return;
    this.svc.deletar(r.id).subscribe({
      next: () => {
        this.cancelarDelete();
        this.carregarRecepcoes();
        this.showToast('Recepção cancelada com sucesso.');
      },
      error: (err) => { this.errorMsg.set(err.message); this.cancelarDelete(); }
    });
  }

  private onSaveSuccess(msg: string): void {
    this.isSaving.set(false);
    this.goToList();
    this.showToast(msg);
  }

  private onSaveError(msg: string): void {
    this.errorMsg.set(msg);
    this.isSaving.set(false);
  }

  showToast(msg: string): void {
    this.successMsg.set(msg);
    setTimeout(() => this.successMsg.set(null), 3500);
  }

  clearError(): void { this.errorMsg.set(null); }

  // ─── Helpers ────────────────────────────────────────────────────────────

  getStatusClass(status: string): string {
    const classes: Record<string, string> = {
      'Pendente': 'status-pendente',
      'EmConferencia': 'status-conferencia',
      'Concluida': 'status-concluida',
      'Cancelada': 'status-cancelada'
    };
    return classes[status] || 'status-pendente';
  }

  getPrioridadeClass(prioridade: string): string {
    const classes: Record<string, string> = {
      'Baixa': 'prioridade-baixa',
      'Media': 'prioridade-media',
      'Alta': 'prioridade-alta'
    };
    return classes[prioridade] || 'prioridade-media';
  }

  formatarData(data: string): string {
    if (!data) return '—';
    return new Date(data).toLocaleDateString('pt-PT', {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit'
    });
  }

  formatarDataSimples(data: string): string {
    if (!data) return '—';
    return new Date(data).toLocaleDateString('pt-PT');
  }
}