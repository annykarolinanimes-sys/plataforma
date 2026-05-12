import {Component, OnInit, OnDestroy,inject, signal, computed} from '@angular/core';
import { CommonModule } from '@angular/common';
import {ReactiveFormsModule, FormBuilder, FormGroup,Validators, AbstractControl, ValidatorFn} from '@angular/forms';
import { Subject, debounceTime, distinctUntilChanged, takeUntil } from 'rxjs';

import { PdfService, PdfField } from '../../core/services/pdf.service';
import {ArmazensService, Armazem,ArmazemCreateDto, ArmazemUpdateDto, PagedResult} from '../../core/services/armazens.service';
import { UiStateService } from '../../core/services/ui-state.service';

type ModalTab    = 'identificacao' | 'morada' | 'contacto';
type ConfirmType = 'desativar' | 'ativar';

function noWhitespaceValidator(): ValidatorFn {
  return (control: AbstractControl) => {
    const v = control.value as string;
    if (!v) return null;                         
    return v.trim().length === 0 ? { whitespace: true } : null;
  };
}

function codigoPostalValidator(): ValidatorFn {
  return (control: AbstractControl) => {
    const form = control.parent;
    if (!form) return null;
    const pais = (form.get('pais')?.value ?? '').trim().toLowerCase();
    const cp   = (control.value ?? '').trim();

    if (pais === 'portugal' || pais === '') {
      if (!cp) return { cpObrigatorio: true };
      if (!/^\d{4}-\d{3}$|^\d{4}$/.test(cp)) return { pattern: true };
    } else {
      if (cp && !/^\d{4}-\d{3}$|^\d{4}$|^[A-Z0-9\s\-]{3,10}$/.test(cp)) {
        return { pattern: true };
      }
    }
    return null;
  };
}

@Component({
  selector: 'app-armazens',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './armazens.component.html',
  styleUrls: ['./armazens.component.css'],
})
export class ArmazensComponent implements OnInit, OnDestroy {

  private readonly svc        = inject(ArmazensService);
  private readonly fb         = inject(FormBuilder);
  private readonly pdfService = inject(PdfService);
  readonly uiState            = inject(UiStateService);
  private readonly destroy$   = new Subject<void>();

  currentState  = this.uiState.currentArmazemState;
  editingId     = this.uiState.currentArmazemId;
  isEditing     = computed(() => this.currentState() === 'edit');
  isViewing     = computed(() => this.currentState() === 'details');
  editingCodigo = signal<string | null>(null);
  activeTab     = signal<ModalTab>('identificacao');

  pagedResult = signal<PagedResult<Armazem> | null>(null);
  armazens    = computed(() => this.pagedResult()?.items ?? []);
  isLoading   = signal(false);
  isSaving    = signal(false);
  errorMsg    = signal<string | null>(null);
  successMsg  = signal<string | null>(null);

  totalArmazens  = computed(() => this.pagedResult()?.total ?? 0);
  totalAtivos    = computed(() => this.armazens().filter(a => a.ativo).length);
  totalInativos  = computed(() => this.armazens().filter(a => !a.ativo).length);
  tiposDistintos = computed(() => {
    const tipos = this.armazens().map(a => a.tipo).filter(Boolean) as string[];
    return new Set(tipos).size;
  });
  novosEsteMes   = computed(() => {
    const agora = new Date();
    return this.armazens().filter(a => {
      const d = new Date(a.criadoEm);
      return d.getMonth() === agora.getMonth() &&
             d.getFullYear() === agora.getFullYear();
    }).length;
  });

  filtroSearch    = '';
  mostrarInativos = false;
  currentPage     = 1;
  readonly pageSize = 20;

  totalPages = computed(() => this.pagedResult()?.totalPages ?? 0);
  pages      = computed(() =>
    Array.from({ length: this.totalPages() }, (_, i) => i + 1)
  );

  showConfirmModal = signal(false);
  confirmType      = signal<ConfirmType>('desativar');
  armazemAlvo      = signal<Armazem | null>(null);

  readonly tiposArmazem = [
    'principal', 'secundario', 'deposito', 'loja', 'cross-dock'
  ] as const;

  form!: FormGroup;
  private readonly searchInput$ = new Subject<string>();


  ngOnInit(): void {
    this.initForm();
    this.uiState.goToArmazemList();

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
      nome: ['', [
        Validators.required,
        Validators.maxLength(200),
        noWhitespaceValidator(),
      ]],
      tipo:  ['principal'],
      ativo: [true],

      morada:      ['', Validators.maxLength(300)],
      // FIX: campo renomeado para 'localizacao' — sincronizado com a BD
      localizacao: ['', Validators.maxLength(100)],
      // FIX: codigoPostalValidator valida formato e obrigatoriedade condicional (PT)
      codigoPostal: ['', [Validators.maxLength(20), codigoPostalValidator()]],
      pais: ['Portugal', Validators.maxLength(100)],

      telefone:            ['', Validators.maxLength(30)],
      email:               ['', [Validators.maxLength(200), Validators.email]],
      responsavelNome:     ['', Validators.maxLength(150)],
      responsavelTelefone: ['', Validators.maxLength(30)],
      observacoes:         [''],

      // Campo de auditoria readonly — desactivado via FormBuilder
      criadoEm: [{ value: '', disabled: true }],
    });

    // Revalidar codigoPostal quando o país muda
    this.form.get('pais')?.valueChanges
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => this.form.get('codigoPostal')?.updateValueAndValidity());
  }

  ctrl(name: string): AbstractControl { return this.form.get(name)!; }

  hasError(name: string, error?: string): boolean {
    const c = this.ctrl(name);
    if (!c.invalid || !c.touched) return false;
    return error ? c.hasError(error) : true;
  }

  // ── Listagem & Paginação ──────────────────────────────────────────────────

  carregarArmazens(): void {
    this.isLoading.set(true);
    this.svc.listar({
      search:   this.filtroSearch || undefined,
      ativo:    this.mostrarInativos ? undefined : true,
      page:     this.currentPage,
      pageSize: this.pageSize,
    }).subscribe({
      next:  r  => { this.pagedResult.set(r); this.isLoading.set(false); },
      error: e  => { this.errorMsg.set(e.message); this.isLoading.set(false); },
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

  // ── REFACTORIZAÇÃO: patchValue centralizado ───────────────────────────────
  // Elimina duplicação entre goToEdit() e goToDetails() — DRY.
  // FIX nomenclatura: usa 'localizacao' (BD) em vez de 'localidade'
  private _patchArmazem(a: Armazem): void {
    this.editingCodigo.set(a.codigo);
    this.activeTab.set('identificacao');
    this.form.patchValue({
      nome:                a.nome,
      tipo:                a.tipo                 ?? 'principal',
      ativo:               a.ativo,
      morada:              a.morada               ?? '',
      localizacao:         a.localizacao          ?? '',   // FIX: era 'localidade'
      codigoPostal:        a.codigoPostal         ?? '',
      pais:                a.pais                 ?? 'Portugal',
      telefone:            a.telefone             ?? '',
      email:               a.email                ?? '',
      responsavelNome:     a.responsavelNome      ?? '',
      responsavelTelefone: a.responsavelTelefone  ?? '',
      observacoes:         a.observacoes          ?? '',
      criadoEm:            new Date(a.criadoEm).toLocaleDateString('pt-PT', {
                             day: '2-digit', month: '2-digit', year: 'numeric',
                             hour: '2-digit', minute: '2-digit',
                           }),
    });
    this.errorMsg.set(null);
  }

  // ── Navegação ─────────────────────────────────────────────────────────────

  goToCreate(): void {
    this.activeTab.set('identificacao');
    this.form.reset({ pais: 'Portugal', tipo: 'principal', ativo: true });
    this.editingCodigo.set(null);
    this.errorMsg.set(null);
    this.uiState.goToArmazemCreate();
  }

  goToEdit(a: Armazem): void {
    this._patchArmazem(a);
    this.uiState.goToArmazemEdit(a.id);
  }

  goToDetails(armazem: Armazem, event?: Event): void {
    if (event) event.stopPropagation();
    this._patchArmazem(armazem);
    this.uiState.goToArmazemDetails(armazem.id);
  }

  cancel(): void {
    this.uiState.goToArmazemList();
    this.form.markAsUntouched();
    this.errorMsg.set(null);
  }

  setTab(tab: ModalTab): void { this.activeTab.set(tab); }

  // ── PDF ───────────────────────────────────────────────────────────────────

  imprimirPdf(armazem: Armazem, event?: Event): void {
    if (event) event.stopPropagation();
    const fields: PdfField[] = [
      { label: 'Código',       value: armazem.codigo },
      { label: 'Nome',         value: armazem.nome },
      { label: 'Tipo',         value: this.formatarTipo(armazem.tipo) },
      { label: 'Localização',  value: armazem.localizacao  || '—' },
      { label: 'Código Postal',value: armazem.codigoPostal || '—' },
      { label: 'País',         value: armazem.pais         || '—' },
      { label: 'Telefone',     value: armazem.telefone     || '—' },
      { label: 'Email',        value: armazem.email        || '—' },
      { label: 'Responsável',  value: armazem.responsavelNome || '—' },
      { label: 'Activo',       value: armazem.ativo ? 'Sim' : 'Não' },
    ];
    try {
      const blob = this.pdfService.generateEntityPdf(
        `Armazém ${armazem.nome}`, fields, 'Documento gerado automaticamente.'
      );
      this.pdfService.downloadPdf(blob, `Armazem_${armazem.nome.replace(/\s+/g, '_')}.pdf`);
    } catch {
      this.errorMsg.set('Erro ao gerar PDF do armazém.');
    }
  }

  // ── CRUD ──────────────────────────────────────────────────────────────────

  salvarArmazem(): void {
    this.form.markAllAsTouched();

    if (this.form.invalid) {
      // Navega para a tab onde está o primeiro erro
      const nameFields    = ['nome', 'tipo'];
      const moradaFields  = ['morada', 'localizacao', 'codigoPostal', 'pais'];
      const contactFields = ['telefone', 'email', 'responsavelNome', 'responsavelTelefone'];

      if (nameFields.some(f => this.ctrl(f).invalid))       this.activeTab.set('identificacao');
      else if (moradaFields.some(f => this.ctrl(f).invalid))  this.activeTab.set('morada');
      else if (contactFields.some(f => this.ctrl(f).invalid)) this.activeTab.set('contacto');

      this.errorMsg.set('Corrija os erros antes de continuar.');
      return;
    }

    this.isSaving.set(true);
    this.errorMsg.set(null);
    const v = this.form.getRawValue();

    // FIX: campo 'localizacao' enviado para a API (não 'localidade')
    const base: ArmazemCreateDto = {
      nome:                v.nome.trim(),
      tipo:                v.tipo             || undefined,
      morada:              v.morada?.trim()   || undefined,
      localizacao:         v.localizacao?.trim() || undefined,
      codigoPostal:        v.codigoPostal?.trim() || undefined,
      pais:                v.pais?.trim()     || 'Portugal',
      telefone:            v.telefone?.trim() || undefined,
      email:               v.email?.trim().toLowerCase() || undefined,
      responsavelNome:     v.responsavelNome?.trim()     || undefined,
      responsavelTelefone: v.responsavelTelefone?.trim() || undefined,
      observacoes:         v.observacoes?.trim()         || undefined,
    };

    const req$ = this.isEditing() && this.editingId()
      ? this.svc.atualizar(this.editingId()!, {
          ...base,
          codigo: this.editingCodigo() || undefined,
          ativo: v.ativo,
        } as ArmazemUpdateDto)
      : this.svc.criar(base);

    req$.subscribe({
      next: () => {
        this.isSaving.set(false);
        this.cancel();
        this.carregarArmazens();
        this.showToast(this.isEditing()
          ? 'Armazém actualizado com sucesso.'
          : 'Armazém criado com sucesso.'
        );
      },
      error: e => { this.errorMsg.set(e.message); this.isSaving.set(false); },
    });
  }

  // ── Modal de confirmação ──────────────────────────────────────────────────

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
          ? 'Armazém desactivado com sucesso.'
          : 'Armazém activado com sucesso.'
        );
      },
      error: e => { this.errorMsg.set(e.message); this.cancelarConfirm(); },
    });
  }

  // ── Helpers ───────────────────────────────────────────────────────────────

  showToast(msg: string): void {
    this.successMsg.set(msg);
    setTimeout(() => this.successMsg.set(null), 3500);
  }

  clearError(): void { this.errorMsg.set(null); }

  formatarTipo(tipo?: string): string {
    const mapa: Record<string, string> = {
      principal:    'Principal',
      secundario:   'Secundário',
      deposito:     'Depósito',
      loja:         'Loja',
      'cross-dock': 'Cross-Dock',
    };
    return tipo ? (mapa[tipo] ?? tipo) : '—';
  }

  tipoBadgeClass(tipo?: string): string {
    const mapa: Record<string, string> = {
      principal:    'tipo-pill--principal',
      secundario:   'tipo-pill--secundario',
      deposito:     'tipo-pill--deposito',
      loja:         'tipo-pill--loja',
      'cross-dock': 'tipo-pill--crossdock',
    };
    return tipo ? (mapa[tipo] ?? '') : '';
  }
}
