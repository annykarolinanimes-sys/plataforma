import { Component, OnInit,OnDestroy, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule,FormBuilder,FormGroup, Validators, AbstractControl} from '@angular/forms';
import { Subject, debounceTime, distinctUntilChanged, takeUntil } from 'rxjs';
import { ClientesCatalogoService, ClienteModel, ClienteCreateDto, ClienteUpdateDto, PagedResult} from '../../core/services/clientes-catalogo.service';

@Component({
  selector: 'app-clientes-catalogo',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './clientes-catalogo.component.html',
  styleUrls: ['./clientes-catalogo.component.css'],
})
export class ClientesCatalogoComponent implements OnInit, OnDestroy {
  private readonly svc     = inject(ClientesCatalogoService);
  private readonly fb      = inject(FormBuilder);
  private readonly destroy$ = new Subject<void>();

  pagedResult   = signal<PagedResult<ClienteModel> | null>(null);
  clientes      = computed(() => this.pagedResult()?.items ?? []);
  isLoading     = signal(false);
  isSaving      = signal(false);
  errorMsg      = signal<string | null>(null);
  successMsg    = signal<string | null>(null);

  filtroSearch    = '';
  mostrarInativos = false;
  currentPage     = 1;
  readonly pageSize = 20;

  showModal  = signal(false);
  isEditing  = signal(false);
  editingId  = signal<number | null>(null);

  form!: FormGroup;

  isLoadingCode = signal(false);

  private readonly searchInput$ = new Subject<string>();


  ngOnInit(): void {
    this.initForm();

    this.searchInput$
      .pipe(debounceTime(300), distinctUntilChanged(), takeUntil(this.destroy$))
      .subscribe(() => {
        this.currentPage = 1;
        this.carregarClientes();
      });

    this.carregarClientes();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }


  private initForm(): void {
    this.form = this.fb.group({
      codigo:           [{ value: ''}],
      nome:             ['', [Validators.required, Validators.maxLength(200)]],
      contribuinte:     ['', [Validators.maxLength(20),
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


  carregarClientes(): void {
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
    this.carregarClientes();
  }

  goToPage(page: number): void {
    const total = this.pagedResult()?.totalPages ?? 1;
    if (page < 1 || page > total) return;
    this.currentPage = page;
    this.carregarClientes();
  }

  get pages(): number[] {
    const total = this.pagedResult()?.totalPages ?? 0;
    return Array.from({ length: total }, (_, i) => i + 1);
  }


  abrirModalNovo(): void {
    this.isEditing.set(false);
    this.editingId.set(null);
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
        this.errorMsg.set('Erro ao gerar código automático.');
      }
    });

    this.showModal.set(true);
  }

  abrirModalEditar(cliente: ClienteModel): void {
    this.isEditing.set(true);
    this.editingId.set(cliente.id);
    this.form.patchValue({
      codigo:           cliente.codigo,
      nome:             cliente.nome,
      contribuinte:     cliente.contribuinte ?? '',
      telefone:         cliente.telefone ?? '',
      email:            cliente.email ?? '',
      morada:           cliente.morada ?? '',
      localidade:       cliente.localidade ?? '',
      codigoPostal:     cliente.codigoPostal ?? '',
      pais:             cliente.pais ?? 'Portugal',
      contactoNome:     cliente.contactoNome ?? '',
      contactoTelefone: cliente.contactoTelefone ?? '',
      observacoes:      cliente.observacoes ?? '',
      ativo:            cliente.ativo,
    });
    this.form.get('codigo')?.disable();
    this.errorMsg.set(null);
    this.showModal.set(true);
  }

  fecharModal(): void {
    this.showModal.set(false);
    this.resetForm();
    this.form.markAsUntouched();
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

  salvarCliente(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid) {
      this.errorMsg.set('Corrija os erros no formulário antes de continuar.');
      return;
    }

    this.isSaving.set(true);
    this.errorMsg.set(null);
    const v = this.form.getRawValue();

    const base = {
      nome:             v.nome.trim(),
      contribuinte:     v.contribuinte?.trim() || undefined,
      telefone:         v.telefone?.trim()     || undefined,
      email:            v.email?.trim()        || undefined,
      morada:           v.morada?.trim()       || undefined,
      localidade:       v.localidade?.trim()   || undefined,
      codigoPostal:     v.codigoPostal?.trim() || undefined,
      pais:             v.pais?.trim()         || 'Portugal',
      contactoNome:     v.contactoNome?.trim() || undefined,
      contactoTelefone: v.contactoTelefone?.trim() || undefined,
      observacoes:      v.observacoes?.trim()  || undefined,
    };

    if (this.isEditing() && this.editingId()) {
      const dto: ClienteUpdateDto = { ...base, codigo: v.codigo.trim(), ativo: v.ativo };
      this.svc.atualizar(this.editingId()!, dto).subscribe({
        next: () => this.onSaveSuccess('Cliente actualizado com sucesso.'),
        error: (err) => this.onSaveError(err.message),
      });
    } else {
      const dto: ClienteCreateDto = base;
      this.svc.criar(dto).subscribe({
        next: (response) => {
          // Mostrar o código gerado na mensagem de sucesso
          const msg = response?.codigo 
            ? `Cliente criado com sucesso. Código: ${response.codigo}` 
            : 'Cliente criado com sucesso.';
          this.onSaveSuccess(msg);
        },
        error: (err) => this.onSaveError(err.message),
      });
    }
  }

  desativarCliente(cliente: ClienteModel): void {
    if (!confirm(`Deseja desactivar o cliente "${cliente.nome}"?`)) return;
    this.svc.deletar(cliente.id).subscribe({
      next: (res) => { this.carregarClientes(); this.showToast(res.message); },
      error: (err) => this.errorMsg.set(err.message),
    });
  }

  ativarCliente(cliente: ClienteModel): void {
    if (!confirm(`Deseja activar o cliente "${cliente.nome}"?`)) return;
    this.svc.ativar(cliente.id).subscribe({
      next: () => { this.carregarClientes(); this.showToast('Cliente activado com sucesso.'); },
      error: (err) => this.errorMsg.set(err.message),
    });
  }


  private onSaveSuccess(msg: string): void {
    this.isSaving.set(false);
    this.fecharModal();
    this.carregarClientes();
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
