import { Component, OnInit, OnDestroy, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators, AbstractControl, FormArray } from '@angular/forms';
import { Subject, debounceTime, distinctUntilChanged, takeUntil } from 'rxjs';
import { AtribuicaoService, Atribuicao, PagedResult, AtribuicaoCreateDto, AtribuicaoUpdateDto } from '../../core/services/atribuicao.service';
import { UiStateService } from '../../core/services/ui-state.service';

@Component({
  selector: 'app-atribuicao',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './atribuicao.component.html',
  styleUrls: ['./atribuicao.component.css']
})
export class AtribuicaoComponent implements OnInit, OnDestroy {
  private readonly svc = inject(AtribuicaoService);
  private readonly fb = inject(FormBuilder);
  private readonly uiState = inject(UiStateService);
  private readonly destroy$ = new Subject<void>();

  currentState = this.uiState.currentAtribuicaoState;
  editingId = this.uiState.currentAtribuicaoId;

  pagedResult = signal<PagedResult<Atribuicao> | null>(null);
  atribuicoes = computed(() => this.pagedResult()?.items ?? []);
  isLoading = signal(false);
  isSaving = signal(false);
  errorMsg = signal<string | null>(null);
  successMsg = signal<string | null>(null);

  filtroStatus = '';
  filtroSearch = '';
  currentPage = 1;
  readonly pageSize = 15;

  form!: FormGroup;

  showDeleteConfirm = signal(false);
  atribuicaoParaDelete = signal<Atribuicao | null>(null);

  private readonly searchInput$ = new Subject<string>();

  readonly prioridades = ['Baixa', 'Media', 'Alta', 'Urgente'];
  readonly statusList = ['Pendente', 'EmProgresso', 'Concluida', 'Cancelada'];

  totalAtribuicoes = computed(() => this.pagedResult()?.total ?? 0);
  totalPages = computed(() => this.pagedResult()?.totalPages ?? 0);
  pages = computed(() => Array.from({ length: this.totalPages() }, (_, i) => i + 1));

  ngOnInit(): void {
    this.initForm();

    this.searchInput$
      .pipe(debounceTime(400), distinctUntilChanged(), takeUntil(this.destroy$))
      .subscribe(() => {
        this.currentPage = 1;
        this.carregarAtribuicoes();
      });

    this.carregarAtribuicoes();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private initForm(): void {
    this.form = this.fb.group({
      clienteNome: ['', [Validators.required, Validators.maxLength(200)]],
      clienteContacto: ['', Validators.maxLength(100)],
      enderecoOrigem: ['', Validators.maxLength(300)],
      enderecoDestino: ['', [Validators.required, Validators.maxLength(300)]],
      dataPrevistaInicio: [''],
      dataPrevistaFim: [''],
      prioridade: ['Media', Validators.required],
      observacoes: ['', Validators.maxLength(500)],
      motoristaId: [null],
      veiculoId: [null],
      transportadoraId: [null],
      rotaId: [null],
      ajudanteIds: [[]],
      distanciaTotalKm: [0, [Validators.min(0)]],
      tempoEstimadoHoras: [null, [Validators.min(0)]],
      entregas: this.fb.array([])
    });
    this.adicionarEntrega();
  }

  get entregasArray(): FormArray {
    return this.form.get('entregas') as FormArray;
  }

  criarEntregaForm(entrega?: any): FormGroup {
    return this.fb.group({
      destinatario: [entrega?.destinatario ?? '', Validators.maxLength(200)],
      endereco: [entrega?.endereco ?? '', Validators.maxLength(300)],
      contacto: [entrega?.contacto ?? '', Validators.maxLength(100)],
      observacoes: [entrega?.observacoes ?? '', Validators.maxLength(500)],
      ordem: [entrega?.ordem ?? this.entregasArray.length + 1, Validators.min(0)]
    });
  }

  adicionarEntrega(): void {
    this.entregasArray.push(this.criarEntregaForm());
  }

  removerEntrega(index: number): void {
    this.entregasArray.removeAt(index);
    if (this.entregasArray.length === 0) {
      this.adicionarEntrega();
    }
  }

  ctrl(name: string): AbstractControl { return this.form.get(name)!; }

  hasError(name: string, error?: string): boolean {
    const c = this.ctrl(name);
    if (!c.invalid || !c.touched) return false;
    return error ? c.hasError(error) : true;
  }

  carregarAtribuicoes(): void {
    this.isLoading.set(true);
    this.svc.listar({
      status: this.filtroStatus || undefined,
      search: this.filtroSearch || undefined,
      page: this.currentPage,
      pageSize: this.pageSize,
    }).subscribe({
      next: (result) => {
        this.pagedResult.set(result);
        this.isLoading.set(false);
      },
      error: (err) => {
        this.errorMsg.set(err.message ?? 'Erro ao carregar atribuições');
        this.isLoading.set(false);
      },
    });
  }

  onSearchChange(value: string): void {
    this.filtroSearch = value;
    this.searchInput$.next(value);
  }

  onStatusChange(value: string): void {
    this.filtroStatus = value;
    this.currentPage = 1;
    this.carregarAtribuicoes();
  }

  goToPage(page: number): void {
    const total = this.totalPages();
    if (page < 1 || page > total) return;
    this.currentPage = page;
    this.carregarAtribuicoes();
  }

  goToCreate(): void {
    this.resetForm();
    this.uiState.goToAtribuicaoCreate();
  }

  goToEdit(atribuicao: Atribuicao, event?: Event): void {
    if (event) event.stopPropagation();
    this.carregarDadosParaEdicao(atribuicao);
    this.uiState.goToAtribuicaoEdit(atribuicao.id);
  }

  goToList(): void {
    this.uiState.goToAtribuicaoList();
    this.resetForm();
    this.carregarAtribuicoes();
  }

  cancel(): void {
    this.goToList();
  }

  private resetForm(): void {
    while (this.entregasArray.length) {
      this.entregasArray.removeAt(0);
    }
    this.form.reset({
      prioridade: 'Media',
      distanciaTotalKm: 0,
      ajudanteIds: []
    });
    this.adicionarEntrega();
    this.errorMsg.set(null);
  }

  private carregarDadosParaEdicao(atribuicao: Atribuicao): void {
    while (this.entregasArray.length) {
      this.entregasArray.removeAt(0);
    }

    this.form.patchValue({
      clienteNome: atribuicao.clienteNome,
      clienteContacto: atribuicao.clienteContacto ?? '',
      enderecoOrigem: atribuicao.enderecoOrigem ?? '',
      enderecoDestino: atribuicao.enderecoDestino,
      dataPrevistaInicio: atribuicao.dataPrevistaInicio?.split('T')[0] ?? '',
      dataPrevistaFim: atribuicao.dataPrevistaFim?.split('T')[0] ?? '',
      prioridade: atribuicao.prioridade,
      observacoes: atribuicao.observacoes ?? '',
      motoristaId: atribuicao.motoristaId,
      veiculoId: atribuicao.veiculoId,
      transportadoraId: atribuicao.transportadoraId,
      rotaId: atribuicao.rotaId,
      ajudanteIds: atribuicao.ajudanteIds,
      distanciaTotalKm: atribuicao.distanciaTotalKm,
      tempoEstimadoHoras: atribuicao.tempoEstimadoHoras,
    });

    if (atribuicao.entregas && atribuicao.entregas.length > 0) {
      atribuicao.entregas.forEach(entrega => {
        this.entregasArray.push(this.criarEntregaForm(entrega));
      });
    }

    if (this.entregasArray.length === 0) {
      this.adicionarEntrega();
    }

    this.errorMsg.set(null);
  }

  salvarAtribuicao(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid) {
      this.errorMsg.set('Corrija os erros no formulário antes de continuar.');
      return;
    }

    this.isSaving.set(true);
    this.errorMsg.set(null);
    const v = this.form.getRawValue();

    const entregas = v.entregas.map((e: any, index: number) => ({
      destinatario: e.destinatario?.trim(),
      endereco: e.endereco?.trim(),
      contacto: e.contacto?.trim(),
      observacoes: e.observacoes?.trim(),
      ordem: e.ordem || index + 1
    })).filter((e: any) => e.destinatario);

    const dto: AtribuicaoCreateDto = {
      clienteNome: v.clienteNome.trim(),
      clienteContacto: v.clienteContacto?.trim() || undefined,
      enderecoOrigem: v.enderecoOrigem?.trim() || undefined,
      enderecoDestino: v.enderecoDestino?.trim(),
      dataPrevistaInicio: v.dataPrevistaInicio || undefined,
      dataPrevistaFim: v.dataPrevistaFim || undefined,
      prioridade: v.prioridade,
      observacoes: v.observacoes?.trim() || undefined,
      motoristaId: v.motoristaId || undefined,
      veiculoId: v.veiculoId || undefined,
      transportadoraId: v.transportadoraId || undefined,
      rotaId: v.rotaId || undefined,
      ajudanteIds: v.ajudanteIds || [],
      distanciaTotalKm: v.distanciaTotalKm || 0,
      tempoEstimadoHoras: v.tempoEstimadoHoras || undefined,
      entregas: entregas.length > 0 ? entregas : undefined
    };

    if (this.uiState.isAtribuicaoEdit() && this.editingId()) {
      this.svc.atualizar(this.editingId()!, dto).subscribe({
        next: () => this.onSaveSuccess('Atribuição actualizada com sucesso.'),
        error: (err) => this.onSaveError(err.message)
      });
    } else {
      this.svc.criar(dto).subscribe({
        next: () => this.onSaveSuccess('Atribuição criada com sucesso.'),
        error: (err) => this.onSaveError(err.message)
      });
    }
  }

  iniciarAtribuicao(atribuicao: Atribuicao, event?: Event): void {
    if (event) event.stopPropagation();
    if (!confirm(`Iniciar atribuição ${atribuicao.numeroAtribuicao}?`)) return;

    this.svc.iniciar(atribuicao.id).subscribe({
      next: () => {
        this.carregarAtribuicoes();
        this.showToast('Atribuição iniciada com sucesso.');
      },
      error: (err) => this.errorMsg.set(err.message)
    });
  }

  concluirAtribuicao(atribuicao: Atribuicao, event?: Event): void {
    if (event) event.stopPropagation();
    if (!confirm(`Concluir atribuição ${atribuicao.numeroAtribuicao}?`)) return;

    this.svc.concluir(atribuicao.id).subscribe({
      next: () => {
        this.carregarAtribuicoes();
        this.showToast('Atribuição concluída com sucesso.');
      },
      error: (err) => this.errorMsg.set(err.message)
    });
  }

  confirmarDelete(atribuicao: Atribuicao, event?: Event): void {
    if (event) event.stopPropagation();
    this.atribuicaoParaDelete.set(atribuicao);
    this.showDeleteConfirm.set(true);
  }

  cancelarDelete(): void {
    this.showDeleteConfirm.set(false);
    this.atribuicaoParaDelete.set(null);
  }

  executarDelete(): void {
    const a = this.atribuicaoParaDelete();
    if (!a) return;
    this.svc.deletar(a.id).subscribe({
      next: () => {
        this.cancelarDelete();
        this.carregarAtribuicoes();
        this.showToast('Atribuição cancelada com sucesso.');
      },
      error: (err) => { this.errorMsg.set(err.message); this.cancelarDelete(); }
    });
  }

  private onSaveSuccess(msg: string): void {
    this.isSaving.set(false);
    this.goToList();
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

  getStatusClass(status: string): string {
    const classes: Record<string, string> = {
      'Pendente': 'status-pendente',
      'EmProgresso': 'status-progresso',
      'Concluida': 'status-concluida',
      'Cancelada': 'status-cancelada'
    };
    return classes[status] || 'status-pendente';
  }

  getPrioridadeClass(prioridade: string): string {
    const classes: Record<string, string> = {
      'Baixa': 'prioridade-baixa',
      'Media': 'prioridade-media',
      'Alta': 'prioridade-alta',
      'Urgente': 'prioridade-urgente'
    };
    return classes[prioridade] || 'prioridade-media';
  }

  formatarData(data: string): string {
    if (!data) return '—';
    return new Date(data).toLocaleDateString('pt-PT', {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit'
    });
  }

  formatarDataSimples(data: string): string {
    if (!data) return '—';
    return new Date(data).toLocaleDateString('pt-PT');
  }
}