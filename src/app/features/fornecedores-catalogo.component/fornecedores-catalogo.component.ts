import {Component, OnInit, OnDestroy, inject, signal, computed,} from '@angular/core';
import { CommonModule } from '@angular/common';
import {ReactiveFormsModule, FormBuilder, FormGroup, Validators, AbstractControl} from '@angular/forms';
import { Subject, debounceTime, distinctUntilChanged, takeUntil } from 'rxjs';
import {FornecedoresCatalogoService, FornecedorModel, FornecedorCreateDto, FornecedorUpdateDto, PagedResult,} from '../../core/services/fornecedores-catalogo.service';

@Component({
  selector: 'app-fornecedores-catalogo',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './fornecedores-catalogo.component.html',
  styleUrls: ['./fornecedores-catalogo.component.css'],
})
export class FornecedoresCatalogoComponent implements OnInit, OnDestroy {
  private readonly svc      = inject(FornecedoresCatalogoService);
  private readonly fb       = inject(FormBuilder);
  private readonly destroy$ = new Subject<void>();

  pagedResult    = signal<PagedResult<FornecedorModel> | null>(null);
  fornecedores   = computed(() => this.pagedResult()?.items ?? []);
  isLoading      = signal(false);
  isSaving       = signal(false);
  errorMsg       = signal<string | null>(null);
  successMsg     = signal<string | null>(null);

  filtroSearch    = '';
  mostrarInativos = false;
  currentPage     = 1;
  readonly pageSize = 20;

  showModal = signal(false);
  isEditing = signal(false);
  editingId = signal<number | null>(null);

  form!: FormGroup;

  private readonly searchInput$ = new Subject<string>();


  ngOnInit(): void {
    this.initForm();

    this.searchInput$
      .pipe(debounceTime(400), distinctUntilChanged(), takeUntil(this.destroy$))
      .subscribe(() => {
        this.currentPage = 1;
        this.carregarFornecedores();
      });

    this.carregarFornecedores();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }


  private initForm(): void {
    this.form = this.fb.group({
      codigo:           ['', [Validators.required, Validators.maxLength(50)]],
      nome:             ['', [Validators.required, Validators.maxLength(200)]],
      nif:              ['', [Validators.maxLength(20),
                              Validators.pattern(/^[A-Za-z0-9\-]{5,20}$/)]],
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
      search:   this.filtroSearch    || undefined,
      ativo:    this.mostrarInativos ? undefined : true,
      page:     this.currentPage,
      pageSize: this.pageSize,
    }).subscribe({
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

  toggleInativos(): void {
    this.mostrarInativos = !this.mostrarInativos;
    this.currentPage = 1;
    this.carregarFornecedores();
  }

  goToPage(page: number): void {
    const total = this.pagedResult()?.totalPages ?? 1;
    if (page < 1 || page > total) return;
    this.currentPage = page;
    this.carregarFornecedores();
  }

  get pages(): number[] {
    const total = this.pagedResult()?.totalPages ?? 0;
    return Array.from({ length: total }, (_, i) => i + 1);
  }


  abrirModalNovo(): void {
    this.isEditing.set(false);
    this.editingId.set(null);
    this.form.reset({ pais: 'Portugal', ativo: true });
    this.errorMsg.set(null);
    this.showModal.set(true);
  }

  abrirModalEditar(f: FornecedorModel): void {
    this.isEditing.set(true);
    this.editingId.set(f.id);
    this.form.patchValue({
      codigo:           f.codigo,
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
    this.showModal.set(true);
  }

  fecharModal(): void {
    this.showModal.set(false);
    this.form.markAsUntouched();
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
      codigo:           v.codigo.trim(),
      nome:             v.nome.trim(),
      nif:              v.nif?.trim()              || undefined,
      telefone:         v.telefone?.trim()          || undefined,
      email:            v.email?.trim()             || undefined,
      morada:           v.morada?.trim()            || undefined,
      localidade:       v.localidade?.trim()        || undefined,
      codigoPostal:     v.codigoPostal?.trim()      || undefined,
      pais:             v.pais?.trim()              || 'Portugal',
      contactoNome:     v.contactoNome?.trim()      || undefined,
      contactoTelefone: v.contactoTelefone?.trim()  || undefined,
      observacoes:      v.observacoes?.trim()       || undefined,
    };

    if (this.isEditing() && this.editingId()) {
      const dto: FornecedorUpdateDto = { ...base, ativo: v.ativo };
      this.svc.atualizar(this.editingId()!, dto).subscribe({
        next: () => this.onSaveSuccess('Fornecedor actualizado com sucesso.'),
        error: (err) => this.onSaveError(err.message),
      });
    } else {
      this.svc.criar(base).subscribe({
        next: () => this.onSaveSuccess('Fornecedor criado com sucesso.'),
        error: (err) => this.onSaveError(err.message),
      });
    }
  }

  desativarFornecedor(f: FornecedorModel): void {
    if (!confirm(`Deseja desactivar o fornecedor "${f.nome}"?`)) return;
    this.svc.deletar(f.id).subscribe({
      next: (res) => { this.carregarFornecedores(); this.showToast(res.message); },
      error: (err) => this.errorMsg.set(err.message),
    });
  }

  ativarFornecedor(f: FornecedorModel): void {
    if (!confirm(`Deseja activar o fornecedor "${f.nome}"?`)) return;
    this.svc.ativar(f.id).subscribe({
      next: () => { this.carregarFornecedores(); this.showToast('Fornecedor activado com sucesso.'); },
      error: (err) => this.errorMsg.set(err.message),
    });
  }

  private onSaveSuccess(msg: string): void {
    this.isSaving.set(false);
    this.fecharModal();
    this.carregarFornecedores();
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
}
