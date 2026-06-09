import {
  Component, OnInit, OnDestroy, inject, signal, computed, Input
} from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  FormsModule, ReactiveFormsModule, FormBuilder, FormGroup,
  Validators, AbstractControl, ValidatorFn
} from '@angular/forms';
import { Subject, debounceTime, distinctUntilChanged, forkJoin, of, throwError, takeUntil } from 'rxjs';
import { catchError, switchMap } from 'rxjs/operators';

import {
  IncidentesService, Incidente, IncidenteAnexo, PagedResult,
  IncidenteCreateDto, IncidenteUpdateDto, ResolverIncidenteDto
} from '../../core/services/incidentes.service';
import { PdfService, PdfField }                from '../../core/services/pdf.service';
import { VeiculosService, Veiculo }             from '../../core/services/veiculos.service';
import { ClientesCatalogoService, ClienteModel } from '../../core/services/clientes-catalogo.service';
import { GestaoViagemService, GestaoViagem }    from '../../core/services/gestao-viagens.service';
import { UiStateService }                       from '../../core/services/ui-state.service';

// ── Validadores ───────────────────────────────────────────────────────────────

function vinculoObrigatorioValidator(): ValidatorFn {
  return (group: AbstractControl) => {
    const viagemId     = group.get('viagemId')?.value;
    const veiculoId    = group.get('veiculoId')?.value;
    const clienteId    = group.get('clienteId')?.value;
    const atribuicaoId = group.get('atribuicaoId')?.value;
    return (viagemId || veiculoId || clienteId || atribuicaoId) ? null : { vinculoObrigatorio: true };
  };
}

function dataNaoFuturoValidator(): ValidatorFn {
  return (control: AbstractControl) => {
    if (!control.value) return null;
    return new Date(control.value) > new Date() ? { dataFutura: true } : null;
  };
}

function dataResolucaoValidator(): ValidatorFn {
  return (group: AbstractControl) => {
    const ocorrencia = group.get('dataOcorrencia')?.value;
    const resolucao  = group.get('dataResolucao')?.value;
    if (ocorrencia && resolucao && new Date(resolucao) < new Date(ocorrencia))
      return { dataResolucaoAnterior: true };
    return null;
  };
}

// ── Interface para ficheiros pendentes ────────────────────────────────────────

export interface PendingFile {
  file: File;
  id: string;
  previewUrl?: string;
}

// ── Componente ────────────────────────────────────────────────────────────────

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

  // ── Estado de navegação ───────────────────────────────────────────────────

  currentState = this.uiState.currentIncidenteState;
  editingId    = this.uiState.currentIncidenteId;
  isViewing    = signal(false);
  selectedIncidente = computed(() => this.incidentes().find(i => i.id === this.editingId()) ?? null);

  isListView()   { return this.currentState() === 'list';   }
  isCreateView() { return this.currentState() === 'create'; }
  isEditView()   { return this.currentState() === 'edit';   }

  // ── Estado de dados ───────────────────────────────────────────────────────

  pagedResult = signal<PagedResult<Incidente> | null>(null);
  incidentes  = computed(() => this.pagedResult()?.items ?? []);
  isLoading   = signal(false);
  isSaving    = signal(false);
  errorMsg    = signal<string | null>(null);
  successMsg  = signal<string | null>(null);

  totalIncidentes = computed(() => this.pagedResult()?.total ?? 0);
  countAbertos    = computed(() => this.incidentes().filter(i => i.status === 'Aberto').length);
  countCriticos   = computed(() => this.incidentes().filter(i => i.gravidade === 'Critica').length);
  custoTotal      = computed(() => this.incidentes().reduce((s, i) => s + (i.custoAssociado ?? 0), 0));
  taxaResolucao   = computed(() => {
    const total      = this.incidentes().length;
    const resolvidos = this.incidentes().filter(i => i.status === 'Resolvido' || i.status === 'Fechado').length;
    return total > 0 ? Math.round((resolvidos / total) * 100) : 0;
  });

  // ── Smart selects ─────────────────────────────────────────────────────────

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

  private readonly viagemInput$  = new Subject<string>();
  private readonly veiculoInput$ = new Subject<string>();
  private readonly clienteInput$ = new Subject<string>();

  contextoViagemBloqueado  = signal(false);
  contextoVeiculoBloqueado = signal(false);
  contextoClienteBloqueado = signal(false);

  // ── Estado de anexos ──────────────────────────────────────────────────────

  /** Ficheiros selecionados mas ainda não enviados ao servidor */
  pendingFiles = signal<PendingFile[]>([]);

  /** Anexos já persistidos no servidor */
  anexosExistentes = signal<IncidenteAnexo[]>([]);

  /** ID do anexo a ser removido (para spinner) */
  removingAnexoId = signal<number | null>(null);

  /** Arrastar sobre a zona de drop */
  isDragOver = signal(false);

  private readonly MAX_FILE_SIZE  = 5 * 1024 * 1024;
  private readonly ALLOWED_TYPES  = ['application/pdf', 'image/jpeg', 'image/jpg', 'image/png'];
  private readonly ALLOWED_EXT    = ['.pdf', '.jpg', '.jpeg', '.png'];

  // ── Modais ────────────────────────────────────────────────────────────────

  showResolverModal     = signal(false);
  incidenteParaResolver = signal<Incidente | null>(null);
  resolverForm!: FormGroup;
  isResolvendo          = signal(false);

  showDeleteConfirm   = signal(false);
  incidenteParaDelete = signal<Incidente | null>(null);

  // ── Filtros e paginação ───────────────────────────────────────────────────

  filtroStatus    = '';
  filtroTipo      = '';
  filtroGravidade = '';
  filtroSearch    = '';
  currentPage     = 1;
  readonly pageSize = 15;

  totalPages = computed(() => this.pagedResult()?.totalPages ?? 0);
  pages      = computed(() => Array.from({ length: this.totalPages() }, (_, i) => i + 1));

  // ── Constantes ────────────────────────────────────────────────────────────

  readonly tiposIncidente = [
    'Atraso', 'Avaria', 'CargaDanificada', 'EntregaFalha', 'Acidente', 'Outro'
  ] as const;
  readonly gravidades  = ['Baixa', 'Media', 'Alta', 'Critica'] as const;
  readonly statusList  = ['Aberto', 'EmAnalise', 'Resolvido', 'Fechado'] as const;

  form!: FormGroup;
  private readonly searchInput$ = new Subject<string>();

  // ── Lifecycle ─────────────────────────────────────────────────────────────

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
    // Libertar object URLs para evitar memory leaks
    this.pendingFiles().forEach(pf => {
      if (pf.previewUrl) URL.revokeObjectURL(pf.previewUrl);
    });
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
      viagemId:      [null],
      veiculoId:     [null],
      clienteId:     [null],
      atribuicaoId:  [null],
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

  // ── Smart selects debounce ────────────────────────────────────────────────

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

  onViagemSearch(value: string): void  { this.searchViagem.set(value);  this.dropdownViagem.set(true);  this.viagemInput$.next(value); }
  onVeiculoSearch(value: string): void { this.searchVeiculo.set(value); this.dropdownVeiculo.set(true); this.veiculoInput$.next(value); }
  onClienteSearch(value: string): void { this.searchCliente.set(value); this.dropdownCliente.set(true); this.clienteInput$.next(value); }

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

  // ── Anexos — Drag & Drop / Seleção ───────────────────────────────────────

  onDragOver(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
    this.isDragOver.set(true);
  }

  onDragLeave(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
    this.isDragOver.set(false);
  }

  onFileDrop(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
    this.isDragOver.set(false);
    const files = event.dataTransfer?.files;
    if (files) this._processarFicheiros(Array.from(files));
  }

  onFileSelect(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (input.files) {
      this._processarFicheiros(Array.from(input.files));
      input.value = '';
    }
  }

  private _processarFicheiros(files: File[]): void {
    const novos: PendingFile[] = [];

    for (const file of files) {
      const ext = '.' + (file.name.split('.').pop()?.toLowerCase() ?? '');

      if (file.size > this.MAX_FILE_SIZE) {
        this.errorMsg.set(`"${file.name}" excede o limite de 5 MB.`);
        continue;
      }

      if (!this.ALLOWED_TYPES.includes(file.type) && !this.ALLOWED_EXT.includes(ext)) {
        this.errorMsg.set(`"${file.name}" — tipo não suportado. Use PDF, JPG ou PNG.`);
        continue;
      }

      const pending: PendingFile = { file, id: crypto.randomUUID() };
      if (file.type.startsWith('image/'))
        pending.previewUrl = URL.createObjectURL(file);

      novos.push(pending);
    }

    if (novos.length > 0) {
      this.pendingFiles.update(prev => [...prev, ...novos]);
      this.errorMsg.set(null);
    }
  }

  removerFicheiroPendente(id: string): void {
    const pf = this.pendingFiles().find(f => f.id === id);
    if (pf?.previewUrl) URL.revokeObjectURL(pf.previewUrl);
    this.pendingFiles.update(prev => prev.filter(f => f.id !== id));
  }

  /** Remove um anexo já persistido no servidor */
  removerAnexoExistente(anexo: IncidenteAnexo): void {
    const incidenteId = this.editingId();
    if (!incidenteId) return;

    this.removingAnexoId.set(anexo.id);

    this.svc.removerAnexo(incidenteId, anexo.id).subscribe({
      next: res => {
        this.anexosExistentes.update(prev => prev.filter(a => a.id !== anexo.id));
        this.removingAnexoId.set(null);
        this.showToast(res.message);
      },
      error: err => {
        this.errorMsg.set(err.error?.message || 'Erro ao remover anexo.');
        this.removingAnexoId.set(null);
      }
    });
  }

  /** Download de um anexo existente */
  downloadAnexo(anexo: IncidenteAnexo): void {
    const incidenteId = this.editingId();
    if (!incidenteId) return;

    this.svc.downloadAnexo(incidenteId, anexo.id).subscribe({
      next: blob => {
        const url = URL.createObjectURL(blob);
        const a   = Object.assign(document.createElement('a'), {
          href: url, download: anexo.nomeOriginal
        });
        a.click();
        URL.revokeObjectURL(url);
      },
      error: () => this.errorMsg.set('Erro ao descarregar o ficheiro.')
    });
  }

  /** Ícone Line Awesome por MIME type */
  getFileIcon(mimeType: string): string {
    if (mimeType === 'application/pdf') return 'la-file-pdf';
    if (mimeType.startsWith('image/'))  return 'la-file-image';
    return 'la-file-alt';
  }

  formatarTamanho(bytes: number): string {
    if (bytes < 1024)        return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  }

  // ── Carregamento ──────────────────────────────────────────────────────────

  carregarIncidentes(): void {
    this.isLoading.set(true);
    this.svc.listar({
      status:    this.filtroStatus    || undefined,
      tipo:      this.filtroTipo      || undefined,
      gravidade: this.filtroGravidade || undefined,
      search:    this.filtroSearch    || undefined,
      page:      this.currentPage,
      pageSize:  this.pageSize,
    }).subscribe({
      next:  r   => { this.pagedResult.set(r); this.isLoading.set(false); },
      error: err => { this.errorMsg.set(err.message ?? 'Erro ao carregar incidentes.'); this.isLoading.set(false); },
    });
  }

  onSearchChange(value: string): void { this.filtroSearch = value; this.searchInput$.next(value); }
  onFiltroChange(): void              { this.currentPage = 1; this.carregarIncidentes(); }

  goToPage(page: number): void {
    if (page < 1 || page > this.totalPages()) return;
    this.currentPage = page;
    this.carregarIncidentes();
  }

  // ── Navegação ─────────────────────────────────────────────────────────────

  goToCreate(): void {
    this.isViewing.set(false);
    this._resetForm();
    this.pendingFiles.set([]);
    this.anexosExistentes.set([]);

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
    this.pendingFiles.set([]);
    this.anexosExistentes.set(incidente.anexos ?? []);
    this.uiState.goToIncidenteEdit(incidente.id);
  }

  goToDetails(incidente: Incidente, event?: Event): void {
    if (event) event.stopPropagation();
    this._patchIncidente(incidente);
    this.isViewing.set(true);
    this.pendingFiles.set([]);
    this.anexosExistentes.set(incidente.anexos ?? []);
    this.uiState.goToIncidenteEdit(incidente.id);
  }

  goToList(): void {
    this.isViewing.set(false);
    this.uiState.goToIncidenteList();
    this._resetForm();
    this.pendingFiles.set([]);
    this.anexosExistentes.set([]);
    this.carregarIncidentes();
  }

  cancel(): void { this.goToList(); }

  // ── Guardar ───────────────────────────────────────────────────────────────

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

    const save$ = this.isEditView() && this.editingId()
      ? this.svc.atualizar(this.editingId()!, {
          status:               v.status           || undefined,
          gravidade:            v.gravidade,
          descricao:            v.descricao?.trim()     || undefined,
          causa:                v.causa?.trim()          || undefined,
          acaoCorretiva:        v.acaoCorretiva?.trim()  || undefined,
          responsavelResolucao: v.responsavelResolucao?.trim() || undefined,
          custoAssociado:       v.custoAssociado != null ? +v.custoAssociado : undefined,
          observacoes:          v.observacoes?.trim()    || undefined,
          dataResolucao:        v.dataResolucao           || undefined,
        } as IncidenteUpdateDto)
      : this.svc.criar({
          tipo:                 v.tipo,
          gravidade:            v.gravidade,
          titulo:               v.titulo.trim(),
          descricao:            v.descricao?.trim()     || undefined,
          dataOcorrencia:       v.dataOcorrencia         || undefined,
          viagemId:             v.viagemId               || undefined,
          veiculoId:            v.veiculoId              || undefined,
          clienteId:            v.clienteId              || undefined,
          atribuicaoId:         v.atribuicaoId           || undefined,
          causa:                v.causa?.trim()          || undefined,
          acaoCorretiva:        v.acaoCorretiva?.trim()  || undefined,
          responsavelResolucao: v.responsavelResolucao?.trim() || undefined,
          custoAssociado:       v.custoAssociado != null ? +v.custoAssociado : undefined,
          observacoes:          v.observacoes?.trim()    || undefined,
        } as IncidenteCreateDto);

    save$.pipe(
      switchMap(incidente => {
        const uploads = this.pendingFiles().map(pf =>
          this.svc.uploadAnexo(incidente.id, pf.file).pipe(
            catchError(err => throwError(() => ({ fileName: pf.file.name, error: err })))
          )
        );
        if (uploads.length === 0) return of(incidente);
        return forkJoin(uploads).pipe(switchMap(() => of(incidente)));
      })
    ).subscribe({
      next: () => {
        this.isSaving.set(false);
        this.pendingFiles.set([]);
        this.showToast(this.isEditView() ? 'Incidente atualizado com sucesso.' : 'Incidente registado com sucesso.');
        this.goToList();
      },
      error: err => {
        this.isSaving.set(false);
        if (err?.fileName) {
          const serverMsg = err.error?.error?.message ?? err.error?.message ?? null;
          this.errorMsg.set(`Erro no upload de ${err.fileName}: ${serverMsg ?? 'ver consola'}`);
        } else {
          this.errorMsg.set(err.error?.message || err.message || 'Erro ao guardar incidente.');
        }
      }
    });
  }

  // ── Resolver / Fechar ─────────────────────────────────────────────────────

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
      error: e => { this.isResolvendo.set(false); this.errorMsg.set(e.message); },
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

  // ── Delete ────────────────────────────────────────────────────────────────

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

  // ── PDF ───────────────────────────────────────────────────────────────────

  imprimirPdf(i: Incidente, event?: Event): void {
    if (event) event.stopPropagation();
    const fields: PdfField[] = [
      { label: 'Nº Incidente',    value: i.numeroIncidente },
      { label: 'Tipo',            value: this.getTipoLabel(i.tipo) },
      { label: 'Gravidade',       value: i.gravidade },
      { label: 'Status',          value: this.getStatusLabel(i.status) },
      { label: 'Título',          value: i.titulo },
      { label: 'Descrição',       value: i.descricao         ?? '—' },
      { label: 'Data Ocorrência', value: this.formatarData(i.dataOcorrencia) },
      { label: 'Viagem',          value: i.viagemNumero      ?? '—' },
      { label: 'Veículo',         value: i.veiculoMatricula  ?? '—' },
      { label: 'Cliente',         value: i.clienteNome       ?? '—' },
      { label: 'Causa',           value: i.causa             ?? '—' },
      { label: 'Ação Corretiva',  value: i.acaoCorretiva     ?? '—' },
      { label: 'Responsável',     value: i.responsavelResolucao ?? '—' },
      { label: 'Custo (€)',       value: i.custoAssociado    ?? 0 },
      { label: 'Total Anexos',    value: i.totalAnexos },
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

  // ── Helpers ───────────────────────────────────────────────────────────────

  private _resetForm(): void {
    this.form.reset({ gravidade: 'Media' });
    this.errorMsg.set(null);
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
      descricao:           inc.descricao          ?? '',
      dataOcorrencia:      inc.dataOcorrencia?.split('T')[0] ?? '',
      dataResolucao:       inc.dataResolucao?.split('T')[0]  ?? '',
      viagemId:            inc.viagemId            ?? null,
      veiculoId:           inc.veiculoId           ?? null,
      clienteId:           inc.clienteId           ?? null,
      atribuicaoId:        inc.atribuicaoId        ?? null,
      causa:               inc.causa               ?? '',
      acaoCorretiva:       inc.acaoCorretiva       ?? '',
      responsavelResolucao:inc.responsavelResolucao ?? '',
      custoAssociado:      inc.custoAssociado      ?? null,
      observacoes:         inc.observacoes         ?? '',
    });
    if (inc.viagemNumero)     this.searchViagem.set(inc.viagemNumero);
    if (inc.veiculoMatricula) this.searchVeiculo.set(inc.veiculoMatricula);
    if (inc.clienteNome)      this.searchCliente.set(inc.clienteNome);
    this.errorMsg.set(null);
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
      Resolvido: 'status-Resolvido', Fechado: 'status-fechado',
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

  podeResolver(inc: Incidente): boolean { return inc.status === 'Aberto' || inc.status === 'EmAnalise'; }
  podeFechar(inc: Incidente):   boolean { return inc.status === 'Resolvido'; }
}