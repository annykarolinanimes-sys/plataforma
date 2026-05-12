import { Component, OnInit, OnDestroy, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  ReactiveFormsModule, FormBuilder, FormGroup,
  Validators, AbstractControl
} from '@angular/forms';
import { Router } from '@angular/router';
import { Subject, debounceTime, distinctUntilChanged, takeUntil } from 'rxjs';

import {
  FornecedoresCatalogoService, FornecedorModel,
  FornecedorCreateDto, FornecedorUpdateDto, PagedResult
} from '../../core/services/fornecedores-catalogo.service';
import { UiStateService } from '../../core/services/ui-state.service';
import { PdfService, PdfField } from '../../core/services/pdf.service';

const NIF_REGEX = /^(\d{9}|[A-Za-z0-9\-]{5,20})$/;

@Component({
  selector: 'app-fornecedores-catalogo',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './fornecedores-catalogo.component.html',
  styleUrls: ['./fornecedores-catalogo.component.css'],
})
export class FornecedoresCatalogoComponent implements OnInit, OnDestroy {

  private readonly svc        = inject(FornecedoresCatalogoService);
  private readonly fb         = inject(FormBuilder);
  readonly uiState            = inject(UiStateService);
  private readonly pdfService = inject(PdfService);
  private readonly router     = inject(Router);
  private readonly destroy$   = new Subject<void>();

  currentState = this.uiState.currentFornecedorState;
  editingId    = this.uiState.currentFornecedorId;
  isViewing    = signal(false);   
  isListView()   { return this.currentState() === 'list';   }
  isCreateView() { return this.currentState() === 'create'; }
  isEditView()   { return this.currentState() === 'edit';   }

  pagedResult  = signal<PagedResult<FornecedorModel> | null>(null);
  fornecedores = computed(() => this.pagedResult()?.items ?? []);
  isLoading    = signal(false);
  isSaving     = signal(false);
  errorMsg     = signal<string | null>(null);
  successMsg   = signal<string | null>(null);

  totalFornecedores = computed(() => this.pagedResult()?.total ?? 0);
  totalAtivos       = computed(() => this.fornecedores().filter(f => f.ativo).length);
  totalInativos     = computed(() => this.fornecedores().filter(f => !f.ativo).length);
  localidadesDistintas = computed(() => {
    const locs = this.fornecedores()
      .map(f => f.localidade)
      .filter(Boolean) as string[];
    return new Set(locs).size;
  });

  filtroSearch    = '';
  mostrarInativos = false;
  currentPage     = 1;
  readonly pageSize = 20;

  totalPages = computed(() => this.pagedResult()?.totalPages ?? 0);
  pages      = computed(() =>
    Array.from({ length: this.totalPages() }, (_, i) => i + 1)
  );

  showDeleteConfirm    = signal(false);
  fornecedorParaDelete = signal<FornecedorModel | null>(null);

  form!: FormGroup;
  private readonly searchInput$ = new Subject<string>();


  ngOnInit(): void {
    this.initForm();

    this.searchInput$
      .pipe(debounceTime(400), distinctUntilChanged(), takeUntil(this.destroy$))
      .subscribe(() => { this.currentPage = 1; this.carregarFornecedores(); });

    this.carregarFornecedores();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }


  private initForm(): void {
    this.form = this.fb.group({
      codigo:           [{ value: '', disabled: true }],
      criadoEm:         [{ value: '', disabled: true }],  
      nome:             ['', [Validators.required, Validators.maxLength(200)]],
      nif:              ['', [Validators.maxLength(20), Validators.pattern(NIF_REGEX)]],
      telefone:         ['', Validators.maxLength(30)],
      email:            ['', [Validators.maxLength(200), Validators.email]],
      morada:           ['', Validators.maxLength(300)],
      localidade:       ['', Validators.maxLength(100)],
      codigoPostal:     ['', Validators.maxLength(20)],
      pais:             ['Portugal', Validators.maxLength(100)],
      contactoNome:     ['', Validators.maxLength(150)],
      contactoTelefone: ['', Validators.maxLength(30)],
      observacoes:      [''],
      ativo:            [true],
    });
  }

  ctrl(name: string): AbstractControl { return this.form.get(name)!; }

  hasError(name: string, error?: string): boolean {
    const c = this.ctrl(name);
    if (!c.invalid || !c.touched) return false;
    return error ? c.hasError(error) : true;
  }


  carregarFornecedores(): void {
    this.isLoading.set(true);
    this.svc.listar({
      search:   this.filtroSearch || undefined,
      ativo:    this.mostrarInativos ? undefined : true,
      page:     this.currentPage,
      pageSize: this.pageSize,
    }).subscribe({
      next:  r   => { this.pagedResult.set(r); this.isLoading.set(false); },
      error: err => { this.errorMsg.set(err.message); this.isLoading.set(false); },
    });
  }

  onSearchChange(value: string): void {
    this.filtroSearch = value;
    this.searchInput$.next(value);
  }

  toggleInativos(): void {
    this.mostrarInativos = !this.mostrarInativos;
    this.currentPage = 1;
    this.carregarFornecedores();
  }

  goToPage(page: number): void {
    const total = this.totalPages();
    if (page < 1 || page > total) return;
    this.currentPage = page;
    this.carregarFornecedores();
  }

  private _patchFornecedor(f: FornecedorModel): void {
    this.form.patchValue({
      codigo:           f.codigo,
      criadoEm:         new Date(f.criadoEm).toLocaleDateString('pt-PT', {
                          day: '2-digit', month: '2-digit', year: 'numeric',
                          hour: '2-digit', minute: '2-digit'
                        }),
      nome:             f.nome,
      nif:              f.nif              ?? '',
      telefone:         f.telefone         ?? '',
      email:            f.email            ?? '',
      morada:           f.morada           ?? '',
      localidade:       f.localidade       ?? '',
      codigoPostal:     f.codigoPostal     ?? '',
      pais:             f.pais             ?? 'Portugal',
      contactoNome:     f.contactoNome     ?? '',
      contactoTelefone: f.contactoTelefone ?? '',
      observacoes:      f.observacoes      ?? '',
      ativo:            f.ativo,
    });
    this.errorMsg.set(null);
  }


  goToCreate(): void {
    this.isViewing.set(false);
    this._resetForm();
    this.uiState.goToFornecedorCreate();
  }

  goToEdit(fornecedor: FornecedorModel, event?: Event): void {
    if (event) event.stopPropagation();
    this.isViewing.set(false);
    this._patchFornecedor(fornecedor);
    this.uiState.goToFornecedorEdit(fornecedor.id);
  }

  goToDetails(fornecedor: FornecedorModel, event?: Event): void {
    if (event) event.stopPropagation();
    this.isViewing.set(true);
    this._patchFornecedor(fornecedor);
    this.uiState.goToFornecedorEdit(fornecedor.id);
  }

  goToList(): void {
    this.isViewing.set(false);
    this.uiState.goToFornecedorList();
    this._resetForm();
    this.carregarFornecedores();
  }

  cancel(): void { this.goToList(); }


  imprimirPdf(fornecedor: FornecedorModel, event?: Event): void {
    if (event) event.stopPropagation();
    const fields: PdfField[] = [
      { label: 'Código',               value: fornecedor.codigo },
      { label: 'Nome',                 value: fornecedor.nome },
      { label: 'NIF',                  value: fornecedor.nif              || '—' },
      { label: 'Telefone',             value: fornecedor.telefone         || '—' },
      { label: 'Email',                value: fornecedor.email            || '—' },
      { label: 'Morada',               value: fornecedor.morada           || '—' },
      { label: 'Localidade',           value: fornecedor.localidade       || '—' },
      { label: 'Código Postal',        value: fornecedor.codigoPostal     || '—' },
      { label: 'País',                 value: fornecedor.pais             || '—' },
      { label: 'Contacto',             value: fornecedor.contactoNome     || '—' },
      { label: 'Telefone do Contacto', value: fornecedor.contactoTelefone || '—' },
      { label: 'Ativo',                value: fornecedor.ativo ? 'Sim' : 'Não' },
    ];
    try {
      const blob = this.pdfService.generateEntityPdf(
        `Fornecedor ${fornecedor.codigo}`, fields, 'Documento gerado automaticamente.'
      );
      this.pdfService.downloadPdf(blob, `Fornecedor_${fornecedor.codigo || fornecedor.id}.pdf`);
    } catch {
      this.errorMsg.set('Erro ao gerar PDF do fornecedor.');
    }
  }


  salvarFornecedor(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid) {
      this.errorMsg.set('Corrija os erros no formulário antes de continuar.');
      return;
    }

    this.isSaving.set(true);
    this.errorMsg.set(null);
    const v = this.form.getRawValue();

    const base: FornecedorCreateDto = {
      nome:             v.nome.trim(),
      nif:              v.nif?.trim()              || undefined,
      telefone:         v.telefone?.trim()         || undefined,
      email:            v.email?.trim()            || undefined,
      morada:           v.morada?.trim()           || undefined,
      localidade:       v.localidade?.trim()       || undefined,
      codigoPostal:     v.codigoPostal?.trim()     || undefined,
      pais:             v.pais?.trim()             || 'Portugal',
      contactoNome:     v.contactoNome?.trim()     || undefined,
      contactoTelefone: v.contactoTelefone?.trim() || undefined,
      observacoes:      v.observacoes?.trim()      || undefined,
    };

    if (this.uiState.isFornecedorEdit() && this.editingId()) {
      const dto: FornecedorUpdateDto = { ...base, ativo: v.ativo };
      this.svc.atualizar(this.editingId()!, dto).subscribe({
        next:  () => this._onSaveSuccess('Fornecedor actualizado com sucesso.'),
        error: e  => this._onSaveError(e.message),
      });
    } else {
      this.svc.criar(base).subscribe({
        next:  () => this._onSaveSuccess('Fornecedor criado com sucesso.'),
        error: e  => this._onSaveError(e.message),
      });
    }
  }


  confirmarDesativar(fornecedor: FornecedorModel, event?: Event): void {
    if (event) event.stopPropagation();
    this.fornecedorParaDelete.set(fornecedor);
    this.showDeleteConfirm.set(true);
  }

  cancelarDelete(): void {
    this.showDeleteConfirm.set(false);
    this.fornecedorParaDelete.set(null);
  }

  executarDesativar(): void {
    const f = this.fornecedorParaDelete();
    if (!f) return;
    this.svc.deletar(f.id).subscribe({
      next:  () => {
        this.cancelarDelete();
        this.carregarFornecedores();
        this.showToast('Fornecedor desactivado com sucesso.');
      },
      error: err => { this.errorMsg.set(err.message); this.cancelarDelete(); },
    });
  }

  ativarFornecedor(fornecedor: FornecedorModel): void {
    if (!confirm(`Deseja activar o fornecedor "${fornecedor.nome}"?`)) return;
    this.svc.ativar(fornecedor.id).subscribe({
      next:  () => { this.carregarFornecedores(); this.showToast('Fornecedor activado com sucesso.'); },
      error: err => this.errorMsg.set(err.message),
    });
  }

  private _resetForm(): void {
    this.form.reset({ pais: 'Portugal', ativo: true });
    this.errorMsg.set(null);
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
}
