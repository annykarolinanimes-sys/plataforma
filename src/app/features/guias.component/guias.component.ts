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

  // ─── UI State Signals ─────────────────────────────────────────────────────
  currentState = this.uiState.currentGuiaState;
  editingId = this.uiState.currentGuiaId;
  isLoading = signal(false);
  isSaving = signal(false);
  errorMsg = signal<string | null>(null);
  successMsg = signal<string | null>(null);
  showDeleteConfirm = signal(false);
  guiaParaDelete = signal<Guia | null>(null);

  // ─── Dados Signals ────────────────────────────────────────────────────────
  pagedResult = signal<PagedResult<Guia> | null>(null);
  guias = computed(() => this.pagedResult()?.items ?? []);
  produtos = signal<ProdutoOption[]>([]);
  clientes = signal<ClienteOption[]>([]);
  transportadoras = signal<TransportadoraOption[]>([]);
  atribuicoes = signal<AtribuicaoOption[]>([]);

  // ─── Filtros & Paginação ──────────────────────────────────────────────────
  filtroTipo = signal('');
  filtroStatus = signal('');
  filtroSearch = signal('');
  currentPage = signal(1);
  readonly pageSize = 15;

  // ─── Computed Signals para Dashboard (Stats Grid) ────────────────────────
  totalGuias = computed(() => this.pagedResult()?.total ?? 0);
  totalPages = computed(() => this.pagedResult()?.totalPages ?? 0);
  pages = computed(() => Array.from({ length: this.totalPages() }, (_, i) => i + 1));

  guiasPendentes = computed(() => 
    this.guias().filter(g => g.status === 'Pendente').length
  );

  pesoTotalTransito = computed(() => 
    this.guias()
      .filter(g => g.status !== 'Cancelada')
      .reduce((total, g) => total + (g.pesoTotalKg || 0), 0)
  );

  volumeTotalTransito = computed(() => 
    this.guias()
      .filter(g => g.status !== 'Cancelada')
      .reduce((total, g) => total + (g.volumeTotalM3 || 0), 0)
  );

  proximaEntrega = computed(() => {
    const guiasComData = this.guias()
      .filter(g => g.dataPrevistaEntrega && g.status !== 'Cancelada')
      .sort((a, b) => new Date(a.dataPrevistaEntrega!).getTime() - new Date(b.dataPrevistaEntrega!).getTime());
    
    if (guiasComData.length === 0) return null;
    return guiasComData[0];
  });

  // ─── Computed Signals para Totais do Formulário (em tempo real) ───────────
  totalPesoGeral = computed(() => {
    let total = 0;
    for (let i = 0; i < this.itensArray.length; i++) {
      const pesoTotal = this.itensArray.at(i).get('pesoTotal')?.value;
      if (pesoTotal && !isNaN(pesoTotal)) {
        total += pesoTotal;
      }
    }
    return total;
  });

  totalVolumeGeral = computed(() => {
    let total = 0;
    for (let i = 0; i < this.itensArray.length; i++) {
      const volumeTotal = this.itensArray.at(i).get('volumeTotal')?.value;
      if (volumeTotal && !isNaN(volumeTotal)) {
        total += volumeTotal;
      }
    }
    return total;
  });

  totalItensCount = computed(() => {
    return this.itensArray.controls.filter(c => c.get('produtoId')?.value).length;
  });

  // ─── Formulário ───────────────────────────────────────────────────────────
  form!: FormGroup;
  isEditMode = computed(() => this.currentState() === 'edit');

  // ─── Opções estáticas ─────────────────────────────────────────────────────
  readonly tipos = ['Transporte', 'Remessa', 'Entrega'];
  readonly statusList = ['Pendente', 'Impressa', 'Enviada', 'Cancelada'];

  // ─── Lifecycle ────────────────────────────────────────────────────────────
  ngOnInit(): void {
    this.initForm();
    this.setupSearchListener();
    this.setupAtribuicaoListener();
    this.carregarDadosAuxiliares();
    this.carregarGuias();
    
    if (this.itensArray.length === 0) {
      this.adicionarItem();
    }
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  // ─── Inicialização do Formulário ──────────────────────────────────────────
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
      .subscribe(() => {
        this.currentPage.set(1);
        this.carregarGuias();
      });
  }

  // ─── AUTOFILL POR ATRIBUIÇÃO (INTELIGENTE) ─────────────────────────────────
  private setupAtribuicaoListener(): void {
    this.form.get('atribuicaoId')?.valueChanges
      .pipe(takeUntil(this.destroy$))
      .subscribe(atribuicaoId => {
        if (atribuicaoId) {
          const atribuicao = this.atribuicoes().find(a => a.id === atribuicaoId);
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
    while (this.itensArray.length) {
      this.itensArray.removeAt(0);
    }
    
    for (const item of itensAtribuicao) {
      const produto = this.produtos().find(p => p.id === item.produtoId);
      if (produto) {
        const quantidade = item.quantidade || 1;
        const pesoTotal = quantidade * produto.pesoUnitario;
        const volumeTotal = quantidade * produto.volumeUnitario;
        
        this.itensArray.push(this.fb.group({
          id: [null],
          produtoId: [produto.id, Validators.required],
          quantidade: [quantidade, [Validators.required, Validators.min(1)]],
          lote: [item.lote || ''],
          observacoes: [item.observacoes || ''],
          produtoSku: [{ value: produto.sku, disabled: true }],
          produtoNome: [{ value: produto.nome, disabled: true }],
          pesoUnitario: [{ value: produto.pesoUnitario, disabled: true }],
          volumeUnitario: [{ value: produto.volumeUnitario, disabled: true }],
          pesoTotal: [{ value: pesoTotal, disabled: true }],
          volumeTotal: [{ value: volumeTotal, disabled: true }]
        }));
      }
    }
    
    if (this.itensArray.length === 0) {
      this.adicionarItem();
    }
  }

  // ─── FormArray Helpers ────────────────────────────────────────────────────
  get itensArray(): FormArray {
    return this.form.get('itens') as FormArray;
  }

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

  // ─── Cálculos Automáticos (Frontend) ──────────────────────────────────────
  onProdutoChange(index: number): void {
    const itemForm = this.itensArray.at(index) as FormGroup;
    const produtoId = itemForm.get('produtoId')?.value;
    const produto = this.produtos().find(p => p.id === produtoId);
    
    if (produto) {
      const quantidade = itemForm.get('quantidade')?.value || 1;
      const pesoTotal = quantidade * produto.pesoUnitario;
      const volumeTotal = quantidade * produto.volumeUnitario;
      
      itemForm.patchValue({
        produtoSku: produto.sku,
        produtoNome: produto.nome,
        pesoUnitario: produto.pesoUnitario,
        volumeUnitario: produto.volumeUnitario,
        pesoTotal: pesoTotal,
        volumeTotal: volumeTotal
      });
    } else {
      itemForm.patchValue({
        produtoSku: '',
        produtoNome: '',
        pesoUnitario: 0,
        volumeUnitario: 0,
        pesoTotal: 0,
        volumeTotal: 0
      });
    }
  }

  onQuantidadeChange(index: number): void {
    const itemForm = this.itensArray.at(index) as FormGroup;
    const quantidade = itemForm.get('quantidade')?.value || 0;
    const pesoUnitario = itemForm.get('pesoUnitario')?.value || 0;
    const volumeUnitario = itemForm.get('volumeUnitario')?.value || 0;
    
    const pesoTotal = quantidade * pesoUnitario;
    const volumeTotal = quantidade * volumeUnitario;
    
    itemForm.patchValue({
      pesoTotal: pesoTotal,
      volumeTotal: volumeTotal
    });
  }

  // ─── Preparação do DTO para envio ao Backend (PascalCase) ─────────────────
  private prepararDto(): GuiaCreateDto {
    const raw = this.form.getRawValue();
    
    const itens = raw.itens
      .filter((item: any) => item.produtoId)
      .map((item: any) => ({
        ProdutoId: item.produtoId,
        Quantidade: item.quantidade,
        Lote: item.lote?.trim() || undefined,
        Observacoes: item.observacoes?.trim() || undefined
      }));

    return {
      Tipo: raw.tipo,
      AtribuicaoId: raw.atribuicaoId || undefined,
      ClienteId: raw.clienteId || undefined,
      TransportadoraId: raw.transportadoraId || undefined,
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
      .filter((item: any) => item.produtoId)
      .map((item: any) => ({
        Id: item.id || undefined,
        ProdutoId: item.produtoId,
        Quantidade: item.quantidade,
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

  // ─── Carregamento de Dados ────────────────────────────────────────────────
  carregarGuias(): void {
    this.isLoading.set(true);
    this.svc.listar({
      tipo: this.filtroTipo() || undefined,
      status: this.filtroStatus() || undefined,
      search: this.filtroSearch() || undefined,
      page: this.currentPage(),
      pageSize: this.pageSize
    }).pipe(
      finalize(() => this.isLoading.set(false))
    ).subscribe({
      next: (result) => {
        this.pagedResult.set(result);
      },
      error: (err) => {
        this.errorMsg.set(err.message ?? 'Erro ao carregar guias');
      }
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

  // ─── Filtros e Paginação ──────────────────────────────────────────────────
  onSearchChange(value: string): void {
    this.filtroSearch.set(value);
    this.searchInput$.next(value);
  }

  onTipoChange(value: string): void {
    this.filtroTipo.set(value);
    this.currentPage.set(1);
    this.carregarGuias();
  }

  onStatusChange(value: string): void {
    this.filtroStatus.set(value);
    this.currentPage.set(1);
    this.carregarGuias();
  }

  goToPage(page: number): void {
    if (page < 1 || page > this.totalPages()) return;
    this.currentPage.set(page);
    this.carregarGuias();
  }

  // ─── Navegação ────────────────────────────────────────────────────────────
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

  clearError(): void {
    this.errorMsg.set(null);
  }

  // ─── Formulário - Reset e Load ────────────────────────────────────────────
  private resetForm(): void {
    while (this.itensArray.length) {
      this.itensArray.removeAt(0);
    }
    this.form.reset({
      tipo: 'Transporte',
      status: 'Pendente'
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
      instrucoesEspeciais: guia.instrucoesEspeciais ?? '',
      status: guia.status,
      dataEntregaReal: guia.dataEntregaReal?.split('T')[0] ?? ''
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

  // ─── CRUD Operations com finalize ─────────────────────────────────────────
  salvarGuia(): void {
    this.form.markAllAsTouched();
    
    if (this.form.invalid) {
      this.errorMsg.set('Corrija os erros no formulário antes de continuar.');
      return;
    }

    if (this.totalItensCount() === 0) {
      this.errorMsg.set('Adicione pelo menos um item à guia.');
      return;
    }

    this.isSaving.set(true);
    this.errorMsg.set(null);

    if (this.isEditMode() && this.editingId()) {
      const dto = this.prepararDtoUpdate();
      this.svc.atualizar(this.editingId()!, dto)
        .pipe(finalize(() => this.isSaving.set(false)))
        .subscribe({
          next: () => this.onSaveSuccess('Guia actualizada com sucesso.'),
          error: (err) => this.onSaveError(err.message)
        });
    } else {
      const dto = this.prepararDto();
      this.svc.criar(dto)
        .pipe(finalize(() => this.isSaving.set(false)))
        .subscribe({
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
        this.showToast('PDF gerado com sucesso!');
      },
      error: (err) => this.errorMsg.set(err.message)
    });
  }

  /**
   * Novo método: Gera PDF localmente e persiste automaticamente no ECM.
   * Permite download imediato + registra documento no sistema.
   */
  imprimirEPersistitirGuia(guia: Guia, event?: Event): void {
    if (event) event.stopPropagation();

    // Simular construção de dados para PDF (conforme geração local)
    const fields = [
      { label: 'Número Guia', value: guia.numeroGuia },
      { label: 'Data', value: guia.dataEmissao },
      { label: 'Status', value: guia.status }
      // ... mais campos
    ];

    // Chamar pdf.service.generateAndPersistPdf() para gerar + persistir
    this.pdfService.generateAndPersistPdf(
      `Guia ${guia.numeroGuia}`,
      fields,
      `Guia_${guia.numeroGuia}.pdf`,
      'Relatorio',
      'Interno',
      `Guia de remessa ${guia.numeroGuia}`
    ).then((result) => {
      // Download imediato
      this.pdfService.downloadPdf(result.blob, result.fileName);
      this.showToast('PDF gerado e persistido com sucesso!');

      // Log para referência
      console.log('PDF persistido:', {
        documentoId: result.documentoId,
        url: result.url,
        hashSHA256: result.hashSHA256
      });
    }).catch((err) => {
      this.errorMsg.set(`Erro ao processar PDF: ${err.message}`);
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
    const guia = this.guiaParaDelete();
    if (!guia) return;
    
    this.svc.deletar(guia.id).subscribe({
      next: () => {
        this.cancelarDelete();
        this.carregarGuias();
        this.showToast('Guia cancelada com sucesso.');
      },
      error: (err) => {
        this.errorMsg.set(err.message);
        this.cancelarDelete();
      }
    });
  }

  // ─── Helpers ──────────────────────────────────────────────────────────────
  private onSaveSuccess(msg: string): void {
    this.goToList();
    this.showToast(msg);
  }

  private onSaveError(msg: string): void {
    this.errorMsg.set(msg);
  }

  showToast(msg: string): void {
    this.successMsg.set(msg);
    setTimeout(() => this.successMsg.set(null), 3500);
  }

  hasError(fieldName: string, errorType?: string): boolean {
    const control = this.form.get(fieldName);
    if (!control) return false;
    if (errorType) return control.touched && control.hasError(errorType);
    return control.touched && control.invalid;
  }

  // ─── Métodos para o Template ──────────────────────────────────────────────
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
    return new Date(data).toLocaleDateString('pt-PT', {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric'
    });
  }

  formatarPeso(peso: number): string {
    if (!peso) return '0 kg';
    return `${peso.toLocaleString('pt-PT', { minimumFractionDigits: 2, maximumFractionDigits: 2 })} kg`;
  }

  formatarVolume(volume: number): string {
    if (!volume) return '0 m³';
    return `${volume.toLocaleString('pt-PT')} m³`;
  }

  getItemPesoTotal(index: number): number {
    return this.itensArray.at(index).get('pesoTotal')?.value || 0;
  }

  getItemVolumeTotal(index: number): number {
    return this.itensArray.at(index).get('volumeTotal')?.value || 0;
  }
}