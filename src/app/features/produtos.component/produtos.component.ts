import { Component, OnInit, OnDestroy, inject, signal,computed } from '@angular/core';
import { CommonModule,CurrencyPipe  } from '@angular/common';
import { ReactiveFormsModule,FormBuilder, FormGroup, Validators, AbstractControl} from '@angular/forms';
import { Subject, debounceTime, distinctUntilChanged, takeUntil } from 'rxjs';
import { ProdutosService, ProdutoModel, ProdutoCreateDto, ProdutoUpdateDto, PagedResult } from '../../core/services/produtos.service';

@Component({
  selector: 'app-produtos',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, CurrencyPipe],
  templateUrl: './produtos.component.html',
  styleUrls: ['./produtos.component.css'],
})
export class ProdutosComponent implements OnInit, OnDestroy {
  private readonly svc = inject(ProdutosService);
  private readonly fb  = inject(FormBuilder);
  private readonly destroy$ = new Subject<void>();

  pagedResult = signal<PagedResult<ProdutoModel> | null>(null);
  produtos    = computed(() => this.pagedResult()?.items ?? []);
  isLoading   = signal(false);
  isSaving    = signal(false);
  errorMsg    = signal<string | null>(null);
  successMsg  = signal<string | null>(null);

  filtroSearch    = '';
  filtroCategoria = '';
  mostrarInativos = false;
  currentPage     = 1;
  readonly pageSize = 20;

  // ── Modal ─────────────────────────────────────────────────────────────────
  showModal  = signal(false);
  isEditing  = signal(false);
  editingId  = signal<number | null>(null);

  form!: FormGroup;

  // Subjects para debounce na pesquisa em tempo real
  private readonly searchInput$ = new Subject<string>();

  // ── Ciclo de vida ─────────────────────────────────────────────────────────

  ngOnInit(): void {
    this.initForm();

    this.searchInput$
      .pipe(debounceTime(300), distinctUntilChanged(), takeUntil(this.destroy$))
      .subscribe(() => {
        this.currentPage = 1;
        this.carregarProdutos();
      });

    this.carregarProdutos();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  // ── Formulário Reactivo ───────────────────────────────────────────────────

  private initForm(): void {
    this.form = this.fb.group({
      sku:                 ['', [Validators.required, Validators.maxLength(50)]],
      nome:                ['', [Validators.required, Validators.maxLength(300)]],
      descricao:           [''],
      categoria:           ['', Validators.maxLength(100)],
      fornecedorId:        [null],
      precoCompra:         [0,  [Validators.required, Validators.min(0)]],
      precoVenda:          [0,  [Validators.required, Validators.min(0)]],
      iva:                 [23, [Validators.required, Validators.min(0), Validators.max(100)]],
      stockInicial:        [0,  [Validators.required, Validators.min(0)]],  // apenas criação
      stockMinimo:         [0,  [Validators.required, Validators.min(0)]],
      unidadeMedida:       ['un', Validators.required],
      localizacao:         ['', Validators.maxLength(50)],
      loteObrigatorio:     [false],
      validadeObrigatoria: [false],
      ativo:               [true],
    });
  }

  /** Retorna um controlo do formulário (útil no template). */
  ctrl(name: string): AbstractControl {
    return this.form.get(name)!;
  }

  /** True se o campo foi tocado e tem erro. */
  hasError(name: string, error?: string): boolean {
    const c = this.ctrl(name);
    if (!c.invalid || !c.touched) return false;
    return error ? c.hasError(error) : true;
  }

  // ── Listagem & Paginação ──────────────────────────────────────────────────

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
        next: (result) => {
          this.pagedResult.set(result);
          this.isLoading.set(false);
        },
        error: (err) => {
          this.errorMsg.set(err.message);
          this.isLoading.set(false);
        },
      });
  }

  onSearchChange(value: string): void {
    this.filtroSearch = value;
    this.searchInput$.next(value);
  }

  onCategoriaChange(value: string): void {
    this.filtroCategoria = value;
    this.searchInput$.next(value);
  }

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
    const total = this.pagedResult()?.totalPages ?? 0;
    return Array.from({ length: total }, (_, i) => i + 1);
  }

  // ── Modal ─────────────────────────────────────────────────────────────────

  abrirModalNovo(): void {
    this.isEditing.set(false);
    this.editingId.set(null);
    this.form.reset({
      sku: '', nome: '', descricao: '', categoria: '',
      fornecedorId: null, precoCompra: 0, precoVenda: 0,
      iva: 23, stockInicial: 0, stockMinimo: 0,
      unidadeMedida: 'un', localizacao: '',
      loteObrigatorio: false, validadeObrigatoria: false, ativo: true,
    });
    // stockInicial apenas visível na criação
    this.ctrl('stockInicial').enable();
    this.errorMsg.set(null);
    this.showModal.set(true);
  }

  abrirModalEditar(produto: ProdutoModel): void {
    this.isEditing.set(true);
    this.editingId.set(produto.id);
    this.form.patchValue({
      sku:                 produto.sku,
      nome:                produto.nome,
      descricao:           produto.descricao ?? '',
      categoria:           produto.categoria ?? '',
      fornecedorId:        produto.fornecedorId ?? null,
      precoCompra:         produto.precoCompra,
      precoVenda:          produto.precoVenda,
      iva:                 produto.iva,
      stockInicial:        produto.stockAtual,
      stockMinimo:         produto.stockMinimo,
      unidadeMedida:       produto.unidadeMedida,
      localizacao:         produto.localizacao ?? '',
      loteObrigatorio:     produto.loteObrigatorio,
      validadeObrigatoria: produto.validadeObrigatoria,
      ativo:               produto.ativo,
    });
    // stockInicial não é editável (o stock muda via movimentações)
    this.ctrl('stockInicial').disable();
    this.errorMsg.set(null);
    this.showModal.set(true);
  }

  fecharModal(): void {
    this.showModal.set(false);
    this.form.markAsUntouched();
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

    if (this.isEditing() && this.editingId()) {
      const dto: ProdutoUpdateDto = {
        sku: v.sku.trim(), nome: v.nome.trim(),
        descricao: v.descricao || undefined,
        categoria: v.categoria || undefined,
        fornecedorId: v.fornecedorId || undefined,
        precoCompra: +v.precoCompra, precoVenda: +v.precoVenda,
        iva: +v.iva, stockMinimo: +v.stockMinimo,
        unidadeMedida: v.unidadeMedida,
        localizacao: v.localizacao || undefined,
        loteObrigatorio: v.loteObrigatorio,
        validadeObrigatoria: v.validadeObrigatoria,
        ativo: v.ativo,
      };
      this.svc.atualizar(this.editingId()!, dto).subscribe({
        next: () => this.onSaveSuccess('Produto actualizado com sucesso.'),
        error: (err) => this.onSaveError(err.message),
      });
    } else {
      const dto: ProdutoCreateDto = {
        sku: v.sku.trim(), nome: v.nome.trim(),
        descricao: v.descricao || undefined,
        categoria: v.categoria || undefined,
        fornecedorId: v.fornecedorId || undefined,
        precoCompra: +v.precoCompra, precoVenda: +v.precoVenda,
        iva: +v.iva, stockInicial: +v.stockInicial,
        stockMinimo: +v.stockMinimo,
        unidadeMedida: v.unidadeMedida,
        localizacao: v.localizacao || undefined,
        loteObrigatorio: v.loteObrigatorio,
        validadeObrigatoria: v.validadeObrigatoria,
      };
      this.svc.criar(dto).subscribe({
        next: () => this.onSaveSuccess('Produto criado com sucesso.'),
        error: (err) => this.onSaveError(err.message),
      });
    }
  }

  desativarProduto(produto: ProdutoModel): void {
    if (!confirm(`Deseja desactivar o produto "${produto.nome}"?`)) return;
    this.svc.deletar(produto.id).subscribe({
      next: (res) => {
        this.carregarProdutos();
        this.showToast(res.message);
      },
      error: (err) => this.errorMsg.set(err.message),
    });
  }

  ativarProduto(produto: ProdutoModel): void {
    if (!confirm(`Deseja activar o produto "${produto.nome}"?`)) return;
    this.svc.ativar(produto.id).subscribe({
      next: () => {
        this.carregarProdutos();
        this.showToast('Produto activado com sucesso.');
      },
      error: (err) => this.errorMsg.set(err.message),
    });
  }

  // ── Helpers ───────────────────────────────────────────────────────────────

  private onSaveSuccess(msg: string): void {
    this.isSaving.set(false);
    this.fecharModal();
    this.carregarProdutos();
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

  clearError(): void {
    this.errorMsg.set(null);
  }

  /** Indica se o stock do produto está abaixo ou igual ao mínimo. */
  stockBaixo(p: ProdutoModel): boolean {
    return p.stockAtual <= p.stockMinimo;
  }
}
