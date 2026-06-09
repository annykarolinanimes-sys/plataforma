import { Component, OnInit, OnDestroy, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators, AbstractControl, FormArray } from '@angular/forms';
import { Subject, debounceTime, distinctUntilChanged, takeUntil, finalize } from 'rxjs';
import { GuiasService, Guia, PagedResult, GuiaCreateDto, GuiaUpdateDto } from '../../core/services/guias.service';
import { PdfService, PdfField } from '../../core/services/pdf.service';
import { UiStateService } from '../../core/services/ui-state.service';

interface ProdutoOption {
  id: number;
  sku: string;
  nome: string;
  pesoUnitario: number;
  volumeUnitario: number;
  stockAtual?: number;
}

interface ClienteOption {
  id: number;
  nome: string;
  contribuinte: string;
  morada: string;
  telefone: string;
}

interface TransportadoraOption {
  id: number;
  nome: string;
  nif: string;
}

interface AtribuicaoOption {
  id: number;
  numeroAtribuicao: string;
  clienteId: number;
  clienteNome: string;
  enderecoOrigem: string;
  enderecoDestino: string;
  itens?: any[];
}

@Component({
  selector: 'app-guias',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './guias.component.html',
  styleUrls: ['./guias.component.css']
})
export class GuiasComponent implements OnInit, OnDestroy {
  private readonly svc = inject(GuiasService);
  private readonly fb = inject(FormBuilder);
  private readonly pdfService = inject(PdfService);
  private readonly uiState = inject(UiStateService);
  private readonly destroy$ = new Subject<void>();
  private readonly searchInput$ = new Subject<string>();

  currentState = this.uiState.currentGuiaState;
  editingId = this.uiState.currentGuiaId;
  isLoading = signal(false);
  isSaving = signal(false);
  errorMsg = signal<string | null>(null);
  successMsg = signal<string | null>(null);
  showDeleteConfirm = signal(false);
  guiaParaDelete = signal<Guia | null>(null);

  pagedResult = signal<PagedResult<Guia> | null>(null);
  guias = computed(() => this.pagedResult()?.items ?? []);
  produtos = signal<ProdutoOption[]>([]);
  clientes = signal<ClienteOption[]>([]);
  transportadoras = signal<TransportadoraOption[]>([]);
  atribuicoes = signal<AtribuicaoOption[]>([]);

  // ── Atribuição search ──────────────────────────────────────────────────
  atribuicaoSearchTerm = signal('');
  showAtribuicaoDropdown = signal(false);
  filteredAtribuicoes = computed(() => {
    const term = this.atribuicaoSearchTerm().trim().toLowerCase();
    if (term.length < 2) return [] as AtribuicaoOption[];
    return this.atribuicoes()
      .filter(a =>
        a.numeroAtribuicao.toLowerCase().includes(term) ||
        a.clienteNome.toLowerCase().includes(term) ||
        (a.enderecoOrigem ?? '').toLowerCase().includes(term) ||
        (a.enderecoDestino ?? '').toLowerCase().includes(term)
      ).slice(0, 10);
  });

  // ── Cliente search ─────────────────────────────────────────────────────
  clienteSearchTerm = signal('');
  showClienteDropdown = signal(false);
  filteredClientes = computed(() => {
    const term = this.clienteSearchTerm().trim().toLowerCase();
    if (term.length < 2) return [] as ClienteOption[];
    return this.clientes()
      .filter(c =>
        c.nome.toLowerCase().includes(term) ||
        (c.contribuinte ?? '').toLowerCase().includes(term) ||
        (c.telefone ?? '').toLowerCase().includes(term)
      ).slice(0, 10);
  });

  // ── Transportadora search ──────────────────────────────────────────────
  transportadoraSearchTerm = signal('');
  showTransportadoraDropdown = signal(false);
  filteredTransportadoras = computed(() => {
    const term = this.transportadoraSearchTerm().trim().toLowerCase();
    if (term.length < 2) return [] as TransportadoraOption[];
    return this.transportadoras()
      .filter(t =>
        t.nome.toLowerCase().includes(term) ||
        (t.nif ?? '').toLowerCase().includes(term)
      ).slice(0, 10);
  });

  filtroTipo = signal('');
  filtroStatus = signal('');
  filtroSearch = signal('');
  currentPage = signal(1);
  readonly pageSize = 15;

  totalGuias = computed(() => this.pagedResult()?.total ?? 0);
  totalPages = computed(() => this.pagedResult()?.totalPages ?? 0);
  pages = computed(() => Array.from({ length: this.totalPages() }, (_, i) => i + 1));

  guiasPendentes = computed(() => this.guias().filter(g => g.status === 'Pendente').length);
  pesoTotalTransito = computed(() =>
    this.guias().filter(g => g.status !== 'Cancelada').reduce((t, g) => t + (g.pesoTotalKg || 0), 0)
  );
  volumeTotalTransito = computed(() =>
    this.guias().filter(g => g.status !== 'Cancelada').reduce((t, g) => t + (g.volumeTotalM3 || 0), 0)
  );
  proximaEntrega = computed(() => {
    const guiasComData = this.guias()
      .filter(g => g.dataPrevistaEntrega && g.status !== 'Cancelada')
      .sort((a, b) => new Date(a.dataPrevistaEntrega!).getTime() - new Date(b.dataPrevistaEntrega!).getTime());
    return guiasComData.length === 0 ? null : guiasComData[0];
  });

  totalPesoGeral = computed(() => {
    let total = 0;
    for (let i = 0; i < this.itensArray.length; i++) {
      const v = this.itensArray.at(i).get('pesoTotal')?.value;
      if (v && !isNaN(v)) total += +v;
    }
    return total;
  });

  totalVolumeGeral = computed(() => {
    let total = 0;
    for (let i = 0; i < this.itensArray.length; i++) {
      const v = this.itensArray.at(i).get('volumeTotal')?.value;
      if (v && !isNaN(v)) total += +v;
    }
    return total;
  });

  totalItensCount(): number {
    return this.itensArray.controls.filter(c => {
      const produtoId = c.get('produtoId')?.value;
      const quantidade = Number(c.get('quantidade')?.value || 0);
      return produtoId != null && produtoId !== '' && quantidade > 0;
    }).length;
  }

  form!: FormGroup;
  isEditMode = computed(() => this.currentState() === 'edit');

  readonly tipos = ['Transporte', 'Remessa', 'Entrega'];
  readonly statusList = ['Pendente', 'Impressa', 'Enviada', 'Cancelada'];

  ngOnInit(): void {
    this.initForm();
    this.setupSearchListener();
    this.setupAtribuicaoListener();
    this.carregarDadosAuxiliares();
    this.carregarGuias();
    if (this.itensArray.length === 0) this.adicionarItem();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private initForm(): void {
    this.form = this.fb.group({
      tipo: ['Transporte', Validators.required],
      atribuicaoId: [null],
      clienteId: [null, Validators.required],
      transportadoraId: [null, Validators.required],
      enderecoOrigem: ['', Validators.maxLength(300)],
      enderecoDestino: ['', Validators.maxLength(300)],
      dataPrevistaEntrega: [''],
      observacoes: ['', Validators.maxLength(500)],
      instrucoesEspeciais: ['', Validators.maxLength(500)],
      status: ['Pendente'],
      dataEntregaReal: [''],
      itens: this.fb.array([])
    });
  }

  private setupSearchListener(): void {
    this.searchInput$
      .pipe(debounceTime(400), distinctUntilChanged(), takeUntil(this.destroy$))
      .subscribe(() => { this.currentPage.set(1); this.carregarGuias(); });
  }

  private setupAtribuicaoListener(): void {
    this.form.get('atribuicaoId')?.valueChanges
      .pipe(takeUntil(this.destroy$))
      .subscribe(atribuicaoId => {
        const idNum = atribuicaoId ? Number(atribuicaoId) : null;
        if (idNum) {
          const atribuicao = this.atribuicoes().find(a => a.id === idNum);
          if (atribuicao) {
            this.form.patchValue({
              clienteId: atribuicao.clienteId,
              enderecoOrigem: atribuicao.enderecoOrigem,
              enderecoDestino: atribuicao.enderecoDestino
            });
            if (atribuicao.itens && atribuicao.itens.length > 0) {
              this.carregarItensDaAtribuicao(atribuicao.itens);
              this.successMsg.set(`Itens da atribuição ${atribuicao.numeroAtribuicao} carregados automaticamente.`);
              setTimeout(() => this.successMsg.set(null), 3000);
            }
          }
        }
      });
  }

  private carregarItensDaAtribuicao(itensAtribuicao: any[]): void {
    while (this.itensArray.length) this.itensArray.removeAt(0);
    for (const item of itensAtribuicao) {
      const produto = this.produtos().find(p => p.id === item.produtoId);
      if (produto) {
        const quantidade = item.quantidade || 1;
        const itemForm = this.fb.group({
          id: [null],
          produtoId: [produto.id, Validators.required],
          quantidade: [quantidade, [Validators.required, Validators.min(1)]],
          lote: [item.lote || ''],
          observacoes: [item.observacoes || ''],
          produtoSku: [{ value: produto.sku, disabled: true }],
          produtoNome: [{ value: produto.nome, disabled: true }],
          pesoUnitario: [{ value: produto.pesoUnitario, disabled: true }],
          volumeUnitario: [{ value: produto.volumeUnitario, disabled: true }],
          pesoTotal: [quantidade * produto.pesoUnitario],
          volumeTotal: [quantidade * produto.volumeUnitario]
        });
        this.setQuantidadeValidators(itemForm, produto.stockAtual ?? null);
        this.itensArray.push(itemForm);
      }
    }
    if (this.itensArray.length === 0) this.adicionarItem();
  }

  get itensArray(): FormArray { return this.form.get('itens') as FormArray; }

  criarItemForm(item?: any): FormGroup {
    return this.fb.group({
      id: [item?.id ?? null],
      produtoId: [item?.produtoId ?? null, Validators.required],
      quantidade: [item?.quantidade ?? 1, [Validators.required, Validators.min(1)]],
      lote: [item?.lote ?? '', Validators.maxLength(100)],
      observacoes: [item?.observacoes ?? '', Validators.maxLength(500)],
      produtoSku: [{ value: item?.produtoSku ?? '', disabled: true }],
      produtoNome: [{ value: item?.produtoNome ?? '', disabled: true }],
      pesoUnitario: [{ value: item?.pesoUnitario ?? 0, disabled: true }],
      volumeUnitario: [{ value: item?.volumeUnitario ?? 0, disabled: true }],
      pesoTotal: [item?.pesoTotal ?? 0],
      volumeTotal: [item?.volumeTotal ?? 0]
    });
  }

  adicionarItem(): void { this.itensArray.push(this.criarItemForm()); }

  removerItem(index: number): void {
    this.itensArray.removeAt(index);
    if (this.itensArray.length === 0) this.adicionarItem();
  }

  onProdutoChange(index: number): void {
    const itemForm = this.itensArray.at(index) as FormGroup;
    const produtoId = itemForm.get('produtoId')?.value;
    const produto = this.produtos().find(p => p.id == produtoId);
    if (produto) {
      const quantidade = itemForm.get('quantidade')?.value || 1;
      itemForm.patchValue({
        produtoSku: produto.sku,
        produtoNome: produto.nome,
        pesoUnitario: produto.pesoUnitario,
        volumeUnitario: produto.volumeUnitario,
        pesoTotal: quantidade * produto.pesoUnitario,
        volumeTotal: quantidade * produto.volumeUnitario
      });
      this.setQuantidadeValidators(itemForm, produto.stockAtual ?? null);
    } else {
      itemForm.patchValue({ produtoSku: '', produtoNome: '', pesoUnitario: 0, volumeUnitario: 0, pesoTotal: 0, volumeTotal: 0 });
      this.setQuantidadeValidators(itemForm, null);
    }
  }

  onQuantidadeChange(index: number): void {
    const itemForm = this.itensArray.at(index) as FormGroup;
    const quantidade = itemForm.get('quantidade')?.value || 0;
    const pesoUnitario = itemForm.get('pesoUnitario')?.value || 0;
    const volumeUnitario = itemForm.get('volumeUnitario')?.value || 0;
    itemForm.patchValue({ pesoTotal: quantidade * pesoUnitario, volumeTotal: quantidade * volumeUnitario });
    this.validateQuantidade(index);
  }

  private setQuantidadeValidators(itemForm: FormGroup, stock: number | null): void {
    const ctrl = itemForm.get('quantidade');
    if (!ctrl) return;
    const validators = [Validators.required, Validators.min(1)];
    if (stock !== null && !isNaN(stock)) validators.push(Validators.max(stock));
    ctrl.setValidators(validators);
    ctrl.updateValueAndValidity({ onlySelf: true });
  }

  private validateQuantidade(index: number): void {
    const itemForm = this.itensArray.at(index) as FormGroup;
    const ctrl = itemForm.get('quantidade');
    const produtoId = itemForm.get('produtoId')?.value;
    const produto = this.produtos().find(p => p.id == produtoId);
    const stock = produto?.stockAtual ?? null;
    if (!ctrl) return;
    if (stock !== null && !isNaN(stock)) {
      const quantidade = ctrl.value || 0;
      if (quantidade > stock) ctrl.setErrors({ max: { max: stock, actual: quantidade } });
      else if (ctrl.hasError('max')) ctrl.updateValueAndValidity({ onlySelf: true, emitEvent: false });
    }
  }

  getItemStockAtual(index: number): number | null {
    const produtoId = this.itensArray.at(index).get('produtoId')?.value;
    return this.produtos().find(p => p.id == produtoId)?.stockAtual ?? null;
  }

  private getProductStockAtual(produtoId: number | null): number | null {
    if (produtoId == null) return null;
    return this.produtos().find(p => p.id === produtoId)?.stockAtual ?? null;
  }

  // ── Atribuição search handlers ──────────────────────────────────────────
  onAtribuicaoSearch(value: string): void {
    this.atribuicaoSearchTerm.set(value || '');
    this.showAtribuicaoDropdown.set(
      this.atribuicaoSearchTerm().trim().length >= 2 && this.filteredAtribuicoes().length > 0
    );
    if (!value?.trim()) this.form.patchValue({ atribuicaoId: null });
  }

  onAtribuicaoFocus(): void {
    this.showAtribuicaoDropdown.set(this.filteredAtribuicoes().length > 0);
  }

  closeAtribuicaoDropdown(): void {
    setTimeout(() => this.showAtribuicaoDropdown.set(false), 120);
  }

  selecionarAtribuicao(a: AtribuicaoOption): void {
    this.form.patchValue({
      atribuicaoId: a.id,
      clienteId: a.clienteId,
      enderecoOrigem: a.enderecoOrigem,
      enderecoDestino: a.enderecoDestino
    });
    this.atribuicaoSearchTerm.set('');
    this.showAtribuicaoDropdown.set(false);
    if (a.itens && a.itens.length > 0) this.carregarItensDaAtribuicao(a.itens);
  }

  // ── Cliente search handlers ─────────────────────────────────────────────
  onClienteSearch(value: string): void {
    this.clienteSearchTerm.set(value || '');
    this.showClienteDropdown.set(
      this.clienteSearchTerm().trim().length >= 2 && this.filteredClientes().length > 0
    );
    if (!value?.trim()) this.form.patchValue({ clienteId: null });
  }

  onClienteFocus(): void {
    this.showClienteDropdown.set(this.filteredClientes().length > 0);
  }

  closeClienteDropdown(): void {
    setTimeout(() => this.showClienteDropdown.set(false), 120);
  }

  selecionarCliente(c: ClienteOption): void {
    this.form.patchValue({ clienteId: c.id });
    this.clienteSearchTerm.set('');
    this.showClienteDropdown.set(false);
  }

  // ── Transportadora search handlers ──────────────────────────────────────
  onTransportadoraSearch(value: string): void {
    this.transportadoraSearchTerm.set(value || '');
    this.showTransportadoraDropdown.set(
      this.transportadoraSearchTerm().trim().length >= 2 && this.filteredTransportadoras().length > 0
    );
    if (!value?.trim()) this.form.patchValue({ transportadoraId: null });
  }

  onTransportadoraFocus(): void {
    this.showTransportadoraDropdown.set(this.filteredTransportadoras().length > 0);
  }

  closeTransportadoraDropdown(): void {
    setTimeout(() => this.showTransportadoraDropdown.set(false), 120);
  }

  selecionarTransportadora(t: TransportadoraOption): void {
    this.form.patchValue({ transportadoraId: t.id });
    this.transportadoraSearchTerm.set('');
    this.showTransportadoraDropdown.set(false);
  }

  // ── Nome do cliente/transportadora/atribuição selecionados ──────────────
  getClienteSelecionadoNome(): string {
    const id = this.form.get('clienteId')?.value;
    return this.clientes().find(c => c.id == id)?.nome ?? '';
  }

  getTransportadoraSelecionadaNome(): string {
    const id = this.form.get('transportadoraId')?.value;
    return this.transportadoras().find(t => t.id == id)?.nome ?? '';
  }

  getAtribuicaoSelecionadaLabel(): string {
    const id = this.form.get('atribuicaoId')?.value;
    const a = this.atribuicoes().find(x => x.id == id);
    if (!a) return '';
    return `${a.numeroAtribuicao} — ${a.clienteNome}`;
  }

  onSearchChange(value: string): void { this.filtroSearch.set(value); this.searchInput$.next(value); }
  onTipoChange(value: string): void { this.filtroTipo.set(value); this.currentPage.set(1); this.carregarGuias(); }
  onStatusChange(value: string): void { this.filtroStatus.set(value); this.currentPage.set(1); this.carregarGuias(); }

  goToPage(page: number): void {
    if (page < 1 || page > this.totalPages()) return;
    this.currentPage.set(page);
    this.carregarGuias();
  }

  goToCreate(): void { this.resetForm(); this.uiState.goToGuiaCreate(); }

  goToEdit(guia: Guia, event?: Event): void {
    if (event) event.stopPropagation();
    this.carregarDadosParaEdicao(guia);
    this.uiState.goToGuiaEdit(guia.id);
  }

  goToList(): void { this.uiState.goToGuiaList(); this.resetForm(); this.carregarGuias(); }
  cancel(): void { this.goToList(); }
  clearError(): void { this.errorMsg.set(null); }

  private resetForm(): void {
    while (this.itensArray.length) this.itensArray.removeAt(0);
    this.form.reset({ tipo: 'Transporte', status: 'Pendente' });
    this.atribuicaoSearchTerm.set(''); this.showAtribuicaoDropdown.set(false);
    this.clienteSearchTerm.set(''); this.showClienteDropdown.set(false);
    this.transportadoraSearchTerm.set(''); this.showTransportadoraDropdown.set(false);
    this.adicionarItem();
    this.errorMsg.set(null);
  }

  private carregarDadosParaEdicao(guia: Guia): void {
    while (this.itensArray.length) this.itensArray.removeAt(0);
    this.form.patchValue({
      tipo: guia.tipo,
      atribuicaoId: guia.atribuicaoId,
      clienteId: guia.clienteId,
      transportadoraId: guia.transportadoraId,
      enderecoOrigem: guia.enderecoOrigem ?? '',
      enderecoDestino: guia.enderecoDestino ?? '',
      dataPrevistaEntrega: guia.dataPrevistaEntrega?.split('T')[0] ?? '',
      observacoes: guia.observacoes ?? '',
      instrucoesEspeciais: guia.instrucoesEspeciais ?? '',
      status: guia.status,
      dataEntregaReal: guia.dataEntregaReal?.split('T')[0] ?? ''
    });
    if (guia.itens && guia.itens.length > 0) {
      guia.itens.forEach(item => {
        const itemForm = this.criarItemForm(item);
        this.setQuantidadeValidators(itemForm, this.getProductStockAtual(item.produtoId));
        this.itensArray.push(itemForm);
      });
    }
    if (this.itensArray.length === 0) this.adicionarItem();
    this.atribuicaoSearchTerm.set(''); this.showAtribuicaoDropdown.set(false);
    this.clienteSearchTerm.set(''); this.showClienteDropdown.set(false);
    this.transportadoraSearchTerm.set(''); this.showTransportadoraDropdown.set(false);
    this.errorMsg.set(null);
  }

  carregarGuias(): void {
    this.isLoading.set(true);
    this.svc.listar({
      tipo: this.filtroTipo() || undefined,
      status: this.filtroStatus() || undefined,
      search: this.filtroSearch() || undefined,
      page: this.currentPage(),
      pageSize: this.pageSize
    }).pipe(finalize(() => this.isLoading.set(false))).subscribe({
      next: result => this.pagedResult.set(result),
      error: err => this.errorMsg.set(err.message ?? 'Erro ao carregar guias')
    });
  }

  carregarDadosAuxiliares(): void {
    this.svc.obterProdutos().subscribe({ next: d => this.produtos.set(d), error: () => this.produtos.set([]) });
    this.svc.obterClientes().subscribe({ next: d => this.clientes.set(d), error: () => this.clientes.set([]) });
    this.svc.obterTransportadoras().subscribe({ next: d => this.transportadoras.set(d), error: () => this.transportadoras.set([]) });
    this.svc.obterAtribuicoes().subscribe({ next: d => this.atribuicoes.set(d), error: () => this.atribuicoes.set([]) });
  }

  private prepararDto(): GuiaCreateDto {
    const raw = this.form.getRawValue();
    const itens = raw.itens
      .filter((item: any) => item.produtoId != null)
      .map((item: any) => ({
        ProdutoId: Number(item.produtoId),
        Quantidade: Number(item.quantidade),
        Lote: item.lote?.trim() || undefined,
        Observacoes: item.observacoes?.trim() || undefined
      }));
    return {
      Tipo: raw.tipo,
      AtribuicaoId: raw.atribuicaoId != null ? Number(raw.atribuicaoId) : undefined,
      ClienteId: raw.clienteId != null ? Number(raw.clienteId) : undefined,
      TransportadoraId: raw.transportadoraId != null ? Number(raw.transportadoraId) : undefined,
      EnderecoOrigem: raw.enderecoOrigem?.trim() || undefined,
      EnderecoDestino: raw.enderecoDestino?.trim() || undefined,
      DataPrevistaEntrega: raw.dataPrevistaEntrega || undefined,
      Observacoes: raw.observacoes?.trim() || undefined,
      InstrucoesEspeciais: raw.instrucoesEspeciais?.trim() || undefined,
      Itens: itens
    };
  }

  private prepararDtoUpdate(): GuiaUpdateDto {
    const raw = this.form.getRawValue();
    const itens = raw.itens
      .filter((item: any) => item.produtoId != null)
      .map((item: any) => ({
        Id: item.id ?? undefined,
        ProdutoId: item.produtoId != null ? Number(item.produtoId) : undefined,
        Quantidade: item.quantidade != null ? Number(item.quantidade) : undefined,
        Lote: item.lote?.trim() || undefined,
        Observacoes: item.observacoes?.trim() || undefined
      }));
    return {
      Status: raw.status,
      DataPrevistaEntrega: raw.dataPrevistaEntrega || undefined,
      DataEntregaReal: raw.dataEntregaReal || undefined,
      Observacoes: raw.observacoes?.trim() || undefined,
      InstrucoesEspeciais: raw.instrucoesEspeciais?.trim() || undefined,
      Itens: itens
    };
  }

  salvarGuia(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid) { this.errorMsg.set('Corrija os erros no formulário antes de continuar.'); return; }
    if (this.totalItensCount() === 0) { this.errorMsg.set('Adicione pelo menos um item à guia.'); return; }

    this.isSaving.set(true);
    this.errorMsg.set(null);

    if (this.isEditMode() && this.editingId()) {
      this.svc.atualizar(this.editingId()!, this.prepararDtoUpdate())
        .pipe(finalize(() => this.isSaving.set(false)))
        .subscribe({ next: () => this.onSaveSuccess('Guia atualizada com sucesso.'), error: err => this.onSaveError(err.message) });
    } else {
      this.svc.criar(this.prepararDto())
        .pipe(finalize(() => this.isSaving.set(false)))
        .subscribe({ next: () => this.onSaveSuccess('Guia criada com sucesso.'), error: err => this.onSaveError(err.message) });
    }
  }

  imprimirGuia(guia: Guia, event?: Event): void {
    if (event) event.stopPropagation();
    this.svc.imprimir(guia.id).subscribe({
      next: blob => {
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url; a.download = `Guia_${guia.numeroGuia}.pdf`; a.click();
        window.URL.revokeObjectURL(url);
        this.showToast('PDF gerado com sucesso!');
      },
      error: err => this.errorMsg.set(err.message)
    });
  }

  confirmarDelete(guia: Guia, event?: Event): void {
    if (event) event.stopPropagation();
    this.guiaParaDelete.set(guia);
    this.showDeleteConfirm.set(true);
  }

  cancelarDelete(): void { this.showDeleteConfirm.set(false); this.guiaParaDelete.set(null); }

  executarDelete(): void {
    const guia = this.guiaParaDelete();
    if (!guia) return;
    this.svc.deletar(guia.id).subscribe({
      next: () => { this.cancelarDelete(); this.carregarGuias(); this.showToast('Guia cancelada com sucesso.'); },
      error: err => { this.errorMsg.set(err.message); this.cancelarDelete(); }
    });
  }

  private onSaveSuccess(msg: string): void { this.goToList(); this.showToast(msg); }
  private onSaveError(msg: string): void { this.errorMsg.set(msg); }

  showToast(msg: string): void { this.successMsg.set(msg); setTimeout(() => this.successMsg.set(null), 3500); }

  hasError(fieldName: string, errorType?: string): boolean {
    const control = this.form.get(fieldName);
    if (!control) return false;
    if (errorType) return control.touched && control.hasError(errorType);
    return control.touched && control.invalid;
  }

  getStatusClass(status: string): string {
    const classes: Record<string, string> = {
      'Pendente': 'status-pendente', 'Impressa': 'status-impressa',
      'Enviada': 'status-enviada', 'Cancelada': 'status-cancelada'
    };
    return classes[status] || 'status-pendente';
  }

  formatarData(data: string): string {
    if (!data) return '—';
    return new Date(data).toLocaleDateString('pt-PT', { day: '2-digit', month: '2-digit', year: 'numeric' });
  }

  formatarPeso(peso: number): string {
    if (!peso) return '0 kg';
    return `${peso.toLocaleString('pt-PT', { minimumFractionDigits: 2, maximumFractionDigits: 2 })} kg`;
  }

  formatarVolume(volume: number): string {
    if (!volume) return '0 m³';
    return `${volume.toLocaleString('pt-PT')} m³`;
  }

  getItemPesoTotal(index: number): number { return this.itensArray.at(index).get('pesoTotal')?.value || 0; }
  getItemVolumeTotal(index: number): number { return this.itensArray.at(index).get('volumeTotal')?.value || 0; }
}