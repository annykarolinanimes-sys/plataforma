import {Component, OnInit, OnDestroy, inject, signal, computed} from '@angular/core';
import { CommonModule } from '@angular/common';
import {ReactiveFormsModule, FormBuilder, FormGroup, Validators, AbstractControl} from '@angular/forms';
import { Subject, debounceTime, distinctUntilChanged, takeUntil } from 'rxjs';
import {ArmazensService, Armazem, ArmazemCreateDto, ArmazemUpdateDto, PagedResult} from '../../core/services/armazens.service';

type ModalTab    = 'identificacao' | 'morada' | 'contacto';
type ConfirmType = 'desativar' | 'ativar';

@Component({
  selector: 'app-armazens',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './armazens.component.html',
  styleUrls: ['./armazens.component.css']
})
export class ArmazensComponent implements OnInit, OnDestroy {
  private readonly svc      = inject(ArmazensService);
  private readonly fb       = inject(FormBuilder);
  private readonly destroy$ = new Subject<void>();

  pagedResult  = signal<PagedResult<Armazem> | null>(null);
  armazens     = computed(() => this.pagedResult()?.items ?? []);
  isLoading    = signal(false);
  isSaving     = signal(false);
  errorMsg     = signal<string | null>(null);
  successMsg   = signal<string | null>(null);

  filtroSearch    = '';
  mostrarInativos = false;
  currentPage     = 1;
  readonly pageSize = 20;

  totalArmazens = computed(() => this.pagedResult()?.total ?? 0);
  totalPages    = computed(() => this.pagedResult()?.totalPages ?? 0);
  pages         = computed(() =>
    Array.from({ length: this.totalPages() }, (_, i) => i + 1)
  );

  showModal  = signal(false);
  isEditing  = signal(false);
  editingId  = signal<number | null>(null);
  editingCodigo = signal<string | null>(null); // Novo signal para armazenar o código em edição
  activeTab  = signal<ModalTab>('identificacao');
  form!: FormGroup;

  showConfirmModal = signal(false);
  confirmType      = signal<ConfirmType>('desativar');
  armazemAlvo      = signal<Armazem | null>(null);

  readonly tiposArmazem = ['principal', 'secundario', 'deposito', 'loja', 'cross-dock'];

  private readonly searchInput$ = new Subject<string>();

  ngOnInit(): void {
    this.initForm();
    this.searchInput$
      .pipe(debounceTime(350), distinctUntilChanged(), takeUntil(this.destroy$))
      .subscribe(() => { this.currentPage = 1; this.carregarArmazens(); });
    this.carregarArmazens();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private initForm(): void {
    this.form = this.fb.group({
      // REMOVIDO: campo codigo
      nome:   ['', [Validators.required, Validators.maxLength(200)]],
      tipo:   ['principal'],
      ativo:  [true],

      morada:      ['', Validators.maxLength(300)],
      localidade:  ['', Validators.maxLength(100)],
      codigoPostal:['', [
        Validators.maxLength(20),
        Validators.pattern(/^\d{4}-\d{3}$|^\d{4}$|^$/)
      ]],
      pais: ['Portugal', Validators.maxLength(100)],

      telefone:           ['', Validators.maxLength(30)],
      email:              ['', [Validators.maxLength(200), Validators.email]],
      responsavelNome:    ['', Validators.maxLength(150)],
      responsavelTelefone:['', Validators.maxLength(30)],
      observacoes:        [''],
    });
  }

  ctrl(name: string): AbstractControl { return this.form.get(name)!; }

  hasError(name: string, error?: string): boolean {
    const c = this.ctrl(name);
    if (!c.invalid || !c.touched) return false;
    return error ? c.hasError(error) : true;
  }

  carregarArmazens(): void {
    this.isLoading.set(true);
    this.svc.listar({
      search:   this.filtroSearch    || undefined,
      ativo:    this.mostrarInativos ? undefined : true,
      page:     this.currentPage,
      pageSize: this.pageSize,
    }).subscribe({
      next: r  => { this.pagedResult.set(r); this.isLoading.set(false); },
      error: e => { this.errorMsg.set(e.message); this.isLoading.set(false); }
    });
  }

  onSearchChange(value: string): void {
    this.filtroSearch = value;
    this.searchInput$.next(value);
  }

  toggleInativos(): void {
    this.mostrarInativos = !this.mostrarInativos;
    this.currentPage = 1;
    this.carregarArmazens();
  }

  goToPage(page: number): void {
    if (page < 1 || page > this.totalPages()) return;
    this.currentPage = page;
    this.carregarArmazens();
  }

  // ── Abrir modais ──────────────────────────────────────────
  abrirModalNovo(): void {
    this.isEditing.set(false);
    this.editingId.set(null);
    this.editingCodigo.set(null);
    this.activeTab.set('identificacao');
    this.form.reset({ pais: 'Portugal', tipo: 'principal', ativo: true });
    this.errorMsg.set(null);
    this.showModal.set(true);
  }

  abrirModalEditar(a: Armazem): void {
    this.isEditing.set(true);
    this.editingId.set(a.id);
    this.editingCodigo.set(a.codigo); // Armazena o código original para exibição
    this.activeTab.set('identificacao');
    this.form.patchValue({
      nome:                a.nome,
      tipo:                a.tipo              ?? 'principal',
      ativo:               a.ativo,
      morada:              a.morada            ?? '',
      localidade:          a.localidade        ?? '',
      codigoPostal:        a.codigoPostal      ?? '',
      pais:                a.pais              ?? 'Portugal',
      telefone:            a.telefone          ?? '',
      email:               a.email             ?? '',
      responsavelNome:     a.responsavelNome   ?? '',
      responsavelTelefone: a.responsavelTelefone ?? '',
      observacoes:         a.observacoes       ?? '',
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

  // ── Guardar ───────────────────────────────────────────────
  salvarArmazem(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid) {
      // Navega para a aba com o primeiro erro
      const nameFields      = ['nome', 'tipo'];
      const moradaFields  = ['morada','localidade','codigoPostal','pais'];
      const contactFields = ['telefone','email','responsavelNome','responsavelTelefone'];
      if (nameFields.some(f => this.ctrl(f).invalid))      this.activeTab.set('identificacao');
      else if (moradaFields.some(f => this.ctrl(f).invalid)) this.activeTab.set('morada');
      else if (contactFields.some(f => this.ctrl(f).invalid)) this.activeTab.set('contacto');
      this.errorMsg.set('Corrija os erros antes de continuar.');
      return;
    }

    this.isSaving.set(true);
    this.errorMsg.set(null);
    const v = this.form.getRawValue();

    const base: ArmazemCreateDto = {
      nome:                 v.nome.trim(),
      tipo:                 v.tipo             || undefined,
      morada:               v.morada?.trim()   || undefined,
      localidade:           v.localidade?.trim() || undefined,
      codigoPostal:         v.codigoPostal?.trim() || undefined,
      pais:                 v.pais?.trim()     || 'Portugal',
      telefone:             v.telefone?.trim() || undefined,
      email:                v.email?.trim().toLowerCase() || undefined,
      responsavelNome:      v.responsavelNome?.trim()     || undefined,
      responsavelTelefone:  v.responsavelTelefone?.trim() || undefined,
      observacoes:          v.observacoes?.trim()         || undefined,
    };

    const req$ = this.isEditing() && this.editingId()
      ? this.svc.atualizar(this.editingId()!, { 
          ...base, 
          codigo: this.editingCodigo() || undefined,
          ativo: v.ativo 
        } as ArmazemUpdateDto)
      : this.svc.criar(base);

    req$.subscribe({
      next: () => {
        this.isSaving.set(false);
        this.fecharModal();
        this.carregarArmazens();
        this.showToast(this.isEditing()
          ? 'Armazém actualizado com sucesso'
          : 'Armazém criado com sucesso');
      },
      error: e => { this.errorMsg.set(e.message); this.isSaving.set(false); }
    });
  }

  // ── Confirm modal (substitui confirm() nativo) ────────────
  abrirConfirm(armazem: Armazem, tipo: ConfirmType): void {
    this.armazemAlvo.set(armazem);
    this.confirmType.set(tipo);
    this.showConfirmModal.set(true);
  }

  cancelarConfirm(): void {
    this.showConfirmModal.set(false);
    this.armazemAlvo.set(null);
  }

  executarConfirm(): void {
    const a = this.armazemAlvo();
    if (!a) return;

    const req$ = this.confirmType() === 'desativar'
      ? this.svc.deletar(a.id)
      : this.svc.ativar(a.id);

    req$.subscribe({
      next: () => {
        this.cancelarConfirm();
        this.carregarArmazens();
        this.showToast(this.confirmType() === 'desativar'
          ? 'Armazém desactivado com sucesso'
          : 'Armazém activado com sucesso');
      },
      error: e => { this.errorMsg.set(e.message); this.cancelarConfirm(); }
    });
  }

  // ── Utils ─────────────────────────────────────────────────
  showToast(msg: string): void {
    this.successMsg.set(msg);
    setTimeout(() => this.successMsg.set(null), 3500);
  }

  clearError(): void { this.errorMsg.set(null); }

  formatarTipo(tipo?: string): string {
    const mapa: Record<string, string> = {
      principal:  'Principal',
      secundario: 'Secundário',
      deposito:   'Depósito',
      loja:       'Loja',
      'cross-dock': 'Cross-Dock',
    };
    return tipo ? (mapa[tipo] ?? tipo) : '—';
  }
}