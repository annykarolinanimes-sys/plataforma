import {Component, OnInit, OnDestroy,inject, signal, computed} from '@angular/core';
import { CommonModule } from '@angular/common';
import {ReactiveFormsModule, FormBuilder, FormGroup,Validators, AbstractControl, FormArray, ValidatorFn} from '@angular/forms';
import { Subject, debounceTime, distinctUntilChanged, takeUntil } from 'rxjs';
import { PdfService, PdfField } from '../../core/services/pdf.service';
import {RecepcaoService, Recepcao, RecepcaoItem, PagedResult} from '../../core/services/recepcao.service';
import {FornecedoresCatalogoService, FornecedorModel} from '../../core/services/fornecedores-catalogo.service';
import {ProdutosService, ProdutoModel} from '../../core/services/produtos.service';
import { UiStateService } from '../../core/services/ui-state.service';

function observacoesObrigatorias(): ValidatorFn {
  return (control: AbstractControl) => {
    const group = control.parent as FormGroup | null;
    if (!group) return null;
    const rejeitada = +(group.get('quantidadeRejeitada')?.value ?? 0);
    const obs       = (control.value ?? '').trim();
    if (rejeitada > 0 && !obs) return { obsObrigatoria: true };
    return null;
  };
}

@Component({
  selector: 'app-recepcao',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './recepcao.component.html',
  styleUrls: ['./recepcao.component.css'],
})
export class RecepcaoComponent implements OnInit, OnDestroy {

  private readonly svc             = inject(RecepcaoService);
  private readonly fornecedoresSvc = inject(FornecedoresCatalogoService);
  private readonly produtosSvc     = inject(ProdutosService);
  private readonly fb              = inject(FormBuilder);
  private readonly pdfService      = inject(PdfService);
  readonly uiState                 = inject(UiStateService);
  private readonly destroy$        = new Subject<void>();

  currentState = this.uiState.currentRecepcaoState;
  editingId    = this.uiState.currentRecepcaoId;
  isViewing    = signal(false);

  isListView()   { return this.currentState() === 'list';   }
  isCreateView() { return this.currentState() === 'create'; }
  isEditView()   { return this.currentState() === 'edit';   }

  pagedResult  = signal<PagedResult<Recepcao> | null>(null);
  rececoes     = computed(() => this.pagedResult()?.items ?? []);
  fornecedores = signal<FornecedorModel[]>([]);
  produtos     = signal<ProdutoModel[]>([]);
  isLoading    = signal(false);
  isSaving     = signal(false);
  errorMsg     = signal<string | null>(null);
  successMsg   = signal<string | null>(null);

  // ── Stats-Grid (computed a partir dos dados carregados) ───────────────────
  totalRecepcoes  = computed(() => this.pagedResult()?.total ?? 0);
  countPendentes  = computed(() =>
    this.rececoes().filter(r => r.status === 'Pendente').length
  );
  countUrgentes   = computed(() =>
    this.rececoes().filter(r => r.prioridade === 'Alta').length
  );
  volumeHoje      = computed(() => {
    const hoje = new Date().toDateString();
    return this.rececoes()
      .filter(r => new Date(r.dataRecepcao).toDateString() === hoje)
      .reduce((acc, r) => acc + (r.totalUnidades ?? 0), 0);
  });
  eficiencia      = computed(() => {
    const items = this.rececoes().flatMap(r => r.itens ?? []);
    const total = items.reduce((a, i) => a + (i.quantidadeRecebida ?? 0), 0);
    const rejeitados = items.reduce((a, i) => a + (i.quantidadeRejeitada ?? 0), 0);
    if (total === 0) return 100;
    return Math.round(((total - rejeitados) / total) * 100);
  });

  // ── Totais reactivos do formulário ────────────────────────────────────────
  // Signal actualizado em tempo real enquanto o utilizador edita os itens
  totalEsperado  = signal(0);
  totalRecebido  = signal(0);
  totalRejeitado = signal(0);
  totalAceite    = computed(() => this.totalRecebido() - this.totalRejeitado());

  // ── Alerta de produto duplicado ───────────────────────────────────────────
  alertaDuplicado = signal<string | null>(null);

  // ── Filtros & paginação ───────────────────────────────────────────────────
  filtroStatus  = '';
  filtroSearch  = '';
  currentPage   = 1;
  readonly pageSize = 15;

  totalPages = computed(() => this.pagedResult()?.totalPages ?? 0);
  pages      = computed(() =>
    Array.from({ length: this.totalPages() }, (_, i) => i + 1)
  );

  // ── Modal de confirmação de cancelamento ──────────────────────────────────
  showDeleteConfirm  = signal(false);
  recepcaoParaDelete = signal<Recepcao | null>(null);

  readonly prioridades = ['Baixa', 'Media', 'Alta'] as const;
  readonly statusList  = ['Pendente', 'EmConferencia', 'Concluida', 'Cancelada'] as const;
  readonly tiposEntrada = [
    { value: 'Fornecedor',    label: 'Fornecedor' },
    { value: 'Transferencia', label: 'Transferência entre armazéns' },
    { value: 'Devolucao',     label: 'Devolução de cliente' },
  ] as const;

  form!: FormGroup;
  private readonly searchInput$ = new Subject<string>();

  // ── Ciclo de vida ─────────────────────────────────────────────────────────

  ngOnInit(): void {
    this.initForm();
    this.searchInput$
      .pipe(debounceTime(400), distinctUntilChanged(), takeUntil(this.destroy$))
      .subscribe(() => { this.currentPage = 1; this.carregarRecepcoes(); });

    this.carregarRecepcoes();
    this.carregarFornecedores();
    this.carregarProdutos();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  // ── Formulário ────────────────────────────────────────────────────────────

  private initForm(): void {
    this.form = this.fb.group({
      fornecedorId:        ['', Validators.required],
      tipoEntrada:         ['Fornecedor', Validators.required],
      prioridade:          ['Media', Validators.required],
      documentoReferencia: ['', Validators.maxLength(100)],
      itens:               this.fb.array([]),
    });
    this.adicionarItem();
    this._subscribeItensTotals();
  }

  get itensArray(): FormArray { return this.form.get('itens') as FormArray; }

  criarItemForm(item?: RecepcaoItem): FormGroup {
    const g = this.fb.group({
      produtoId:           [item?.produtoId          ?? '', Validators.required],
      quantidadeEsperada:  [item?.quantidadeEsperada  ?? 1, [Validators.required, Validators.min(1)]],
      quantidadeRecebida:  [item?.quantidadeRecebida  ?? 0, [Validators.required, Validators.min(0)]],
      quantidadeRejeitada: [item?.quantidadeRejeitada ?? 0, [Validators.required, Validators.min(0)]],
      lote:        [item?.lote        ?? '', Validators.maxLength(100)],
      validade:    [item?.validade    ?? ''],
      localizacao: [item?.localizacao ?? '', Validators.maxLength(50)],
      // observacoes: validação condicional — obrigatória se quantidadeRejeitada > 0
      observacoes: [item?.observacoes ?? '', [Validators.maxLength(500), observacoesObrigatorias()]],
    });

    // Revalida observacoes quando quantidadeRejeitada muda
    g.get('quantidadeRejeitada')?.valueChanges
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        g.get('observacoes')?.updateValueAndValidity();
        this._recalcularTotais();
      });

    g.get('quantidadeRecebida')?.valueChanges
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => this._recalcularTotais());

    g.get('quantidadeEsperada')?.valueChanges
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => this._recalcularTotais());

    return g;
  }

  adicionarItem(): void {
    this.itensArray.push(this.criarItemForm());
    this._recalcularTotais();
  }

  removerItem(index: number): void {
    this.itensArray.removeAt(index);
    if (this.itensArray.length === 0) this.adicionarItem();
    this.alertaDuplicado.set(null);
    this._recalcularTotais();
  }

  // FIX: Prevenção de produto duplicado no FormArray
  onProdutoChange(index: number, produtoId: string): void {
    if (!produtoId) { this.alertaDuplicado.set(null); return; }

    const ids = this.itensArray.controls
      .map((c, i) => i !== index ? String(c.get('produtoId')?.value ?? '') : null)
      .filter(Boolean);

    if (ids.includes(String(produtoId))) {
      const produto = this.produtos().find(p => p.id === +produtoId);
      this.alertaDuplicado.set(
        `O produto "${produto?.nome ?? produtoId}" já foi adicionado. Edite a linha existente.`
      );
      // Repõe o valor anterior para evitar o duplicado
      this.itensArray.at(index).get('produtoId')?.setValue('', { emitEvent: false });
    } else {
      this.alertaDuplicado.set(null);
    }
  }

  ctrl(name: string): AbstractControl { return this.form.get(name)!; }

  hasError(name: string, error?: string): boolean {
    const c = this.ctrl(name);
    if (!c.invalid || !c.touched) return false;
    return error ? c.hasError(error) : true;
  }

  itemCtrl(i: number, name: string): AbstractControl {
    return (this.itensArray.at(i) as FormGroup).get(name)!;
  }

  hasItemError(i: number, name: string, error?: string): boolean {
    const c = this.itemCtrl(i, name);
    if (!c.invalid || !c.touched) return false;
    return error ? c.hasError(error) : true;
  }

  // FIX: Recalcular totais reactivos em tempo real
  private _recalcularTotais(): void {
    let esp = 0, rec = 0, rej = 0;
    this.itensArray.controls.forEach(c => {
      esp += +(c.get('quantidadeEsperada')?.value  ?? 0);
      rec += +(c.get('quantidadeRecebida')?.value  ?? 0);
      rej += +(c.get('quantidadeRejeitada')?.value ?? 0);
    });
    this.totalEsperado.set(esp);
    this.totalRecebido.set(rec);
    this.totalRejeitado.set(rej);
  }

  private _subscribeItensTotals(): void {
    this.itensArray.valueChanges
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => this._recalcularTotais());
  }

  // ── Listagem & Paginação ──────────────────────────────────────────────────

  carregarRecepcoes(): void {
    this.isLoading.set(true);
    this.svc.listar({
      status:   this.filtroStatus || undefined,
      search:   this.filtroSearch || undefined,
      page:     this.currentPage,
      pageSize: this.pageSize,
    }).subscribe({
      next:  r   => { this.pagedResult.set(r); this.isLoading.set(false); },
      error: err => { this.errorMsg.set(err.message ?? 'Erro ao carregar recepções.'); this.isLoading.set(false); },
    });
  }

  carregarFornecedores(): void {
    this.fornecedoresSvc.listar({ ativo: true }).subscribe({
      next:  r   => this.fornecedores.set(r.items),
      error: err => console.error('Erro ao carregar fornecedores:', err),
    });
  }

  carregarProdutos(): void {
    this.produtosSvc.listar({ ativo: true }).subscribe({
      next:  r   => this.produtos.set(r.items),
      error: err => console.error('Erro ao carregar produtos:', err),
    });
  }

  onSearchChange(value: string): void {
    this.filtroSearch = value;
    this.searchInput$.next(value);
  }

  onStatusChange(value: string): void {
    this.filtroStatus = value;
    this.currentPage  = 1;
    this.carregarRecepcoes();
  }

  goToPage(page: number): void {
    if (page < 1 || page > this.totalPages()) return;
    this.currentPage = page;
    this.carregarRecepcoes();
  }

  // ── Navegação ─────────────────────────────────────────────────────────────

  goToCreate(): void {
    this.isViewing.set(false);
    this._resetForm();
    this.uiState.goToRecepcaoCreate();
  }

  goToEdit(recepcao: Recepcao, event?: Event): void {
    if (event) event.stopPropagation();
    this.isViewing.set(false);
    this._patchRecepcao(recepcao);
    this.uiState.goToRecepcaoEdit(recepcao.id);
  }

  goToDetails(recepcao: Recepcao, event?: Event): void {
    if (event) event.stopPropagation();
    this._patchRecepcao(recepcao);
    this.isViewing.set(true);
    this.uiState.goToRecepcaoEdit(recepcao.id);
  }

  goToList(): void {
    this.isViewing.set(false);
    this.uiState.goToRecepcaoList();
    this._resetForm();
    this.carregarRecepcoes();
  }

  cancel(): void { this.goToList(); }

  // ── PDF — Guia de Entrada ─────────────────────────────────────────────────

  imprimirPdf(recepcao: Recepcao, event?: Event): void {
    if (event) event.stopPropagation();
    const fields: PdfField[] = [
      { label: 'Nº Recepção',          value: recepcao.numeroRecepcao },
      { label: 'Fornecedor',           value: recepcao.fornecedor ?? '—' },
      { label: 'Tipo de Entrada',      value: recepcao.tipoEntrada ?? '—' },
      { label: 'Data',                 value: this.formatarData(recepcao.dataRecepcao) },
      { label: 'Status',               value: recepcao.status },
      { label: 'Prioridade',           value: recepcao.prioridade },
      { label: 'Doc. Referência',      value: recepcao.documentoReferencia ?? '—' },
      { label: 'Total Itens',          value: recepcao.totalItens ?? 0 },
      { label: 'Total Unidades',       value: recepcao.totalUnidades ?? 0 },
    ];
    // Adicionar linhas de itens
    (recepcao.itens ?? []).forEach((item, i) => {
      fields.push({ label: `Item ${i + 1} — ${item.sku ?? item.produtoNome ?? ''}`, value: '' });
      fields.push({ label: '  Qtd. Esperada',  value: item.quantidadeEsperada });
      fields.push({ label: '  Qtd. Recebida',  value: item.quantidadeRecebida });
      fields.push({ label: '  Qtd. Rejeitada', value: item.quantidadeRejeitada });
      if (item.lote)       fields.push({ label: '  Lote',       value: item.lote });
      if (item.localizacao) fields.push({ label: '  Localização', value: item.localizacao });
    });
    try {
      const blob = this.pdfService.generateEntityPdf(
        `Guia de Entrada — ${recepcao.numeroRecepcao}`,
        fields,
        'Documento gerado automaticamente pelo sistema Accusoft.'
      );
      this.pdfService.downloadPdf(blob, `Guia_Entrada_${recepcao.numeroRecepcao}.pdf`);
    } catch {
      this.errorMsg.set('Erro ao gerar guia de entrada.');
    }
  }

  // ── CRUD ──────────────────────────────────────────────────────────────────

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
      produtoId:           +item.produtoId,
      quantidadeEsperada:  +item.quantidadeEsperada,
      quantidadeRecebida:  +item.quantidadeRecebida,
      quantidadeRejeitada: +item.quantidadeRejeitada,
      lote:        item.lote?.trim()        || undefined,
      validade:    item.validade            || undefined,
      localizacao: item.localizacao?.trim() || undefined,
      observacoes: item.observacoes?.trim() || undefined,
    }));

    const dto = {
      fornecedorId:        +v.fornecedorId,
      tipoEntrada:         v.tipoEntrada,
      prioridade:          v.prioridade,
      documentoReferencia: v.documentoReferencia?.trim() || undefined,
      itens,
    };

    if (this.uiState.isRecepcaoEdit() && this.editingId()) {
      this.svc.atualizar(this.editingId()!, dto).subscribe({
        next:  () => this._onSaveSuccess('Recepção actualizada com sucesso.'),
        error: e  => this._onSaveError(e.message),
      });
    } else {
      this.svc.criar(dto).subscribe({
        next:  () => this._onSaveSuccess('Recepção criada com sucesso.'),
        error: e  => this._onSaveError(e.message),
      });
    }
  }

  concluirRecepcao(recepcao: Recepcao, event?: Event): void {
    if (event) event.stopPropagation();
    if (!confirm(`Concluir recepção ${recepcao.numeroRecepcao}?`)) return;
    this.svc.concluir(recepcao.id).subscribe({
      next:  () => { this.carregarRecepcoes(); this.showToast('Recepção concluída com sucesso.'); },
      error: e  => this.errorMsg.set(e.message),
    });
  }

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
      next:  () => { this.cancelarDelete(); this.carregarRecepcoes(); this.showToast('Recepção cancelada.'); },
      error: e  => { this.errorMsg.set(e.message); this.cancelarDelete(); },
    });
  }

  // ── Helpers ───────────────────────────────────────────────────────────────

  private _resetForm(): void {
    while (this.itensArray.length) this.itensArray.removeAt(0);
    this.form.reset({ fornecedorId: '', tipoEntrada: 'Fornecedor', prioridade: 'Media', documentoReferencia: '' });
    this.adicionarItem();
    this.errorMsg.set(null);
    this.alertaDuplicado.set(null);
    this._recalcularTotais();
  }

  private _patchRecepcao(recepcao: Recepcao): void {
    while (this.itensArray.length) this.itensArray.removeAt(0);
    this.form.patchValue({
      fornecedorId:        recepcao.fornecedorId,
      tipoEntrada:         recepcao.tipoEntrada  || 'Fornecedor',
      prioridade:          recepcao.prioridade,
      documentoReferencia: recepcao.documentoReferencia ?? '',
    });
    recepcao.itens.forEach(item => this.itensArray.push(this.criarItemForm(item)));
    if (this.itensArray.length === 0) this.adicionarItem();
    this.errorMsg.set(null);
    this.alertaDuplicado.set(null);
    this._recalcularTotais();
  }

  private _onSaveSuccess(msg: string): void {
    this.isSaving.set(false);
    this.goToList();
    this.showToast(msg);
  }

  private _onSaveError(msg: string): void {
    this.errorMsg.set(msg);
    this.isSaving.set(false);
  }

  showToast(msg: string): void {
    this.successMsg.set(msg);
    setTimeout(() => this.successMsg.set(null), 3500);
  }

  clearError(): void { this.errorMsg.set(null); }

  getNomeProduto(produtoId: string | number): string {
    const p = this.produtos().find(p => p.id === +produtoId);
    return p ? `${p.sku} — ${p.nome}` : String(produtoId);
  }

  getSkuProduto(produtoId: string | number): string {
    return this.produtos().find(p => p.id === +produtoId)?.sku ?? '—';
  }

  getStatusClass(status: string): string {
    const map: Record<string, string> = {
      Pendente:       'status-pendente',
      EmConferencia:  'status-conferencia',
      Concluida:      'status-concluida',
      Cancelada:      'status-cancelada',
    };
    return map[status] ?? 'status-pendente';
  }

  getStatusLabel(status: string): string {
    const map: Record<string, string> = {
      Pendente:       'Pendente',
      EmConferencia:  'Em Conferência',
      Concluida:      'Concluída',
      Cancelada:      'Cancelada',
    };
    return map[status] ?? status;
  }

  getPrioridadeClass(prioridade: string): string {
    const map: Record<string, string> = {
      Baixa: 'prioridade-baixa',
      Media: 'prioridade-media',
      Alta:  'prioridade-alta',
    };
    return map[prioridade] ?? 'prioridade-media';
  }

  formatarData(data: string): string {
    if (!data) return '—';
    return new Date(data).toLocaleDateString('pt-PT', {
      day: '2-digit', month: '2-digit', year: 'numeric',
      hour: '2-digit', minute: '2-digit',
    });
  }

  formatarDataSimples(data: string): string {
    if (!data) return '—';
    return new Date(data).toLocaleDateString('pt-PT');
  }
}
