import { Component, OnInit, OnDestroy, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators, AbstractControl } from '@angular/forms';
import { Subject, debounceTime, distinctUntilChanged, takeUntil } from 'rxjs';
import { FornecedoresCatalogoService, FornecedorModel, FornecedorCreateDto, FornecedorUpdateDto, PagedResult } from '../../core/services/fornecedores-catalogo.service';
import { UiStateService } from '../../core/services/ui-state.service';

@Component({
  selector: 'app-fornecedores-catalogo',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './fornecedores-catalogo.component.html',
  styleUrls: ['./fornecedores-catalogo.component.css'],
})
export class FornecedoresCatalogoComponent implements OnInit, OnDestroy {
  private readonly svc = inject(FornecedoresCatalogoService);
  private readonly fb = inject(FormBuilder);
  private readonly uiState = inject(UiStateService);
  private readonly destroy$ = new Subject<void>();

  currentState = this.uiState.currentFornecedorState;
  editingId = this.uiState.currentFornecedorId;

  pagedResult = signal<PagedResult<FornecedorModel> | null>(null);
  fornecedores = computed(() => this.pagedResult()?.items ?? []);
  isLoading = signal(false);
  isSaving = signal(false);
  errorMsg = signal<string | null>(null);
  successMsg = signal<string | null>(null);

  filtroSearch = '';
  mostrarInativos = false;
  currentPage = 1;
  readonly pageSize = 20;

  form!: FormGroup;

  isLoadingCode = signal(false);
  showDeleteConfirm = signal(false);
  fornecedorParaDelete = signal<FornecedorModel | null>(null);

  private readonly searchInput$ = new Subject<string>();

  totalFornecedores = computed(() => this.pagedResult()?.total ?? 0);
  totalPages = computed(() => this.pagedResult()?.totalPages ?? 0);
  pages = computed(() => Array.from({ length: this.totalPages() }, (_, i) => i + 1));

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
      codigo: [{ value: ''}],
      nome: ['', [Validators.required, Validators.maxLength(200)]],
      nif: ['', [Validators.maxLength(20), Validators.pattern(/^[A-Za-z0-9\-]{5,20}$/)]],
      telefone: ['', Validators.maxLength(30)],
      email: ['', [Validators.maxLength(200), Validators.email]],
      morada: ['', Validators.maxLength(300)],
      localidade: ['', Validators.maxLength(100)],
      codigoPostal: ['', Validators.maxLength(20)],
      pais: ['Portugal', Validators.maxLength(100)],
      contactoNome: ['', Validators.maxLength(150)],
      contactoTelefone: ['', Validators.maxLength(30)],
      observacoes: [''],
      ativo: [true],
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
      search: this.filtroSearch || undefined,
      ativo: this.mostrarInativos ? undefined : true,
      page: this.currentPage,
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
    const total = this.totalPages();
    if (page < 1 || page > total) return;
    this.currentPage = page;
    this.carregarFornecedores();
  }

goToCreate(): void {
  this.resetForm();
  this.form.get('codigo')?.setValue('A carregar...');
  this.form.get('codigo')?.disable();
  this.isLoadingCode.set(true);

  this.svc.obterProximoCodigo().subscribe({
    next: (res) => {
      this.form.get('codigo')?.setValue(res.codigo);
      this.form.get('codigo')?.disable(); 
      this.isLoadingCode.set(false);
    },
    error: () => {
      this.form.get('codigo')?.setValue('');
      this.form.get('codigo')?.enable();
      this.isLoadingCode.set(false);
      this.showToast('Erro ao gerar código automático.');
    }
  });

  this.uiState.goToFornecedorCreate();
}

  goToEdit(fornecedor: FornecedorModel, event?: Event): void {
    if (event) event.stopPropagation();
    this.carregarDadosParaEdicao(fornecedor);
    this.form.get('codigo')?.disable();
    this.uiState.goToFornecedorEdit(fornecedor.id);
  }

  goToList(): void {
    this.uiState.goToFornecedorList();
    this.resetForm();
    this.carregarFornecedores();
  }

  cancel(): void {
    this.goToList();
  }

  private resetForm(): void {
    this.form.reset({ 
      pais: 'Portugal', 
      ativo: true, 
      codigo: '' 
    });
    this.form.get('codigo')?.enable();
    this.isLoadingCode.set(false);
    this.errorMsg.set(null);
  }

  private carregarDadosParaEdicao(f: FornecedorModel): void {
    this.form.patchValue({
      codigo: f.codigo,
      nome: f.nome,
      nif: f.nif ?? '',
      telefone: f.telefone ?? '',
      email: f.email ?? '',
      morada: f.morada ?? '',
      localidade: f.localidade ?? '',
      codigoPostal: f.codigoPostal ?? '',
      pais: f.pais ?? 'Portugal',
      contactoNome: f.contactoNome ?? '',
      contactoTelefone: f.contactoTelefone ?? '',
      observacoes: f.observacoes ?? '',
      ativo: f.ativo,
    });
    this.errorMsg.set(null);
  }

  salvarFornecedor(): void {
    this.form.markAllAsTouched();
    
    const nomeControl = this.form.get('nome');
    if (nomeControl?.invalid) {
      this.errorMsg.set('Nome do fornecedor é obrigatório.');
      return;
    }

    this.isSaving.set(true);
    this.errorMsg.set(null);
    
    const rawValue = this.form.getRawValue();

    const base: FornecedorCreateDto = {
      nome: rawValue.nome?.trim(),
      nif: rawValue.nif?.trim() || undefined,
      telefone: rawValue.telefone?.trim() || undefined,
      email: rawValue.email?.trim() || undefined,
      morada: rawValue.morada?.trim() || undefined,
      localidade: rawValue.localidade?.trim() || undefined,
      codigoPostal: rawValue.codigoPostal?.trim() || undefined,
      pais: rawValue.pais?.trim() || 'Portugal',
      contactoNome: rawValue.contactoNome?.trim() || undefined,
      contactoTelefone: rawValue.contactoTelefone?.trim() || undefined,
      observacoes: rawValue.observacoes?.trim() || undefined,
    };

    if (this.uiState.isFornecedorEdit() && this.editingId()) {
      const dto: FornecedorUpdateDto = { 
        codigo: rawValue.codigo?.trim() || '',
        nome: rawValue.nome?.trim(),
        nif: rawValue.nif?.trim() || undefined,
        telefone: rawValue.telefone?.trim() || undefined,
        email: rawValue.email?.trim() || undefined,
        morada: rawValue.morada?.trim() || undefined,
        localidade: rawValue.localidade?.trim() || undefined,
        codigoPostal: rawValue.codigoPostal?.trim() || undefined,
        pais: rawValue.pais?.trim() || 'Portugal',
        contactoNome: rawValue.contactoNome?.trim() || undefined,
        contactoTelefone: rawValue.contactoTelefone?.trim() || undefined,
        observacoes: rawValue.observacoes?.trim() || undefined,
        ativo: rawValue.ativo === true || rawValue.ativo === 'true'
      };
      this.svc.atualizar(this.editingId()!, dto).subscribe({
        next: () => this.onSaveSuccess('Fornecedor actualizado com sucesso.'),
        error: (err) => this.onSaveError(err.message),
      });
    } else {
      this.svc.criar(base).subscribe({
        next: (response) => {
          // Mostrar o código gerado na mensagem de sucesso
          const msg = response?.codigo 
            ? `Fornecedor criado com sucesso. Código: ${response.codigo}` 
            : 'Fornecedor criado com sucesso.';
          this.showToast(msg);
          this.onSaveSuccess(msg);
        },
        error: (err) => this.onSaveError(err.message),
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
      next: () => {
        this.cancelarDelete();
        this.carregarFornecedores();
        this.showToast('Fornecedor desactivado com sucesso');
      },
      error: (err) => { this.errorMsg.set(err.message); this.cancelarDelete(); }
    });
  }

  ativarFornecedor(fornecedor: FornecedorModel): void {
    if (!confirm(`Deseja activar o fornecedor "${fornecedor.nome}"?`)) return;
    this.svc.ativar(fornecedor.id).subscribe({
      next: () => {
        this.carregarFornecedores();
        this.showToast('Fornecedor activado com sucesso.');
      },
      error: (err) => this.errorMsg.set(err.message),
    });
  }

  private onSaveSuccess(msg: string): void {
    this.isSaving.set(false);
    this.goToList();
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