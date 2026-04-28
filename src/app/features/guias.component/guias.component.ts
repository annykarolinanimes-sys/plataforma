import { Component, OnInit, OnDestroy, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators, AbstractControl, FormArray } from '@angular/forms';
import { Subject, debounceTime, distinctUntilChanged, takeUntil, map } from 'rxjs';
import { GuiasService, Guia, PagedResult, GuiaCreateDto, GuiaUpdateDto, GuiaItem } from '../../core/services/guias.service';
import { UiStateService } from '../../core/services/ui-state.service';

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
  private readonly uiState = inject(UiStateService);
  private readonly destroy$ = new Subject<void>();

  // Estado da UI
  currentState = this.uiState.currentGuiaState;
  editingId = this.uiState.currentGuiaId;

  // Dados
  pagedResult = signal<PagedResult<Guia> | null>(null);
  guias = computed(() => this.pagedResult()?.items ?? []);
  isLoading = signal(false);
  isSaving = signal(false);
  errorMsg = signal<string | null>(null);
  successMsg = signal<string | null>(null);

  // Filtros
  filtroTipo = '';
  filtroStatus = '';
  filtroSearch = '';
  currentPage = 1;
  readonly pageSize = 15;

  // Formulário
  form!: FormGroup;

  // Listas para selects
  produtos = signal<{ id: number; sku: string; nome: string; pesoUnitario: number; volumeUnitario: number }[]>([]);
  clientes = signal<{ id: number; nome: string; contribuinte: string; morada: string; telefone: string }[]>([]);
  transportadoras = signal<{ id: number; nome: string; nif: string }[]>([]);
  atribuicoes = signal<{ id: number; numeroAtribuicao: string; clienteNome: string; enderecoOrigem: string; enderecoDestino: string }[]>([]);

  // Modal apenas para ELIMINAR
  showDeleteConfirm = signal(false);
  guiaParaDelete = signal<Guia | null>(null);

  private readonly searchInput$ = new Subject<string>();

  readonly tipos = ['Transporte', 'Remessa', 'Entrega'];
  readonly statusList = ['Pendente', 'Impressa', 'Enviada', 'Cancelada'];

  totalGuias = computed(() => this.pagedResult()?.total ?? 0);
  totalPages = computed(() => this.pagedResult()?.totalPages ?? 0);
  pages = computed(() => Array.from({ length: this.totalPages() }, (_, i) => i + 1));

  ngOnInit(): void {
    this.initForm();
    this.carregarDadosAuxiliares();

    this.searchInput$
      .pipe(debounceTime(400), distinctUntilChanged(), takeUntil(this.destroy$))
      .subscribe(() => {
        this.currentPage = 1;
        this.carregarGuias();
      });

    this.carregarGuias();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private initForm(): void {
    this.form = this.fb.group({
      tipo: ['Transporte', Validators.required],
      atribuicaoId: [null],
      clienteId: [null],
      transportadoraId: [null],
      enderecoOrigem: ['', Validators.maxLength(300)],
      enderecoDestino: ['', Validators.maxLength(300)],
      dataPrevistaEntrega: [''],
      observacoes: ['', Validators.maxLength(500)],
      instrucoesEspeciais: ['', Validators.maxLength(500)],
      itens: this.fb.array([])
    });

    // Quando selecionar uma atribuição, preencher dados automaticamente
    this.form.get('atribuicaoId')?.valueChanges.subscribe(atribuicaoId => {
      if (atribuicaoId) {
        const atribuicao = this.atribuicoes().find(a => a.id === atribuicaoId);
        if (atribuicao) {
          this.form.patchValue({
            clienteId: null, // Será preenchido pelo cliente da atribuição
            enderecoOrigem: atribuicao.enderecoOrigem,
            enderecoDestino: atribuicao.enderecoDestino
          });
        }
      }
    });

    // Quando selecionar um cliente, preencher morada e contacto
    this.form.get('clienteId')?.valueChanges.subscribe(clienteId => {
      if (clienteId) {
        const cliente = this.clientes().find(c => c.id === clienteId);
        if (cliente) {
          // Opcional: preencher automaticamente
        }
      }
    });
  }

  get itensArray(): FormArray {
    return this.form.get('itens') as FormArray;
  }

  criarItemForm(item?: GuiaItem): FormGroup {
    return this.fb.group({
      produtoId: [item?.produtoId ?? null, Validators.required],
      quantidade: [item?.quantidade ?? 1, [Validators.required, Validators.min(1)]],
      lote: [item?.lote ?? '', Validators.maxLength(100)],
      observacoes: [item?.observacoes ?? '', Validators.maxLength(500)],
      // Campos calculados
      produtoSku: [{ value: item?.produtoSku ?? '', disabled: true }],
      produtoNome: [{ value: item?.produtoNome ?? '', disabled: true }],
      pesoUnitario: [{ value: item?.pesoUnitario ?? 0, disabled: true }],
      volumeUnitario: [{ value: item?.volumeUnitario ?? 0, disabled: true }],
      pesoTotal: [{ value: item?.pesoTotal ?? 0, disabled: true }],
      volumeTotal: [{ value: item?.volumeTotal ?? 0, disabled: true }]
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

  onProdutoChange(index: number): void {
    const itemForm = this.itensArray.at(index) as FormGroup;
    const produtoId = itemForm.get('produtoId')?.value;
    const produto = this.produtos().find(p => p.id === produtoId);
    
    if (produto) {
      itemForm.patchValue({
        produtoSku: produto.sku,
        produtoNome: produto.nome,
        pesoUnitario: produto.pesoUnitario,
        volumeUnitario: produto.volumeUnitario
      });
      this.calcularTotaisItem(index);
    }
  }

  calcularTotaisItem(index: number): void {
    const itemForm = this.itensArray.at(index) as FormGroup;
    const quantidade = itemForm.get('quantidade')?.value || 0;
    const pesoUnitario = itemForm.get('pesoUnitario')?.value || 0;
    const volumeUnitario = itemForm.get('volumeUnitario')?.value || 0;
    
    itemForm.patchValue({
      pesoTotal: quantidade * pesoUnitario,
      volumeTotal: quantidade * volumeUnitario
    });
    
    this.calcularTotaisGerais();
  }

  calcularTotaisGerais(): void {
    let totalItens = 0;
    let pesoTotal = 0;
    let volumeTotal = 0;
    let totalVolumes = 0;
    
    for (let i = 0; i < this.itensArray.length; i++) {
      const item = this.itensArray.at(i).value;
      if (item.produtoId) {
        totalItens++;
        pesoTotal += (item.pesoTotal || 0);
        volumeTotal += (item.volumeTotal || 0);
        totalVolumes += (item.quantidade || 0);
      }
    }
    
    // Os totais serão salvos no backend
  }

  ctrl(name: string): AbstractControl { return this.form.get(name)!; }

  hasError(name: string, error?: string): boolean {
    const c = this.ctrl(name);
    if (!c.invalid || !c.touched) return false;
    return error ? c.hasError(error) : true;
  }

  carregarGuias(): void {
    this.isLoading.set(true);
    this.svc.listar({
      tipo: this.filtroTipo || undefined,
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
        this.errorMsg.set(err.message ?? 'Erro ao carregar guias');
        this.isLoading.set(false);
      },
    });
  }

  carregarDadosAuxiliares(): void {
    this.svc.obterProdutos().subscribe({
      next: (data) => this.produtos.set(data),
      error: () => this.produtos.set([])
    });
    
    this.svc.obterClientes().subscribe({
      next: (data) => this.clientes.set(data),
      error: () => this.clientes.set([])
    });
    
    this.svc.obterTransportadoras().subscribe({
      next: (data) => this.transportadoras.set(data),
      error: () => this.transportadoras.set([])
    });
    
    this.svc.obterAtribuicoes().subscribe({
      next: (data) => this.atribuicoes.set(data),
      error: () => this.atribuicoes.set([])
    });
  }

  onSearchChange(value: string): void {
    this.filtroSearch = value;
    this.searchInput$.next(value);
  }

  onTipoChange(value: string): void {
    this.filtroTipo = value;
    this.currentPage = 1;
    this.carregarGuias();
  }

  onStatusChange(value: string): void {
    this.filtroStatus = value;
    this.currentPage = 1;
    this.carregarGuias();
  }

  goToPage(page: number): void {
    const total = this.totalPages();
    if (page < 1 || page > total) return;
    this.currentPage = page;
    this.carregarGuias();
  }

  goToCreate(): void {
    this.resetForm();
    this.uiState.goToGuiaCreate();
  }

  goToEdit(guia: Guia, event?: Event): void {
    if (event) event.stopPropagation();
    this.carregarDadosParaEdicao(guia);
    this.uiState.goToGuiaEdit(guia.id);
  }

  goToList(): void {
    this.uiState.goToGuiaList();
    this.resetForm();
    this.carregarGuias();
  }

  cancel(): void {
    this.goToList();
  }

  private resetForm(): void {
    while (this.itensArray.length) {
      this.itensArray.removeAt(0);
    }
    this.form.reset({
      tipo: 'Transporte'
    });
    this.adicionarItem();
    this.errorMsg.set(null);
  }

  private carregarDadosParaEdicao(guia: Guia): void {
    while (this.itensArray.length) {
      this.itensArray.removeAt(0);
    }

    this.form.patchValue({
      tipo: guia.tipo,
      atribuicaoId: guia.atribuicaoId,
      clienteId: guia.clienteId,
      transportadoraId: guia.transportadoraId,
      enderecoOrigem: guia.enderecoOrigem ?? '',
      enderecoDestino: guia.enderecoDestino ?? '',
      dataPrevistaEntrega: guia.dataPrevistaEntrega?.split('T')[0] ?? '',
      observacoes: guia.observacoes ?? '',
      instrucoesEspeciais: guia.instrucoesEspeciais ?? ''
    });

    if (guia.itens && guia.itens.length > 0) {
      guia.itens.forEach(item => {
        this.itensArray.push(this.criarItemForm(item));
      });
    }

    if (this.itensArray.length === 0) {
      this.adicionarItem();
    }

    this.errorMsg.set(null);
  }

  salvarGuia(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid) {
      this.errorMsg.set('Corrija os erros no formulário antes de continuar.');
      return;
    }

    this.isSaving.set(true);
    this.errorMsg.set(null);
    const v = this.form.getRawValue();

    const itens = v.itens
      .filter((item: any) => item.produtoId)
      .map((item: any) => ({
        produtoId: item.produtoId,
        quantidade: item.quantidade,
        lote: item.lote?.trim() || undefined,
        observacoes: item.observacoes?.trim() || undefined
      }));

    if (itens.length === 0) {
      this.errorMsg.set('Adicione pelo menos um item à guia.');
      this.isSaving.set(false);
      return;
    }

    const dto: GuiaCreateDto = {
      tipo: v.tipo,
      atribuicaoId: v.atribuicaoId || undefined,
      clienteId: v.clienteId || undefined,
      transportadoraId: v.transportadoraId || undefined,
      enderecoOrigem: v.enderecoOrigem?.trim() || undefined,
      enderecoDestino: v.enderecoDestino?.trim() || undefined,
      dataPrevistaEntrega: v.dataPrevistaEntrega || undefined,
      observacoes: v.observacoes?.trim() || undefined,
      instrucoesEspeciais: v.instrucoesEspeciais?.trim() || undefined,
      itens: itens
    };

    if (this.uiState.isGuiaEdit() && this.editingId()) {
      this.svc.atualizar(this.editingId()!, dto).subscribe({
        next: () => this.onSaveSuccess('Guia actualizada com sucesso.'),
        error: (err) => this.onSaveError(err.message)
      });
    } else {
      this.svc.criar(dto).subscribe({
        next: () => this.onSaveSuccess('Guia criada com sucesso.'),
        error: (err) => this.onSaveError(err.message)
      });
    }
  }

  imprimirGuia(guia: Guia, event?: Event): void {
    if (event) event.stopPropagation();
    this.svc.imprimir(guia.id).subscribe({
      next: (blob) => {
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `Guia_${guia.numeroGuia}.pdf`;
        a.click();
        window.URL.revokeObjectURL(url);
      },
      error: (err) => this.errorMsg.set(err.message)
    });
  }

  confirmarDelete(guia: Guia, event?: Event): void {
    if (event) event.stopPropagation();
    this.guiaParaDelete.set(guia);
    this.showDeleteConfirm.set(true);
  }

  cancelarDelete(): void {
    this.showDeleteConfirm.set(false);
    this.guiaParaDelete.set(null);
  }

  executarDelete(): void {
    const g = this.guiaParaDelete();
    if (!g) return;
    this.svc.deletar(g.id).subscribe({
      next: () => {
        this.cancelarDelete();
        this.carregarGuias();
        this.showToast('Guia cancelada com sucesso.');
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

  getStatusClass(status: string): string {
    const classes: Record<string, string> = {
      'Pendente': 'status-pendente',
      'Impressa': 'status-impressa',
      'Enviada': 'status-enviada',
      'Cancelada': 'status-cancelada'
    };
    return classes[status] || 'status-pendente';
  }

  formatarData(data: string): string {
    if (!data) return '—';
    return new Date(data).toLocaleDateString('pt-PT');
  }

  formatarPeso(peso: number): string {
    return `${peso.toLocaleString('pt-PT')} kg`;
  }

  formatarVolume(volume: number): string {
    return `${volume.toLocaleString('pt-PT')} m³`;
  }

  calcularPesoTotal(): number {
  let total = 0;
  for (let i = 0; i < this.itensArray.length; i++) {
    total += this.itensArray.at(i).get('pesoTotal')?.value || 0;
  }
  return total;
}

  calcularVolumeTotal(): number {
    let total = 0;
    for (let i = 0; i < this.itensArray.length; i++) {
      total += this.itensArray.at(i).get('volumeTotal')?.value || 0;
    }
    return total;
  }

  getProdutoNome(produtoId: number): string {
    const produto = this.produtos().find(p => p.id === produtoId);
    return produto?.nome || '';
  }
}