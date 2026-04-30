import {
  Component, OnInit, OnDestroy, inject, signal, computed
} from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  ReactiveFormsModule, FormBuilder, FormGroup,
  Validators, AbstractControl
} from '@angular/forms';
import { Subject, debounceTime, distinctUntilChanged, takeUntil } from 'rxjs';
import { VeiculosService, Veiculo, PagedResult } from '../../core/services/veiculos.service';

const MATRICULA_REGEX = /^([A-Z]{2}-\d{2}-[A-Z]{2}|\d{2}-[A-Z]{2}-\d{2}|\d{2}-\d{2}-[A-Z]{2})$/;

type ModalTab = 'geral' | 'tecnico' | 'vinculo';

@Component({
  selector: 'app-veiculos',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './veiculos.component.html',
  styleUrls: ['./veiculos.component.css']
})
export class VeiculosComponent implements OnInit, OnDestroy {
  private readonly svc     = inject(VeiculosService);
  private readonly fb      = inject(FormBuilder);
  private readonly destroy$ = new Subject<void>();

  // ── State ─────────────────────────────────────────────────
  pagedResult   = signal<PagedResult<Veiculo> | null>(null);
  veiculos      = computed(() => this.pagedResult()?.items ?? []);
  isLoading     = signal(false);
  isSaving      = signal(false);
  errorMsg      = signal<string | null>(null);
  successMsg    = signal<string | null>(null);

  // ── Filters ───────────────────────────────────────────────
  filtroSearch        = '';
  filtroCombustivel   = '';
  mostrarInativos     = false;
  currentPage         = 1;
  readonly pageSize   = 15;

  // ── Modal ─────────────────────────────────────────────────
  showModal     = signal(false);
  isEditing     = signal(false);
  editingId     = signal<number | null>(null);
  activeTab     = signal<ModalTab>('geral');

  // ── Form ──────────────────────────────────────────────────
  form!: FormGroup;

  // ── Delete confirm ────────────────────────────────────────
  showDeleteConfirm    = signal(false);
  veiculoParaDelete    = signal<Veiculo | null>(null);

  private readonly searchInput$ = new Subject<string>();

  readonly currentYear = new Date().getFullYear();
  readonly combustivelOpcoes = [
    'Gasolina', 'Diesel', 'Híbrido', 'Eléctrico', 'GPL', 'Hidrogénio'
  ];

  // ── Computed helpers ──────────────────────────────────────
  totalVeiculos = computed(() => this.pagedResult()?.total ?? 0);
  totalPages    = computed(() => this.pagedResult()?.totalPages ?? 0);
  pages         = computed(() =>
    Array.from({ length: this.totalPages() }, (_, i) => i + 1)
  );

  ngOnInit(): void {
    this.initForm();

    this.searchInput$
      .pipe(debounceTime(350), distinctUntilChanged(), takeUntil(this.destroy$))
      .subscribe(() => { this.currentPage = 1; this.carregarVeiculos(); });

    this.carregarVeiculos();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  // ── Form setup ────────────────────────────────────────────
  private initForm(): void {
    this.form = this.fb.group({
      // Aba: Dados Gerais
      matricula: ['', [
        Validators.required,
        Validators.maxLength(20),
        Validators.pattern(MATRICULA_REGEX)
      ]],
      marca:  ['', [Validators.required, Validators.maxLength(100)]],
      modelo: ['', [Validators.required, Validators.maxLength(100)]],
      cor:    ['', Validators.maxLength(50)],
      ano:    [null, [
        Validators.min(1900),
        Validators.max(this.currentYear + 1)
      ]],
      vin:    ['', Validators.maxLength(50)],
      observacoes: [''],
      ativo:  [true],

      // Aba: Especificações Técnicas
      tipoCombustivel: [''],
      cilindrada:      [null, [Validators.min(0), Validators.max(99999)]],
      potencia:        [null, [Validators.min(0), Validators.max(9999)]],
      lugares:         [null, [Validators.min(1), Validators.max(200)]],
      peso:            [null, [Validators.min(0)]],

      // Aba: Vínculos
      proprietarioId: [null],
    });
  }

  ctrl(name: string): AbstractControl { return this.form.get(name)!; }

  hasError(name: string, error?: string): boolean {
    const c = this.ctrl(name);
    if (!c.invalid || !c.touched) return false;
    return error ? c.hasError(error) : true;
  }

  // ── Load data ─────────────────────────────────────────────
  carregarVeiculos(): void {
    this.isLoading.set(true);
    this.svc.listar({
      search:      this.filtroSearch        || undefined,
      combustivel: this.filtroCombustivel   || undefined,
      ativo:       this.mostrarInativos     ? undefined : true,
      page:        this.currentPage,
      pageSize:    this.pageSize,
    }).subscribe({
      next: (result) => { this.pagedResult.set(result); this.isLoading.set(false); },
      error: (err)   => { this.errorMsg.set(err.message ?? err.error?.message ?? 'Erro ao carregar veículos'); this.isLoading.set(false); }
    });
  }

  onSearchChange(value: string): void {
    this.filtroSearch = value;
    this.searchInput$.next(value);
  }

  onCombustivelChange(value: string): void {
    this.filtroCombustivel = value;
    this.currentPage = 1;
    this.carregarVeiculos();
  }

  toggleInativos(): void {
    this.mostrarInativos = !this.mostrarInativos;
    this.currentPage = 1;
    this.carregarVeiculos();
  }

  goToPage(page: number): void {
    const total = this.totalPages();
    if (page < 1 || page > total) return;
    this.currentPage = page;
    this.carregarVeiculos();
  }

  // ── Modal ─────────────────────────────────────────────────
  abrirModalNovo(): void {
    this.isEditing.set(false);
    this.editingId.set(null);
    this.activeTab.set('geral');
    this.form.reset({ ativo: true });
    this.errorMsg.set(null);
    this.showModal.set(true);
  }

  abrirModalEditar(v: Veiculo): void {
    this.isEditing.set(true);
    this.editingId.set(v.id);
    this.activeTab.set('geral');
    this.form.patchValue({
      matricula:       v.matricula,
      marca:           v.marca,
      modelo:          v.modelo,
      cor:             v.cor             ?? '',
      ano:             v.ano             ?? null,
      vin:             v.vin             ?? '',
      observacoes:     v.observacoes     ?? '',
      ativo:           v.ativo,
      tipoCombustivel: v.tipoCombustivel ?? '',
      cilindrada:      v.cilindrada      ?? null,
      potencia:        v.potencia        ?? null,
      lugares:         v.lugares         ?? null,
      peso:            v.peso            ?? null,
      proprietarioId:  v.proprietarioId  ?? null,
    });
    this.errorMsg.set(null);
    this.showModal.set(true);
  }

  fecharModal(): void {
    this.showModal.set(false);
    this.form.markAsUntouched();
    this.errorMsg.set(null);
  }

  setTab(tab: ModalTab): void { this.activeTab.set(tab); }

  // ── Save ──────────────────────────────────────────────────
  salvarVeiculo(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid) {
      // Switch to first tab with errors
      const geralFields = ['matricula','marca','modelo','cor','ano','vin'];
      const tecFields   = ['tipoCombustivel','cilindrada','potencia','lugares','peso'];
      if (geralFields.some(f => this.ctrl(f).invalid)) this.activeTab.set('geral');
      else if (tecFields.some(f => this.ctrl(f).invalid)) this.activeTab.set('tecnico');
      this.errorMsg.set('Corrija os erros no formulário antes de continuar.');
      return;
    }

    this.isSaving.set(true);
    this.errorMsg.set(null);
    const v = this.form.getRawValue();

    const dto: Partial<Veiculo> = {
      matricula:       v.matricula.trim().toUpperCase(),
      marca:           v.marca.trim(),
      modelo:          v.modelo.trim(),
      cor:             v.cor?.trim()             || undefined,
      ano:             v.ano                     || undefined,
      vin:             v.vin?.trim()             || undefined,
      observacoes:     v.observacoes?.trim()     || undefined,
      ativo:           v.ativo,
      tipoCombustivel: v.tipoCombustivel?.trim() || undefined,
      cilindrada:      v.cilindrada              || undefined,
      potencia:        v.potencia                || undefined,
      lugares:         v.lugares                 || undefined,
      peso:            v.peso                    || undefined,
      proprietarioId:  v.proprietarioId          || undefined,
    };

    const req$ = this.isEditing() && this.editingId()
      ? this.svc.atualizar(this.editingId()!, dto as Veiculo)
      : this.svc.criar(dto as Veiculo);

    req$.subscribe({
      next: () => {
        this.isSaving.set(false);
        this.fecharModal();
        this.carregarVeiculos();
        this.showToast(this.isEditing() ? 'Veículo actualizado com sucesso' : 'Veículo criado com sucesso');
      },
      error: (err) => {
        this.errorMsg.set(err.error?.message ?? err.message ?? 'Erro ao guardar veículo');
        this.isSaving.set(false);
      }
    });
  }

  // ── Delete ────────────────────────────────────────────────
  confirmarDesativar(v: Veiculo): void {
    this.veiculoParaDelete.set(v);
    this.showDeleteConfirm.set(true);
  }

  cancelarDelete(): void {
    this.showDeleteConfirm.set(false);
    this.veiculoParaDelete.set(null);
  }

  executarDesativar(): void {
    const v = this.veiculoParaDelete();
    if (!v) return;
    this.svc.deletar(v.id).subscribe({
      next: () => { this.cancelarDelete(); this.carregarVeiculos(); this.showToast('Veículo desactivado com sucesso'); },
      error: (err) => { this.errorMsg.set(err.error?.message ?? 'Erro ao desactivar'); this.cancelarDelete(); }
    });
  }

  ativarVeiculo(v: Veiculo): void {
    if (!confirm(`Activar o veículo ${v.matricula}?`)) return;
    this.svc.ativar(v.id).subscribe({
      next: () => { this.carregarVeiculos(); this.showToast('Veículo activado com sucesso'); },
      error: (err) => this.errorMsg.set(err.error?.message ?? 'Erro ao activar')
    });
  }

  // ── Utils ─────────────────────────────────────────────────
  showToast(msg: string): void {
    this.successMsg.set(msg);
    setTimeout(() => this.successMsg.set(null), 3500);
  }

  clearError(): void { this.errorMsg.set(null); }

  getMatriculaErrorMsg(): string {
    const c = this.ctrl('matricula');
    if (c.hasError('required')) return 'Matrícula é obrigatória.';
    if (c.hasError('pattern'))  return 'Formato inválido. Use: AA-00-AA, 00-AA-00 ou 00-00-AA.';
    if (c.hasError('maxlength')) return 'Máximo 20 caracteres.';
    return '';
  }
}