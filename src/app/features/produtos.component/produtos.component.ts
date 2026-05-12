import {
  Component, OnInit, OnDestroy,
  inject, signal, computed
} from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  ReactiveFormsModule, FormBuilder, FormGroup,
  Validators, AbstractControl
} from '@angular/forms';
import { Subject, debounceTime, distinctUntilChanged, takeUntil } from 'rxjs';

import {
  ProdutosService, ProdutoModel,
  ProdutoCreateDto, ProdutoUpdateDto, PagedResult
} from '../../core/services/produtos.service';
import { PdfService, PdfField } from '../../core/services/pdf.service';
import {
  FornecedoresCatalogoService, FornecedorModel
} from '../../core/services/fornecedores-catalogo.service';
import { UiStateService } from '../../core/services/ui-state.service';

@Component({
  selector: 'app-produtos',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './produtos.component.html',
  styleUrls: ['./produtos.component.css'],
})
export class ProdutosComponent implements OnInit, OnDestroy {

  // ── Serviços ─────────────────────────────────────────────────────────────
  private readonly svc              = inject(ProdutosService);
  private readonly fb               = inject(FormBuilder);
  private readonly pdfService       = inject(PdfService);
  private readonly fornecedoresSvc  = inject(FornecedoresCatalogoService);
  readonly uiState                  = inject(UiStateService);
  private readonly destroy$         = new Subject<void>();

  // ── Estado UI (padrão ui-state) ──────────────────────────────────────────
  readonly currentState = this.uiState.currentProdutoState;
  readonly editingId = this.uiState.currentProdutoId;

  // helpers de leitura de estado (equivalentes aos do UiStateService)
  isListView()    { return this.currentState() === 'list'; }
  isCreateView()  { return this.currentState() === 'create'; }
  isEditView()    { return this.currentState() === 'edit'; }
  isDetailsView() { return this.currentState() === 'details'; }

  // ── Dados ─────────────────────────────────────────────────────────────────
  pagedResult  = signal<PagedResult<ProdutoModel> | null>(null);
  produtos     = computed(() => this.pagedResult()?.items ?? []);
  isLoading    = signal(false);
  isSaving     = signal(false);
  errorMsg     = signal<string | null>(null);
  successMsg   = signal<string | null>(null);
  fornecedores = signal<FornecedorModel[]>([]);

  // ── Filtros & paginação ───────────────────────────────────────────────────
  filtroSearch    = '';
  filtroCategoria = '';
  mostrarInativos = false;
  currentPage     = 1;
  readonly pageSize = 20;

  form!: FormGroup;
  private readonly searchInput$ = new Subject<string>();

  // ── Ciclo de vida ─────────────────────────────────────────────────────────

  ngOnInit(): void {
    this.initForm();
    this.uiState.goToProdutoList();
    this.carregarFornecedores();

    this.searchInput$
      .pipe(debounceTime(300), distinctUntilChanged(), takeUntil(this.destroy$))
      .subscribe(() => { this.currentPage = 1; this.carregarProdutos(); });

    this.carregarProdutos();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  // ── Formulário ────────────────────────────────────────────────────────────

  private initForm(): void {
    this.form = this.fb.group({
      sku:                 [{ value: '', disabled: true }],
      nome:                ['', [Validators.required, Validators.maxLength(300)]],
      descricao:           [''],
      categoria:           ['', Validators.maxLength(100)],
      fornecedorCodigo:    ['', Validators.maxLength(50)],
      precoCompra:         [null, [Validators.required, Validators.min(0)]],
      precoVenda:          [null, [Validators.required, Validators.min(0)]],
      iva:                 [23,   [Validators.required, Validators.min(0), Validators.max(100)]],
      stockInicial:        [0,    [Validators.required, Validators.min(0)]],
      stockMinimo:         [0,    [Validators.required, Validators.min(0)]],
      unidadeMedida:       ['un', Validators.required],
      localizacao:         ['', Validators.maxLength(50)],
      loteObrigatorio:     [false],
      validadeObrigatoria: [false],
      ativo:               [true],
    });
  }

  ctrl(name: string): AbstractControl {
    return this.form.get(name)!;
  }

  hasError(name: string, error?: string): boolean {
    const c = this.ctrl(name);
    if (!c.invalid || !c.touched) return false;
    return error ? c.hasError(error) : true;
  }


  carregarProdutos(): void {
    this.isLoading.set(true);
    this.svc
      .listar({
        search:    this.filtroSearch    || undefined,
        categoria: this.filtroCategoria || undefined,
        ativo:     this.mostrarInativos ? undefined : true,
        page:      this.currentPage,
        pageSize:  this.pageSize,
      })
      .subscribe({
        next:  r  => { this.pagedResult.set(r); this.isLoading.set(false); },
        error: e  => { this.errorMsg.set(e.message); this.isLoading.set(false); },
      });
  }

  carregarFornecedores(): void {
    this.fornecedoresSvc.listar({ ativo: true }).subscribe({
      next:  r   => this.fornecedores.set(r.items),
      error: err => console.error('Erro ao carregar fornecedores:', err),
    });
  }

  onSearchChange(value: string)   { this.filtroSearch    = value; this.searchInput$.next(value); }
  onCategoriaChange(value: string){ this.filtroCategoria = value; this.searchInput$.next(value); }

  toggleInativos(): void {
    this.mostrarInativos = !this.mostrarInativos;
    this.currentPage = 1;
    this.carregarProdutos();
  }

  goToPage(page: number): void {
    const total = this.pagedResult()?.totalPages ?? 1;
    if (page < 1 || page > total) return;
    this.currentPage = page;
    this.carregarProdutos();
  }

  get pages(): number[] {
    return Array.from({ length: this.pagedResult()?.totalPages ?? 0 }, (_, i) => i + 1);
  }


  irParaNovo(): void {
    this.uiState.goToProdutoCreate();
    this.form.reset({
      sku: '', nome: '', descricao: '', categoria: '',
      fornecedorCodigo: '', precoCompra: null, precoVenda: null,
      iva: 23, stockInicial: 0, stockMinimo: 0,
      unidadeMedida: 'un', localizacao: '',
      loteObrigatorio: false, validadeObrigatoria: false, ativo: true,
    });
    // FIX acessibilidade: usar FormControl.disable() em vez de [disabled] no template
    this.ctrl('sku').disable();
    this.ctrl('stockInicial').enable();
    this.errorMsg.set(null);
  }

  irParaEditar(produto: ProdutoModel): void {
    this.uiState.goToProdutoEdit(produto.id);
    this.form.patchValue({
      sku:                 produto.sku,
      nome:                produto.nome,
      descricao:           produto.descricao ?? '',
      categoria:           produto.categoria ?? '',
      // FIX prefixo FOR-: guardamos só a parte após o prefixo
      fornecedorCodigo:    this._stripPrefix(produto.fornecedorCodigo ?? '', 'FOR-'),
      precoCompra:         produto.precoCompra,
      precoVenda:          produto.precoVenda,
      iva:                 produto.iva,
      stockInicial:        produto.stockAtual,
      stockMinimo:         produto.stockMinimo,
      unidadeMedida:       produto.unidadeMedida,
      // FIX prefixo ARM-: guardamos só a parte após o prefixo
      localizacao:         this._stripPrefix(produto.localizacao ?? '', 'ARM-'),
      loteObrigatorio:     produto.loteObrigatorio,
      validadeObrigatoria: produto.validadeObrigatoria,
      ativo:               produto.ativo,
    });
    this.ctrl('sku').disable();
    // stock só muda via movimentações — desativamos com FormControl.disable()
    this.ctrl('stockInicial').disable();
    this.errorMsg.set(null);
  }

  irParaDetalhes(produto: ProdutoModel, event?: Event): void {
    if (event) event.stopPropagation();
    this.irParaEditar(produto);
    this.uiState.goToProdutoDetails(produto.id);
    // Modo somente-leitura: desativamos todo o form via fieldset no template
  }

  voltarParaLista(): void {
    this.uiState.goToProdutoList();
    this.form.markAsUntouched();
  }

  // ── PDF ───────────────────────────────────────────────────────────────────

  imprimirPdf(produto: ProdutoModel, event?: Event): void {
    if (event) event.stopPropagation();
    const fields: PdfField[] = [
      { label: 'SKU',            value: produto.sku },
      { label: 'Nome',           value: produto.nome },
      { label: 'Descrição',      value: produto.descricao || '—' },
      { label: 'Categoria',      value: produto.categoria || '—' },
      { label: 'Fornecedor',     value: produto.fornecedorCodigo || '—' },
      { label: 'Preço de Compra',value: `€ ${produto.precoCompra.toFixed(2)}` },
      { label: 'Preço de Venda', value: `€ ${produto.precoVenda.toFixed(2)}` },
      { label: 'IVA',            value: `${produto.iva}%` },
      { label: 'Stock Atual',    value: produto.stockAtual ?? 0 },
      { label: 'Stock Mínimo',   value: produto.stockMinimo ?? 0 },
      { label: 'Unidade',        value: produto.unidadeMedida || '—' },
      { label: 'Localização',    value: produto.localizacao || '—' },
      { label: 'Ativo',          value: produto.ativo ? 'Sim' : 'Não' },
    ];
    try {
      const blob = this.pdfService.generateEntityPdf(
        `Produto ${produto.sku}`, fields, 'Documento gerado automaticamente.'
      );
      this.pdfService.downloadPdf(blob, `Produto_${produto.sku || produto.id}.pdf`);
    } catch {
      this.errorMsg.set('Erro ao gerar PDF do produto.');
    }
  }

  // ── CRUD ──────────────────────────────────────────────────────────────────

  salvarProduto(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid) {
      this.errorMsg.set('Corrija os erros no formulário antes de continuar.');
      return;
    }

    this.isSaving.set(true);
    this.errorMsg.set(null);
    const v = this.form.getRawValue();

    if (!this.isEditView() && +v.stockMinimo < +v.stockInicial) {
      this.errorMsg.set('O stock mínimo não pode ser inferior ao stock inicial.');
      this.isSaving.set(false);
      return;
    }

    // FIX prefixos: concatenamos FOR- e ARM- antes de enviar à API
    const fornecedorCodigoCompleto = v.fornecedorCodigo
      ? `FOR-${v.fornecedorCodigo.trim()}`
      : undefined;
    const localizacaoCompleta = v.localizacao
      ? `ARM-${v.localizacao.trim()}`
      : undefined;

    if (this.isEditView() && this.editingId()) {
      const dto: ProdutoUpdateDto = {
        nome:               v.nome.trim(),
        descricao:          v.descricao || undefined,
        categoria:          v.categoria || undefined,
        fornecedorCodigo:   fornecedorCodigoCompleto,
        precoCompra:        +v.precoCompra,
        precoVenda:         +v.precoVenda,
        iva:                +v.iva,
        stockMinimo:        +v.stockMinimo,
        unidadeMedida:      v.unidadeMedida,
        localizacao:        localizacaoCompleta,
        loteObrigatorio:    v.loteObrigatorio,
        validadeObrigatoria:v.validadeObrigatoria,
        ativo:              v.ativo,
      };
      this.svc.atualizar(this.editingId()!, dto).subscribe({
        next:  () => this._onSaveSuccess('Produto actualizado com sucesso.'),
        error: e  => this._onSaveError(e.message),
      });
    } else {
      const dto: ProdutoCreateDto = {
        nome:               v.nome.trim(),
        descricao:          v.descricao || undefined,
        categoria:          v.categoria || undefined,
        fornecedorCodigo:   fornecedorCodigoCompleto,
        precoCompra:        +v.precoCompra,
        precoVenda:         +v.precoVenda,
        iva:                +v.iva,
        stockInicial:       +v.stockInicial,
        stockMinimo:        +v.stockMinimo,
        unidadeMedida:      v.unidadeMedida,
        localizacao:        localizacaoCompleta,
        loteObrigatorio:    v.loteObrigatorio,
        validadeObrigatoria:v.validadeObrigatoria,
      };
      this.svc.criar(dto).subscribe({
        next:  () => this._onSaveSuccess('Produto criado com sucesso.'),
        error: e  => this._onSaveError(e.message),
      });
    }
  }

  desativarProduto(produto: ProdutoModel): void {
    if (!confirm(`Deseja desactivar o produto "${produto.nome}"?`)) return;
    this.svc.deletar(produto.id).subscribe({
      next:  r   => { this.carregarProdutos(); this.showToast(r.message); },
      error: err => this.errorMsg.set(err.message),
    });
  }

  ativarProduto(produto: ProdutoModel): void {
    if (!confirm(`Deseja activar o produto "${produto.nome}"?`)) return;
    this.svc.ativar(produto.id).subscribe({
      next:  () => { this.carregarProdutos(); this.showToast('Produto activado com sucesso.'); },
      error: err => this.errorMsg.set(err.message),
    });
  }

  // ── Helpers ───────────────────────────────────────────────────────────────

  private _onSaveSuccess(msg: string): void {
    this.isSaving.set(false);
    this.voltarParaLista();
    this.carregarProdutos();
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

  stockBaixo(p: ProdutoModel): boolean {
    return p.stockAtual <= p.stockMinimo;
  }

 
  private _stripPrefix(value: string, prefix: string): string {
    return value.startsWith(prefix) ? value.slice(prefix.length) : value;
  }

  /**
   * Formata preço para exibição em pt-PT sem depender do CurrencyPipe/locale.
   * FIX NG0701: evitamos CurrencyPipe com locale 'pt-PT' que exige registo do locale.
   * O registo está em app.config.ts; este método é um fallback seguro.
   */
  formatarPreco(valor: number): string {
    return new Intl.NumberFormat('pt-PT', {
      style: 'currency', currency: 'EUR', minimumFractionDigits: 2
    }).format(valor);
  }
}
