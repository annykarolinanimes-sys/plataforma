import {Component, OnInit, OnDestroy,inject, signal, computed, Input} from '@angular/core';
import { CommonModule } from '@angular/common';
import {FormsModule, ReactiveFormsModule, FormBuilder, FormGroup,Validators, AbstractControl, ValidatorFn} from '@angular/forms';
import { Subject, debounceTime, distinctUntilChanged, takeUntil } from 'rxjs';

import {IncidentesService, Incidente,PagedResult, IncidenteCreateDto, IncidenteUpdateDto, ResolverIncidenteDto} from '../../core/services/incidentes.service';
import { PdfService, PdfField }               from '../../core/services/pdf.service';
import { VeiculosService, Veiculo }            from '../../core/services/veiculos.service';
import { ClientesCatalogoService, ClienteModel } from '../../core/services/clientes-catalogo.service';
import { GestaoViagemService, GestaoViagem }   from '../../core/services/gestao-viagens.service';
import { UiStateService }                      from '../../core/services/ui-state.service';

function vinculoObrigatorioValidator(): ValidatorFn {
  return (group: AbstractControl) => {
    const viagemId    = group.get('viagemId')?.value;
    const veiculoId   = group.get('veiculoId')?.value;
    const clienteId   = group.get('clienteId')?.value;
    const atribuicaoId = group.get('atribuicaoId')?.value;
    const temVinculo  = !!(viagemId || veiculoId || clienteId || atribuicaoId);
    return temVinculo ? null : { vinculoObrigatorio: true };
  };
}

function dataNaoFuturoValidator(): ValidatorFn {
  return (control: AbstractControl) => {
    if (!control.value) return null;
    const data = new Date(control.value);
    return data > new Date() ? { dataFutura: true } : null;
  };
}

function dataResolucaoValidator(): ValidatorFn {
  return (group: AbstractControl) => {
    const ocorrencia = group.get('dataOcorrencia')?.value;
    const resolucao  = group.get('dataResolucao')?.value;
    if (ocorrencia && resolucao && new Date(resolucao) < new Date(ocorrencia)) {
      return { dataResolucaoAnterior: true };
    }
    return null;
  };
}

@Component({
  selector: 'app-incidentes',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule],
  templateUrl: './incidentes.component.html',
  styleUrls: ['./incidentes.component.css'],
})
export class IncidentesComponent implements OnInit, OnDestroy {

  @Input() contextViagemId?: number;
  @Input() contextVeiculoId?: number;
  @Input() contextClienteId?: number;

  private readonly svc             = inject(IncidentesService);
  private readonly veiculosService = inject(VeiculosService);
  private readonly clientesService = inject(ClientesCatalogoService);
  private readonly viagensService  = inject(GestaoViagemService);
  private readonly fb              = inject(FormBuilder);
  private readonly pdfService      = inject(PdfService);
  readonly uiState                 = inject(UiStateService);
  private readonly destroy$        = new Subject<void>();

  currentState = this.uiState.currentIncidenteState;
  editingId    = this.uiState.currentIncidenteId;
  isViewing    = signal(false);

  isListView()   { return this.currentState() === 'list';   }
  isCreateView() { return this.currentState() === 'create'; }
  isEditView()   { return this.currentState() === 'edit';   }

  pagedResult = signal<PagedResult<Incidente> | null>(null);
  incidentes  = computed(() => this.pagedResult()?.items ?? []);
  isLoading   = signal(false);
  isSaving    = signal(false);
  errorMsg    = signal<string | null>(null);
  successMsg  = signal<string | null>(null);

  totalIncidentes  = computed(() => this.pagedResult()?.total ?? 0);
  countAbertos     = computed(() => this.incidentes().filter(i => i.status === 'Aberto').length);
  countCriticos    = computed(() => this.incidentes().filter(i => i.gravidade === 'Critica').length);
  custoTotal       = computed(() =>
    this.incidentes().reduce((s, i) => s + (i.custoAssociado ?? 0), 0)
  );
  taxaResolucao    = computed(() => {
    const total     = this.incidentes().length;
    const resolvidos = this.incidentes().filter(i =>
      i.status === 'Resolvido' || i.status === 'Fechado'
    ).length;
    return total > 0 ? Math.round((resolvidos / total) * 100) : 0;
  });


  viagensFiltradas  = signal<GestaoViagem[]>([]);
  veiculosFiltrados = signal<Veiculo[]>([]);
  clientesFiltrados = signal<ClienteModel[]>([]);

  viagemSelecionada  = signal<GestaoViagem | null>(null);
  veiculoSelecionado = signal<Veiculo | null>(null);
  clienteSelecionado = signal<ClienteModel | null>(null);

  searchViagem  = signal('');
  searchVeiculo = signal('');
  searchCliente = signal('');

  dropdownViagem  = signal(false);
  dropdownVeiculo = signal(false);
  dropdownCliente = signal(false);

  // Subjects para debounce das pesquisas
  private readonly viagemInput$  = new Subject<string>();
  private readonly veiculoInput$ = new Subject<string>();
  private readonly clienteInput$ = new Subject<string>();

  // Se o vínculo de contexto está bloqueado
  contextoViagemBloqueado  = signal(false);
  contextoVeiculoBloqueado = signal(false);
  contextoClienteBloqueado = signal(false);

  // ── Upload de evidências (Mock) ───────────────────────────────────────────
  evidencias = signal<{ nome: string; tipo: string; tamanho: string }[]>([]);

  // ── Modal de Resolução ────────────────────────────────────────────────────
  showResolverModal    = signal(false);
  incidenteParaResolver = signal<Incidente | null>(null);
  resolverForm!: FormGroup;
  isResolvendo         = signal(false);

  // ── Modal de confirmação de exclusão ──────────────────────────────────────
  showDeleteConfirm    = signal(false);
  incidenteParaDelete  = signal<Incidente | null>(null);

  // ── Filtros & paginação ───────────────────────────────────────────────────
  filtroStatus    = '';
  filtroTipo      = '';
  filtroGravidade = '';
  filtroSearch    = '';
  currentPage     = 1;
  readonly pageSize = 15;

  totalPages = computed(() => this.pagedResult()?.totalPages ?? 0);
  pages      = computed(() =>
    Array.from({ length: this.totalPages() }, (_, i) => i + 1)
  );

  readonly tiposIncidente = [
    'Atraso', 'Avaria', 'CargaDanificada', 'EntregaFalha', 'Acidente', 'Outro'
  ] as const;
  readonly gravidades  = ['Baixa', 'Media', 'Alta', 'Critica'] as const;
  readonly statusList  = ['Aberto', 'EmAnalise', 'Resolvido', 'Fechado'] as const;

  form!: FormGroup;
  private readonly searchInput$ = new Subject<string>();

  // ── Ciclo de vida ─────────────────────────────────────────────────────────

  ngOnInit(): void {
    this.initForm();
    this.initResolverForm();
    this._initSmartSelectDebounces();

    this.searchInput$
      .pipe(debounceTime(350), distinctUntilChanged(), takeUntil(this.destroy$))
      .subscribe(() => { this.currentPage = 1; this.carregarIncidentes(); });

    this.carregarIncidentes();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  // ── Formulário ────────────────────────────────────────────────────────────

  private initForm(): void {
    this.form = this.fb.group({
      tipo:          ['', Validators.required],
      gravidade:     ['Media', Validators.required],
      titulo:        ['', [Validators.required, Validators.maxLength(200)]],
      descricao:     ['', Validators.maxLength(2000)],
      dataOcorrencia:['', dataNaoFuturoValidator()],
      dataResolucao: [''],
      // Vínculos — geridos pelos smart selects
      viagemId:      [null],
      veiculoId:     [null],
      clienteId:     [null],
      atribuicaoId:  [null],
      // Resolução
      causa:         [''],
      acaoCorretiva: [''],
      responsavelResolucao: ['', Validators.maxLength(200)],
      custoAssociado:       [null, Validators.min(0)],
      observacoes:          ['', Validators.maxLength(1000)],
    }, {
      validators: [vinculoObrigatorioValidator(), dataResolucaoValidator()]
    });
  }

  private initResolverForm(): void {
    this.resolverForm = this.fb.group({
      acaoCorretiva:       ['', Validators.required],
      responsavelResolucao:['', Validators.maxLength(200)],
      custoAssociado:      [null, Validators.min(0)],
      observacoes:         ['', Validators.maxLength(1000)],
    });
  }

  ctrl(name: string): AbstractControl { return this.form.get(name)!; }

  hasError(name: string, error?: string): boolean {
    const c = this.ctrl(name);
    if (!c.invalid || !c.touched) return false;
    return error ? c.hasError(error) : true;
  }

  hasFormError(error: string): boolean {
    return this.form.hasError(error) && this.form.touched;
  }

  // ── Smart Selects ─────────────────────────────────────────────────────────

  private _initSmartSelectDebounces(): void {
    this.viagemInput$
      .pipe(debounceTime(300), distinctUntilChanged(), takeUntil(this.destroy$))
      .subscribe(q => this._pesquisarViagens(q));

    this.veiculoInput$
      .pipe(debounceTime(300), distinctUntilChanged(), takeUntil(this.destroy$))
      .subscribe(q => this._pesquisarVeiculos(q));

    this.clienteInput$
      .pipe(debounceTime(300), distinctUntilChanged(), takeUntil(this.destroy$))
      .subscribe(q => this._pesquisarClientes(q));
  }

  onViagemSearch(value: string): void {
    this.searchViagem.set(value);
    this.dropdownViagem.set(true);
    this.viagemInput$.next(value);
  }

  onVeiculoSearch(value: string): void {
    this.searchVeiculo.set(value);
    this.dropdownVeiculo.set(true);
    this.veiculoInput$.next(value);
  }

  onClienteSearch(value: string): void {
    this.searchCliente.set(value);
    this.dropdownCliente.set(true);
    this.clienteInput$.next(value);
  }

  private _pesquisarViagens(q: string): void {
    if (!q || q.length < 2) { this.viagensFiltradas.set([]); return; }
    this.viagensService.listar({ search: q, pageSize: 8 }).subscribe({
      next: r => this.viagensFiltradas.set(r.items),
      error: () => this.viagensFiltradas.set([]),
    });
  }

  private _pesquisarVeiculos(q: string): void {
    if (!q || q.length < 2) { this.veiculosFiltrados.set([]); return; }
    this.veiculosService.listar({ search: q, pageSize: 8 } as any).subscribe({
      next: r => this.veiculosFiltrados.set(r.items),
      error: () => this.veiculosFiltrados.set([]),
    });
  }

  private _pesquisarClientes(q: string): void {
    if (!q || q.length < 2) { this.clientesFiltrados.set([]); return; }
    this.clientesService.listar({ search: q, pageSize: 8 }).subscribe({
      next: r => this.clientesFiltrados.set(r.items),
      error: () => this.clientesFiltrados.set([]),
    });
  }

  selecionarViagem(v: GestaoViagem): void {
    this.ctrl('viagemId').setValue(v.id);
    this.viagemSelecionada.set(v);
    this.searchViagem.set(v.numeroViagem);
    this.dropdownViagem.set(false);
    this.viagensFiltradas.set([]);
  }

  selecionarVeiculo(v: Veiculo): void {
    this.ctrl('veiculoId').setValue(v.id);
    this.veiculoSelecionado.set(v);
    this.searchVeiculo.set(`${v.matricula} — ${v.marca} ${v.modelo}`);
    this.dropdownVeiculo.set(false);
    this.veiculosFiltrados.set([]);
  }

  selecionarCliente(c: ClienteModel): void {
    this.ctrl('clienteId').setValue(c.id);
    this.clienteSelecionado.set(c);
    this.searchCliente.set(c.nome);
    this.dropdownCliente.set(false);
    this.clientesFiltrados.set([]);
  }

  limparViagem(): void {
    if (this.contextoViagemBloqueado()) return;
    this.ctrl('viagemId').setValue(null);
    this.viagemSelecionada.set(null);
    this.searchViagem.set('');
  }

  limparVeiculo(): void {
    if (this.contextoVeiculoBloqueado()) return;
    this.ctrl('veiculoId').setValue(null);
    this.veiculoSelecionado.set(null);
    this.searchVeiculo.set('');
  }

  limparCliente(): void {
    if (this.contextoClienteBloqueado()) return;
    this.ctrl('clienteId').setValue(null);
    this.clienteSelecionado.set(null);
    this.searchCliente.set('');
  }

  fecharDropdowns(): void {
    this.dropdownViagem.set(false);
    this.dropdownVeiculo.set(false);
    this.dropdownCliente.set(false);
  }

  // ── Upload de evidências (Mock UI) ────────────────────────────────────────

  onFileSelect(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (!input.files?.length) return;
    Array.from(input.files).forEach(f => {
      this.evidencias.update(ev => [
        ...ev,
        {
          nome: f.name,
          tipo: f.type || 'application/octet-stream',
          tamanho: this._formatBytes(f.size),
        }
      ]);
    });
    input.value = '';
  }

  removerEvidencia(idx: number): void {
    this.evidencias.update(ev => ev.filter((_, i) => i !== idx));
  }

  private _formatBytes(bytes: number): string {
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  }

  iconeTipoEvidencia(tipo: string): string {
    if (tipo.startsWith('image/')) return 'las la-image';
    if (tipo === 'application/pdf') return 'las la-file-pdf';
    return 'las la-file-alt';
  }

  // ── Listagem & Paginação ──────────────────────────────────────────────────

  carregarIncidentes(): void {
    this.isLoading.set(true);
    this.svc.listar({
      status:   this.filtroStatus   || undefined,
      tipo:     this.filtroTipo     || undefined,
      gravidade:this.filtroGravidade || undefined,
      search:   this.filtroSearch   || undefined,
      page:     this.currentPage,
      pageSize: this.pageSize,
    }).subscribe({
      next:  r   => { this.pagedResult.set(r); this.isLoading.set(false); },
      error: err => { this.errorMsg.set(err.message ?? 'Erro ao carregar incidentes.'); this.isLoading.set(false); },
    });
  }

  onSearchChange(value: string): void {
    this.filtroSearch = value;
    this.searchInput$.next(value);
  }

  onFiltroChange(): void {
    this.currentPage = 1;
    this.carregarIncidentes();
  }

  goToPage(page: number): void {
    if (page < 1 || page > this.totalPages()) return;
    this.currentPage = page;
    this.carregarIncidentes();
  }

  // ── Navegação ─────────────────────────────────────────────────────────────

  goToCreate(): void {
    this.isViewing.set(false);
    this._resetForm();
    // Contextualização automática via @Input
    if (this.contextViagemId) {
      this.ctrl('viagemId').setValue(this.contextViagemId);
      this.ctrl('viagemId').disable();
      this.contextoViagemBloqueado.set(true);
      this.searchViagem.set(`Viagem #${this.contextViagemId}`);
    }
    if (this.contextVeiculoId) {
      this.ctrl('veiculoId').setValue(this.contextVeiculoId);
      this.ctrl('veiculoId').disable();
      this.contextoVeiculoBloqueado.set(true);
    }
    if (this.contextClienteId) {
      this.ctrl('clienteId').setValue(this.contextClienteId);
      this.ctrl('clienteId').disable();
      this.contextoClienteBloqueado.set(true);
    }
    this.uiState.goToIncidenteCreate();
  }

  goToEdit(incidente: Incidente, event?: Event): void {
    if (event) event.stopPropagation();
    this.isViewing.set(false);
    this._patchIncidente(incidente);
    this.uiState.goToIncidenteEdit(incidente.id);
  }

  goToDetails(incidente: Incidente, event?: Event): void {
    if (event) event.stopPropagation();
    this._patchIncidente(incidente);
    this.isViewing.set(true);
    this.uiState.goToIncidenteEdit(incidente.id);
  }

  goToList(): void {
    this.isViewing.set(false);
    this.uiState.goToIncidenteList();
    this._resetForm();
    this.carregarIncidentes();
  }

  cancel(): void { this.goToList(); }

  // ── PDF ───────────────────────────────────────────────────────────────────

  imprimirPdf(i: Incidente, event?: Event): void {
    if (event) event.stopPropagation();
    const fields: PdfField[] = [
      { label: 'Nº Incidente',   value: i.numeroIncidente },
      { label: 'Tipo',           value: this.getTipoLabel(i.tipo) },
      { label: 'Gravidade',      value: i.gravidade },
      { label: 'Status',         value: this.getStatusLabel(i.status) },
      { label: 'Título',         value: i.titulo },
      { label: 'Descrição',      value: i.descricao   ?? '—' },
      { label: 'Data Ocorrência',value: this.formatarData(i.dataOcorrencia) },
      { label: 'Viagem',         value: i.viagemNumero ?? '—' },
      { label: 'Veículo',        value: i.veiculoMatricula ?? '—' },
      { label: 'Cliente',        value: i.clienteNome ?? '—' },
      { label: 'Causa',          value: i.causa         ?? '—' },
      { label: 'Ação Corretiva', value: i.acaoCorretiva ?? '—' },
      { label: 'Responsável',    value: i.responsavelResolucao ?? '—' },
      { label: 'Custo (€)',      value: i.custoAssociado ?? 0 },
    ];
    try {
      const blob = this.pdfService.generateEntityPdf(
        `Incidente ${i.numeroIncidente}`, fields, 'Documento gerado automaticamente.'
      );
      this.pdfService.downloadPdf(blob, `Incidente_${i.numeroIncidente}.pdf`);
    } catch {
      this.errorMsg.set('Erro ao gerar PDF do incidente.');
    }
  }

  // ── CRUD ──────────────────────────────────────────────────────────────────

  salvarIncidente(): void {
    this.form.markAllAsTouched();

    if (this.form.hasError('vinculoObrigatorio')) {
      this.errorMsg.set('Associe pelo menos um vínculo (Viagem, Veículo, Cliente ou Atribuição).');
      return;
    }
    if (this.form.hasError('dataResolucaoAnterior')) {
      this.errorMsg.set('A data de resolução não pode ser anterior à data de ocorrência.');
      return;
    }
    if (this.form.invalid) {
      this.errorMsg.set('Corrija os erros no formulário antes de continuar.');
      return;
    }

    this.isSaving.set(true);
    this.errorMsg.set(null);
    const v = this.form.getRawValue();

    if (this.isEditView() && this.editingId()) {
      const dto: IncidenteUpdateDto = {
        status:              v.status           || undefined,
        gravidade:           v.gravidade,
        descricao:           v.descricao?.trim()    || undefined,
        causa:               v.causa?.trim()         || undefined,
        acaoCorretiva:       v.acaoCorretiva?.trim() || undefined,
        responsavelResolucao:v.responsavelResolucao?.trim() || undefined,
        custoAssociado:      v.custoAssociado != null ? +v.custoAssociado : undefined,
        observacoes:         v.observacoes?.trim()   || undefined,
        dataResolucao:       v.dataResolucao          || undefined,
      };
      this.svc.atualizar(this.editingId()!, dto).subscribe({
        next:  () => this._onSaveSuccess('Incidente actualizado com sucesso.'),
        error: e  => this._onSaveError(e.message),
      });
    } else {
      const dto: IncidenteCreateDto = {
        tipo:                v.tipo,
        gravidade:           v.gravidade,
        titulo:              v.titulo.trim(),
        descricao:           v.descricao?.trim()    || undefined,
        dataOcorrencia:      v.dataOcorrencia        || undefined,
        viagemId:            v.viagemId              || undefined,
        veiculoId:           v.veiculoId             || undefined,
        clienteId:           v.clienteId             || undefined,
        atribuicaoId:        v.atribuicaoId          || undefined,
        causa:               v.causa?.trim()         || undefined,
        acaoCorretiva:       v.acaoCorretiva?.trim() || undefined,
        responsavelResolucao:v.responsavelResolucao?.trim() || undefined,
        custoAssociado:      v.custoAssociado != null ? +v.custoAssociado : undefined,
        observacoes:         v.observacoes?.trim()   || undefined,
      };
      this.svc.criar(dto).subscribe({
        next:  () => this._onSaveSuccess('Incidente registado com sucesso.'),
        error: e  => this._onSaveError(e.message),
      });
    }
  }

  // ── Modal de Resolução ────────────────────────────────────────────────────

  abrirModalResolver(incidente: Incidente, event?: Event): void {
    if (event) event.stopPropagation();
    this.incidenteParaResolver.set(incidente);
    this.resolverForm.reset();
    this.showResolverModal.set(true);
  }

  fecharResolverModal(): void {
    this.showResolverModal.set(false);
    this.incidenteParaResolver.set(null);
    this.resolverForm.reset();
  }

  submeterResolucao(): void {
    this.resolverForm.markAllAsTouched();
    if (this.resolverForm.invalid) return;

    this.isResolvendo.set(true);
    const incidente = this.incidenteParaResolver()!;
    const v = this.resolverForm.getRawValue();

    const dto: ResolverIncidenteDto = {
      acaoCorretiva:       v.acaoCorretiva.trim(),
      responsavelResolucao:v.responsavelResolucao?.trim() || undefined,
      custoAssociado:      v.custoAssociado != null ? +v.custoAssociado : undefined,
      observacoes:         v.observacoes?.trim() || undefined,
    };

    this.svc.resolver(incidente.id, dto).subscribe({
      next: () => {
        this.isResolvendo.set(false);
        this.fecharResolverModal();
        this.carregarIncidentes();
        this.showToast('Incidente resolvido com sucesso.');
      },
      error: e => {
        this.isResolvendo.set(false);
        this.errorMsg.set(e.message);
      },
    });
  }

  fecharIncidente(incidente: Incidente, event?: Event): void {
    if (event) event.stopPropagation();
    if (!confirm(`Fechar incidente ${incidente.numeroIncidente}?`)) return;
    this.svc.fechar(incidente.id).subscribe({
      next:  () => { this.carregarIncidentes(); this.showToast('Incidente fechado com sucesso.'); },
      error: e  => this.errorMsg.set(e.message),
    });
  }

  // ── Modal de exclusão ──────────────────────────────────────────────────────

  confirmarDelete(incidente: Incidente, event?: Event): void {
    if (event) event.stopPropagation();
    this.incidenteParaDelete.set(incidente);
    this.showDeleteConfirm.set(true);
  }

  cancelarDelete(): void {
    this.showDeleteConfirm.set(false);
    this.incidenteParaDelete.set(null);
  }

  executarDelete(): void {
    const inc = this.incidenteParaDelete();
    if (!inc) return;
    this.svc.deletar(inc.id).subscribe({
      next:  () => { this.cancelarDelete(); this.carregarIncidentes(); this.showToast('Incidente removido.'); },
      error: e  => { this.errorMsg.set(e.message); this.cancelarDelete(); },
    });
  }

  // ── Helpers ───────────────────────────────────────────────────────────────

  private _resetForm(): void {
    this.form.reset({ gravidade: 'Media' });
    this.errorMsg.set(null);
    this.evidencias.set([]);
    this.viagemSelecionada.set(null);
    this.veiculoSelecionado.set(null);
    this.clienteSelecionado.set(null);
    this.searchViagem.set('');
    this.searchVeiculo.set('');
    this.searchCliente.set('');
    this.contextoViagemBloqueado.set(false);
    this.contextoVeiculoBloqueado.set(false);
    this.contextoClienteBloqueado.set(false);
    this.fecharDropdowns();
  }

  private _patchIncidente(inc: Incidente): void {
    this.form.patchValue({
      tipo:                inc.tipo,
      gravidade:           inc.gravidade,
      titulo:              inc.titulo,
      descricao:           inc.descricao         ?? '',
      dataOcorrencia:      inc.dataOcorrencia?.split('T')[0] ?? '',
      dataResolucao:       inc.dataResolucao?.split('T')[0]  ?? '',
      viagemId:            inc.viagemId           ?? null,
      veiculoId:           inc.veiculoId          ?? null,
      clienteId:           inc.clienteId          ?? null,
      atribuicaoId:        inc.atribuicaoId       ?? null,
      causa:               inc.causa              ?? '',
      acaoCorretiva:       inc.acaoCorretiva      ?? '',
      responsavelResolucao:inc.responsavelResolucao ?? '',
      custoAssociado:      inc.custoAssociado     ?? null,
      observacoes:         inc.observacoes        ?? '',
    });
    // Actualiza textos de pesquisa dos smart selects
    if (inc.viagemNumero)     this.searchViagem.set(inc.viagemNumero);
    if (inc.veiculoMatricula) this.searchVeiculo.set(inc.veiculoMatricula);
    if (inc.clienteNome)      this.searchCliente.set(inc.clienteNome);
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

  getTipoLabel(tipo: string): string {
    const m: Record<string, string> = {
      Atraso: 'Atraso', Avaria: 'Avaria', CargaDanificada: 'Carga Danificada',
      EntregaFalha: 'Falha Entrega', Acidente: 'Acidente', Outro: 'Outro',
    };
    return m[tipo] ?? tipo;
  }

  getStatusLabel(s: string): string {
    const m: Record<string, string> = {
      Aberto: 'Aberto', EmAnalise: 'Em Análise', Resolvido: 'Resolvido', Fechado: 'Fechado',
    };
    return m[s] ?? s;
  }

  getTipoClass(tipo: string): string {
    const m: Record<string, string> = {
      Atraso: 'tipo-atraso', Avaria: 'tipo-avaria',
      CargaDanificada: 'tipo-danificado', EntregaFalha: 'tipo-falha',
      Acidente: 'tipo-acidente', Outro: 'tipo-outro',
    };
    return m[tipo] ?? 'tipo-outro';
  }

  getStatusClass(s: string): string {
    const m: Record<string, string> = {
      Aberto: 'status-aberto', EmAnalise: 'status-analise',
      Resolvido: 'status-resolvido', Fechado: 'status-fechado',
    };
    return m[s] ?? 'status-aberto';
  }

  getGravidadeClass(g: string): string {
    const m: Record<string, string> = {
      Baixa: 'gravidade-baixa', Media: 'gravidade-media',
      Alta: 'gravidade-alta',   Critica: 'gravidade-critica',
    };
    return m[g] ?? 'gravidade-media';
  }

  formatarData(data: string | Date): string {
    if (!data) return '—';
    return new Date(data.toString()).toLocaleDateString('pt-PT', {
      day: '2-digit', month: '2-digit', year: 'numeric',
    });
  }

  formatarMoeda(v?: number | null): string {
    if (v == null) return '—';
    return new Intl.NumberFormat('pt-PT', { style: 'currency', currency: 'EUR' }).format(v);
  }

  podeResolver(inc: Incidente): boolean {
    return inc.status === 'Aberto' || inc.status === 'EmAnalise';
  }

  podeFerchar(inc: Incidente): boolean {
    return inc.status === 'Resolvido';
  }
}
