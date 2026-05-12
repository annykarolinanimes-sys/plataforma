import { Component, OnInit, OnDestroy, inject, signal, computed } from '@angular/core';
import { CommonModule} from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup,Validators, AbstractControl} from '@angular/forms';
import { Router } from '@angular/router';
import { Subject, debounceTime, distinctUntilChanged, takeUntil } from 'rxjs';
import { ClientesCatalogoService, ClienteModel,ClienteCreateDto, ClienteUpdateDto, PagedResult} from '../../core/services/clientes-catalogo.service';
import { PdfService, PdfField } from '../../core/services/pdf.service';
import { UiStateService } from '../../core/services/ui-state.service';

const NIF_PT_REGEX   = /^\d{9}$/;
const NIF_INTL_REGEX = /^[A-Za-z0-9\-]{5,20}$/;
const NIF_REGEX      = new RegExp(`(${NIF_PT_REGEX.source})|(${NIF_INTL_REGEX.source})`);

@Component({
  selector: 'app-clientes-catalogo',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './clientes-catalogo.component.html',
  styleUrls: ['./clientes-catalogo.component.css'],
})
export class ClientesCatalogoComponent implements OnInit, OnDestroy {

  private readonly svc        = inject(ClientesCatalogoService);
  private readonly fb         = inject(FormBuilder);
  private readonly pdfService = inject(PdfService);
  private readonly router     = inject(Router);
  readonly uiState            = inject(UiStateService);
  private readonly destroy$   = new Subject<void>();

  currentState = this.uiState.currentClienteState;
  editingId    = this.uiState.currentClienteId;
  isEditing    = computed(() => this.currentState() === 'edit');
  isViewing    = computed(() => this.currentState() === 'details');

  pagedResult = signal<PagedResult<ClienteModel> | null>(null);
  clientes    = computed(() => this.pagedResult()?.items ?? []);
  isLoading   = signal(false);
  isSaving    = signal(false);
  errorMsg    = signal<string | null>(null);
  successMsg  = signal<string | null>(null);

  totalClientes  = computed(() => this.totalClientesGeral());
  totalAtivos    = computed(() => this.totalAtivosGeral());
  totalInativos  = computed(() => this.totalInativosGeral());
  novosEsteMes   = computed(() => {
    const agora   = new Date();
    const mesAtual = agora.getMonth();
    const anoAtual = agora.getFullYear();
    return this.clientes().filter(c => {
      const d = new Date(c.criadoEm);
      return d.getMonth() === mesAtual && d.getFullYear() === anoAtual;
    }).length;
  });

  // Signals para totais do servidor (independentes de filtros)
  private totalClientesGeral  = signal<number>(0);
  private totalAtivosGeral    = signal<number>(0);
  private totalInativosGeral  = signal<number>(0);

  filtroSearch    = '';
  mostrarInativos = false;
  currentPage     = 1;
  readonly pageSize = 20;

  form!: FormGroup;
  private readonly searchInput$ = new Subject<string>();

  ngOnInit(): void {
    this.initForm();
    this.uiState.goToClienteList();
    this.carregarTotaisGerais();

    this.searchInput$
      .pipe(debounceTime(300), distinctUntilChanged(), takeUntil(this.destroy$))
      .subscribe(() => { this.currentPage = 1; this.carregarClientes(); });

    this.carregarClientes();
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
      contribuinte:     ['', [
        Validators.maxLength(20),
        Validators.pattern(NIF_REGEX),
      ]],
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

  private _patchCliente(cliente: ClienteModel): void {
    this.form.patchValue({
      codigo:           cliente.codigo,
      criadoEm:         new Date(cliente.criadoEm).toLocaleString('pt-PT'),
      nome:             cliente.nome,
      contribuinte:     cliente.contribuinte     ?? '',
      telefone:         cliente.telefone         ?? '',
      email:            cliente.email            ?? '',
      morada:           cliente.morada           ?? '',
      localidade:       cliente.localidade       ?? '',
      codigoPostal:     cliente.codigoPostal     ?? '',
      pais:             cliente.pais             ?? 'Portugal',
      contactoNome:     cliente.contactoNome     ?? '',
      contactoTelefone: cliente.contactoTelefone ?? '',
      observacoes:      cliente.observacoes      ?? '',
      ativo:            cliente.ativo,
    });
    this.errorMsg.set(null);
  }

  private carregarTotaisGerais(): void {
    // Carregar totais: total geral e total de inativos
    // Primeira chamada: total de ativos
    this.svc.listar({ page: 1, pageSize: 1, ativo: true }).subscribe({
      next: (result) => {
        const ativos = result.total;
        // Segunda chamada: total geral (sem filtro)
        this.svc.listar({ page: 1, pageSize: 1, ativo: undefined }).subscribe({
          next: (allResult) => {
            const totalGeral = allResult.total;
            const inativos = totalGeral - ativos;
            this.totalClientesGeral.set(totalGeral);
            this.totalAtivosGeral.set(ativos);
            this.totalInativosGeral.set(inativos);
          },
          error: () => {
            // Fallback: usar apenas resultados da primeira chamada
            this.totalClientesGeral.set(ativos);
            this.totalAtivosGeral.set(ativos);
            this.totalInativosGeral.set(0);
          }
        });
      },
      error: () => {
        this.totalClientesGeral.set(0);
        this.totalAtivosGeral.set(0);
        this.totalInativosGeral.set(0);
      }
    });
  }

  carregarClientes(): void {
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
    this.carregarClientes();
  }

  goToPage(page: number): void {
    const total = this.pagedResult()?.totalPages ?? 1;
    if (page < 1 || page > total) return;
    this.currentPage = page;
    this.carregarClientes();
  }

  get pages(): number[] {
    return Array.from({ length: this.pagedResult()?.totalPages ?? 0 }, (_, i) => i + 1);
  }

  goToCreate(): void {
    this.form.reset({ pais: 'Portugal', ativo: true });
    this.errorMsg.set(null);
    this.uiState.goToClienteCreate();
  }

  goToEdit(cliente: ClienteModel): void {
    this._patchCliente(cliente);
    this.uiState.goToClienteEdit(cliente.id);
  }

  goToDetails(cliente: ClienteModel, event?: Event): void {
    if (event) event.stopPropagation();
    this._patchCliente(cliente);
    this.uiState.goToClienteDetails(cliente.id);
  }

  cancel(): void {
    this.uiState.goToClienteList();
    this.form.markAsUntouched();
  }


  imprimirPdf(cliente: ClienteModel, event?: Event): void {
    if (event) event.stopPropagation();
    const fields: PdfField[] = [
      { label: 'Código',               value: cliente.codigo },
      { label: 'Nome',                 value: cliente.nome },
      { label: 'Contribuinte',         value: cliente.contribuinte        || '—' },
      { label: 'Telefone',             value: cliente.telefone            || '—' },
      { label: 'Email',                value: cliente.email               || '—' },
      { label: 'Morada',               value: cliente.morada              || '—' },
      { label: 'Localidade',           value: cliente.localidade          || '—' },
      { label: 'Código Postal',        value: cliente.codigoPostal        || '—' },
      { label: 'País',                 value: cliente.pais                || '—' },
      { label: 'Contacto',             value: cliente.contactoNome        || '—' },
      { label: 'Telefone do Contacto', value: cliente.contactoTelefone    || '—' },
      { label: 'Ativo',                value: cliente.ativo ? 'Sim' : 'Não' },
    ];
    try {
      const blob = this.pdfService.generateEntityPdf(
        `Cliente ${cliente.codigo}`, fields, 'Documento gerado automaticamente.'
      );
      this.pdfService.downloadPdf(blob, `Cliente_${cliente.codigo || cliente.id}.pdf`);
    } catch {
      this.errorMsg.set('Erro ao gerar PDF do cliente.');
    }
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
      contribuinte:     v.contribuinte?.trim()     || undefined,
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

    if (this.isEditing() && this.editingId()) {
      const dto: ClienteUpdateDto = { ...base, ativo: v.ativo };
      this.svc.atualizar(this.editingId()!, dto).subscribe({
        next:  () => this._onSaveSuccess('Cliente actualizado com sucesso.'),
        error: e  => this._onSaveError(e.message),
      });
    } else {
      const dto: ClienteCreateDto = base;
      this.svc.criar(dto).subscribe({
        next:  () => this._onSaveSuccess('Cliente criado com sucesso.'),
        error: e  => this._onSaveError(e.message),
      });
    }
  }

  desativarCliente(cliente: ClienteModel): void {
    if (!confirm(`Deseja desactivar o cliente "${cliente.nome}"?`)) return;
    this.svc.deletar(cliente.id).subscribe({
      next:  r   => { this.carregarClientes(); this.showToast(r.message); },
      error: err => this.errorMsg.set(err.message),
    });
  }

  ativarCliente(cliente: ClienteModel): void {
    if (!confirm(`Deseja activar o cliente "${cliente.nome}"?`)) return;
    this.svc.ativar(cliente.id).subscribe({
      next:  () => { this.carregarClientes(); this.showToast('Cliente activado com sucesso.'); },
      error: err => this.errorMsg.set(err.message),
    });
  }

  private _onSaveSuccess(msg: string): void {
    this.isSaving.set(false);
    this.uiState.goToClienteList();
    this.carregarClientes();
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
