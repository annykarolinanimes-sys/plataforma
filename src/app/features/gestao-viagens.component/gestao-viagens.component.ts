import { Component, OnInit, OnDestroy, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators, AbstractControl, ValidatorFn } from '@angular/forms';
import { Subject, debounceTime, distinctUntilChanged, takeUntil } from 'rxjs';
import { GestaoViagemService, GestaoViagem, PagedResult, GestaoViagemCreateDto, GestaoViagemUpdateDto } from '../../core/services/gestao-viagens.service';
import { PdfService, PdfField } from '../../core/services/pdf.service';
import { VeiculosService, Veiculo } from '../../core/services/veiculos.service';
import { TransportadorasService, Transportadora } from '../../core/services/transportadoras-catalogo.service';
import { ClientesCatalogoService, ClienteModel } from '../../core/services/clientes-catalogo.service';
import { UiStateService } from '../../core/services/ui-state.service';

function dataFimValidator(): ValidatorFn {
  return (group: AbstractControl) => {
    const inicio = group.get('dataInicioPlaneada')?.value;
    const fim = group.get('dataFimPlaneada')?.value;
    if (inicio && fim && new Date(fim) < new Date(inicio)) {
      return { dataFimAnterior: true };
    }
    return null;
  };
}

@Component({
  selector: 'app-gestao-viagens',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './gestao-viagens.component.html',
  styleUrls: ['./gestao-viagens.component.css'],
})
export class GestaoViagensComponent implements OnInit, OnDestroy {

  private readonly svc = inject(GestaoViagemService);
  private readonly veiculosService = inject(VeiculosService);
  private readonly transportadorasService = inject(TransportadorasService);
  private readonly clientesService = inject(ClientesCatalogoService);
  private readonly fb = inject(FormBuilder);
  private readonly pdfService = inject(PdfService);
  readonly uiState = inject(UiStateService);
  private readonly destroy$ = new Subject<void>();

  currentState = this.uiState.currentGestaoViagemState;
  editingId = this.uiState.currentGestaoViagemId;
  isViewing = signal(false);
  isLoading = signal(false);
  isSaving = signal(false);
  errorMsg = signal<string | null>(null);
  successMsg = signal<string | null>(null);

  showDeleteConfirm = signal(false);
  viagemParaDelete = signal<GestaoViagem | null>(null);

  pagedResult = signal<PagedResult<GestaoViagem> | null>(null);
  viagens = computed(() => this.pagedResult()?.items ?? []);

  veiculos = signal<Veiculo[]>([]);
  transportadoras = signal<Transportadora[]>([]);
  clientes = signal<ClienteModel[]>([]);

  clienteSearchTerm = signal('');
  showClienteDropdown = signal(false);
  filteredClientes = computed(() => {
    const term = this.clienteSearchTerm().trim().toLowerCase();
    if (term.length < 2) return [] as ClienteModel[];
    return this.clientes()
      .filter(c =>
        (c.codigo ?? '').toLowerCase().includes(term) ||
        c.nome.toLowerCase().includes(term) ||
        (c.contribuinte ?? '').toLowerCase().includes(term)
      ).slice(0, 10);
  });

  veiculoSearchTerm = signal('');
  showVeiculoDropdown = signal(false);
  filteredVeiculos = computed(() => {
    const term = this.veiculoSearchTerm().trim().toLowerCase();
    if (term.length < 2) return [] as Veiculo[];
    return this.veiculos()
      .filter(v =>
        v.matricula.toLowerCase().includes(term) ||
        (v.marca ?? '').toLowerCase().includes(term) ||
        (v.modelo ?? '').toLowerCase().includes(term)
      ).slice(0, 10);
  });

  transportadoraSearchTerm = signal('');
  showTransportadoraDropdown = signal(false);
  filteredTransportadoras = computed(() => {
    const term = this.transportadoraSearchTerm().trim().toLowerCase();
    if (term.length < 2) return [] as Transportadora[];
    return this.transportadoras()
      .filter(t =>
        (t.codigo ?? '').toLowerCase().includes(term) ||
        t.nome.toLowerCase().includes(term) ||
        (t.nif ?? '').toLowerCase().includes(term)
      ).slice(0, 10);
  });

  filtroStatus = signal('');
  filtroSearch = signal('');
  currentPage = signal(1);
  readonly pageSize = 15;

  totalViagens = computed(() => this.pagedResult()?.total ?? 0);
  totalPages = computed(() => this.pagedResult()?.totalPages ?? 0);
  pages = computed(() => Array.from({ length: this.totalPages() }, (_, i) => i + 1));

  viagensPlaneadas = computed(() => this.viagens().filter(v => v.status === 'Planeada').length);
  viagensEmCurso = computed(() => this.viagens().filter(v => v.status === 'EmCurso').length);
  viagensConcluidas = computed(() => this.viagens().filter(v => v.status === 'Concluida').length);
  viagensAtrasadas = computed(() => this.viagens().filter(v => v.atrasoHoras && v.atrasoHoras > 0).length);

  private viagemEmEdicao = signal<GestaoViagem | null>(null);

  readonly statusList = ['Planeada', 'EmCurso', 'Concluida', 'Cancelada'] as const;
  readonly prioridades = ['Baixa', 'Media', 'Alta', 'Urgente'] as const;

  form!: FormGroup;
  private readonly searchInput$ = new Subject<string>();

  isListView = () => this.currentState() === 'list';
  isCreateView = () => this.currentState() === 'create';
  isEditView = () => this.currentState() === 'edit';
  isViewingMode = () => this.isViewing();

  viagemSelecionada(): GestaoViagem | null {
    return this.viagemEmEdicao();
  }

  ngOnInit(): void {
    this.initForm();
    this.setupSearchListener();
    this.carregarDadosIniciais();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private setupSearchListener(): void {
    this.searchInput$
      .pipe(debounceTime(400), distinctUntilChanged(), takeUntil(this.destroy$))
      .subscribe(() => {
        this.currentPage.set(1);
        this.carregarViagens();
      });
  }

  private carregarDadosIniciais(): void {
    this.carregarVeiculos();
    this.carregarTransportadoras();
    this.carregarClientes();
    this.carregarViagens();
  }

  private initForm(): void {
    this.form = this.fb.group({
      prioridade: ['Media', Validators.required],
      dataInicioPlaneada: [''],
      dataFimPlaneada: [''],
      dataInicioReal: [{ value: '', disabled: true }],
      dataFimReal: [{ value: '', disabled: true }],
      distanciaPercorridaKm: [{ value: 0, disabled: true }, Validators.min(0)],
      veiculoId: [null],
      clienteId: [null],
      transportadoraId: [null],
      origemNome: ['', Validators.maxLength(300)],
      destinoNome: ['', [Validators.required, Validators.maxLength(300)]],
      precoPorKm: [0, Validators.min(0)],
      cargaDescricao: ['', Validators.maxLength(500)],
      cargaPeso: [0, [Validators.required, Validators.min(0)]],
      cargaVolume: [0, [Validators.required, Validators.min(0)]],
      cargaObservacoes: ['', Validators.maxLength(500)],
      distanciaTotalKm: [0, [Validators.required, Validators.min(0)]],
      tempoEstimadoHoras: [null, Validators.min(0)],
      observacoes: ['', Validators.maxLength(1000)],
    }, { validators: dataFimValidator() });
  }

  ctrl(name: string): AbstractControl {
    return this.form.get(name)!;
  }

  hasError(fieldName: string, errorType?: string): boolean {
    const control = this.ctrl(fieldName);
    if (!control) return false;
    if (errorType) return control.touched && control.hasError(errorType);
    return control.touched && control.invalid;
  }

  hasFormError(errorKey: string): boolean {
    return this.form.hasError(errorKey) &&
      (this.form.get('dataInicioPlaneada')?.touched === true ||
       this.form.get('dataFimPlaneada')?.touched === true);
  }

  private aplicarGuardEmCurso(viagem: GestaoViagem): void {
    const emCurso = viagem.status === 'EmCurso';

    const camposPlaneamento = [
      'prioridade', 'dataInicioPlaneada', 'dataFimPlaneada',
      'veiculoId', 'transportadoraId', 'clienteId',
      'origemNome', 'destinoNome', 'precoPorKm',
      'cargaDescricao', 'cargaPeso', 'cargaVolume', 'cargaObservacoes',
      'distanciaTotalKm', 'tempoEstimadoHoras',
    ];

    camposPlaneamento.forEach(f => {
      emCurso ? this.ctrl(f).disable() : this.ctrl(f).enable();
    });

    const camposExecucao = ['dataInicioReal', 'dataFimReal', 'distanciaPercorridaKm'];
    camposExecucao.forEach(f => {
      emCurso ? this.ctrl(f).enable() : this.ctrl(f).disable();
    });
  }

  carregarViagens(): void {
    this.isLoading.set(true);
    this.svc.listar({
      status: this.filtroStatus() || undefined,
      search: this.filtroSearch() || undefined,
      page: this.currentPage(),
      pageSize: this.pageSize,
    }).subscribe({
      next: r => {
        this.pagedResult.set(r);
        this.isLoading.set(false);
      },
      error: err => {
        this.errorMsg.set(err.message ?? 'Erro ao carregar viagens.');
        this.isLoading.set(false);
      },
    });
  }

  carregarVeiculos(): void {
    this.veiculosService.listar({ ativo: true, pageSize: 500 }).subscribe({
      next: r => this.veiculos.set(r?.items ?? []),
      error: () => this.veiculos.set([]),
    });
  }

  carregarTransportadoras(): void {
    this.transportadorasService.listar({ ativo: true, pageSize: 500 }).subscribe({
      next: d => this.transportadoras.set(d?.items ?? []),
      error: () => this.transportadoras.set([]),
    });
  }

  carregarClientes(): void {
    this.clientesService.listar({ ativo: true, pageSize: 500 }).subscribe({
      next: r => this.clientes.set(r?.items ?? []),
      error: () => this.clientes.set([]),
    });
  }

  onClienteSearch(value: string): void {
    this.clienteSearchTerm.set(value || '');
    this.showClienteDropdown.set(this.clienteSearchTerm().trim().length >= 2 && this.filteredClientes().length > 0);
    this.form.patchValue({ clienteId: null });
  }

  onClienteFocus(): void {
    this.showClienteDropdown.set(this.filteredClientes().length > 0);
  }

  closeClienteDropdown(): void {
    setTimeout(() => this.showClienteDropdown.set(false), 120);
  }

  selecionarCliente(c: ClienteModel): void {
    this.form.patchValue({ clienteId: c.id });
    this.clienteSearchTerm.set('');
    this.showClienteDropdown.set(false);
  }

  getVeiculoSelecionado(): Veiculo | null {
    const id = this.form.get('veiculoId')?.value;
    return this.veiculos().find(v => String(v.id) === String(id)) ?? null;
  }

  onVeiculoSearch(value: string): void {
    this.veiculoSearchTerm.set(value || '');
    this.showVeiculoDropdown.set(this.veiculoSearchTerm().trim().length >= 2 && this.filteredVeiculos().length > 0);
    this.form.patchValue({ veiculoId: null });
  }

  onVeiculoFocus(): void {
    this.showVeiculoDropdown.set(this.filteredVeiculos().length > 0);
  }

  closeVeiculoDropdown(): void {
    setTimeout(() => this.showVeiculoDropdown.set(false), 120);
  }

  selecionarVeiculo(v: Veiculo): void {
    this.form.patchValue({ veiculoId: v.id });
    this.veiculoSearchTerm.set('');
    this.showVeiculoDropdown.set(false);
  }

  getTransportadoraSelecionado(): Transportadora | null {
    const id = this.form.get('transportadoraId')?.value;
    return this.transportadoras().find(t => String(t.id) === String(id)) ?? null;
  }

  onTransportadoraSearch(value: string): void {
    this.transportadoraSearchTerm.set(value || '');
    const term = this.transportadoraSearchTerm().trim();
    const shouldShow = term.length >= 2 && this.filteredTransportadoras().length > 0;
    this.showTransportadoraDropdown.set(shouldShow);
    if (term.length === 0) {
      this.form.patchValue({ transportadoraId: null });
    }
  }

  onTransportadoraFocus(): void {
    this.showTransportadoraDropdown.set(this.filteredTransportadoras().length > 0);
  }

  closeTransportadoraDropdown(): void {
    setTimeout(() => this.showTransportadoraDropdown.set(false), 120);
  }

  selecionarTransportadora(t: Transportadora): void {
    this.form.patchValue({ transportadoraId: t.id });
    this.transportadoraSearchTerm.set('');
    this.showTransportadoraDropdown.set(false);
  }

  onSearchChange(value: string): void {
    this.filtroSearch.set(value);
    this.searchInput$.next(value);
  }

  onStatusChange(value: string): void {
    this.filtroStatus.set(value);
    this.currentPage.set(1);
    this.carregarViagens();
  }

  goToPage(page: number): void {
    if (page < 1 || page > this.totalPages()) return;
    this.currentPage.set(page);
    this.carregarViagens();
  }

  goToCreate(): void {
    this.isViewing.set(false);
    this.viagemEmEdicao.set(null);
    this.resetForm();
    this.uiState.goToGestaoViagemCreate();
  }

  goToEdit(viagem: GestaoViagem, event?: Event): void {
    if (event) event.stopPropagation();
    this.isViewing.set(false);
    this.patchViagem(viagem);
    this.uiState.goToGestaoViagemEdit(viagem.id);
  }

  goToDetails(viagem: GestaoViagem, event?: Event): void {
    if (event) event.stopPropagation();
    this.patchViagem(viagem);
    this.isViewing.set(true);
    this.uiState.goToGestaoViagemEdit(viagem.id);
  }

  goToList(): void {
    this.isViewing.set(false);
    this.viagemEmEdicao.set(null);
    this.uiState.goToGestaoViagemList();
    this.resetForm();
    this.carregarViagens();
  }

  cancel(): void {
    this.goToList();
  }

  clearError(): void {
    this.errorMsg.set(null);
  }

  salvarViagem(): void {
    this.form.markAllAsTouched();

    if (this.form.hasError('dataFimAnterior')) {
      this.errorMsg.set('A data/hora de fim não pode ser anterior à data/hora de início.');
      return;
    }

    const camposObrigatorios = [];
    if (!this.ctrl('destinoNome').value) camposObrigatorios.push('Destino');

    if (camposObrigatorios.length > 0) {
      this.errorMsg.set(`Campos obrigatórios não preenchidos: ${camposObrigatorios.join(', ')}`);
      return;
    }

    if (this.form.invalid) {
      this.errorMsg.set('Corrija os erros no formulário antes de continuar.');
      return;
    }

    this.isSaving.set(true);
    this.errorMsg.set(null);
    const raw = this.form.getRawValue();

    if (this.isEditView() && this.editingId()) {
      const dto: GestaoViagemUpdateDto = {
        prioridade: raw.prioridade,
        dataInicioPlaneada: raw.dataInicioPlaneada || undefined,
        dataFimPlaneada: raw.dataFimPlaneada || undefined,
        dataInicioReal: raw.dataInicioReal || undefined,
        dataFimReal: raw.dataFimReal || undefined,
        veiculoId: raw.veiculoId || undefined,
        transportadoraId: raw.transportadoraId || undefined,
        clienteId: raw.clienteId || undefined,
        origem: raw.origemNome?.trim() || undefined,
        destino: raw.destinoNome?.trim() || undefined,
        precoPorKm: +raw.precoPorKm || 0,
        cargaDescricao: raw.cargaDescricao?.trim() || undefined,
        cargaPeso: +raw.cargaPeso,
        cargaVolume: +raw.cargaVolume,
        cargaObservacoes: raw.cargaObservacoes?.trim() || undefined,
        distanciaTotalKm: +raw.distanciaTotalKm,
        distanciaPercorridaKm: +raw.distanciaPercorridaKm || undefined,
        tempoEstimadoHoras: raw.tempoEstimadoHoras || undefined,
        observacoes: raw.observacoes?.trim() || undefined,
      };
      this.svc.atualizar(this.editingId()!, dto).subscribe({
        next: () => this.onSaveSuccess('Viagem atualizada com sucesso.'),
        error: e => this.onSaveError(e.message),
      });
    } else {
      const dto: GestaoViagemCreateDto = {
        prioridade: raw.prioridade,
        dataInicioPlaneada: raw.dataInicioPlaneada || undefined,
        dataFimPlaneada: raw.dataFimPlaneada || undefined,
        veiculoId: raw.veiculoId || undefined,
        transportadoraId: raw.transportadoraId || undefined,
        clienteId: raw.clienteId || undefined,
        origem: raw.origemNome?.trim() || undefined,
        destino: raw.destinoNome?.trim() || undefined,
        precoPorKm: +raw.precoPorKm || 0,
        cargaDescricao: raw.cargaDescricao?.trim() || undefined,
        cargaPeso: +raw.cargaPeso,
        cargaVolume: +raw.cargaVolume,
        cargaObservacoes: raw.cargaObservacoes?.trim() || undefined,
        distanciaTotalKm: +raw.distanciaTotalKm,
        tempoEstimadoHoras: raw.tempoEstimadoHoras || undefined,
        observacoes: raw.observacoes?.trim() || undefined,
      };
      this.svc.criar(dto).subscribe({
        next: () => this.onSaveSuccess('Viagem criada com sucesso.'),
        error: e => this.onSaveError(e.message),
      });
    }
  }

  iniciarViagem(viagem: GestaoViagem, event?: Event): void {
    if (event) event.stopPropagation();
    this.svc.iniciar(viagem.id).subscribe({
      next: () => { this.carregarViagens(); this.showToast('Viagem iniciada com sucesso.'); },
      error: e => this.errorMsg.set(e.message),
    });
  }

  concluirViagem(viagem: GestaoViagem, event?: Event): void {
    if (event) event.stopPropagation();
    this.svc.concluir(viagem.id).subscribe({
      next: () => { this.carregarViagens(); this.showToast('Viagem concluída com sucesso.'); },
      error: e => this.errorMsg.set(e.message),
    });
  }

  confirmarDelete(viagem: GestaoViagem, event?: Event): void {
    if (event) event.stopPropagation();
    this.viagemParaDelete.set(viagem);
    this.showDeleteConfirm.set(true);
  }

  cancelarDelete(): void {
    this.showDeleteConfirm.set(false);
    this.viagemParaDelete.set(null);
  }

  executarDelete(): void {
    const viagem = this.viagemParaDelete();
    if (!viagem) return;

    this.svc.deletar(viagem.id).subscribe({
      next: () => {
        this.cancelarDelete();
        this.carregarViagens();
        this.showToast('Viagem cancelada com sucesso.');
      },
      error: e => {
        this.errorMsg.set(e.message);
        this.cancelarDelete();
      },
    });
  }

  imprimirPdf(viagem: GestaoViagem | null, event?: Event): void {
    if (!viagem) return;
    if (event) event.stopPropagation();

    const fields: PdfField[] = [
      { label: 'Nº Viagem', value: viagem.numeroViagem },
      { label: 'Status', value: this.getStatusLabel(viagem.status) },
      { label: 'Prioridade', value: viagem.prioridade },
      { label: 'Cliente', value: viagem.clienteNome ?? '—' },
      { label: 'Motorista', value: viagem.motoristaNome ?? '—' },
      { label: 'Veículo', value: viagem.veiculoMatricula ?? '—' },
      { label: 'Transportadora', value: viagem.transportadoraNome ?? '—' },
      { label: 'Origem', value: viagem.origem ?? '—' },
      { label: 'Destino', value: viagem.destino ?? '—' },
      { label: 'Início Planeado', value: this.formatarDataHora(viagem.dataInicioPlaneada) },
      { label: 'Fim Planeado', value: this.formatarDataHora(viagem.dataFimPlaneada) },
      { label: 'Distância (km)', value: viagem.distanciaTotalKm ?? 0 },
      { label: 'Carga', value: viagem.cargaDescricao ?? '—' },
      { label: 'Peso (kg)', value: viagem.cargaPeso ?? 0 },
      { label: 'Volume (m³)', value: viagem.cargaVolume ?? 0 },
      { label: 'Tempo Estimado', value: this.formatarTempo(viagem.tempoEstimadoHoras) },
    ];

    try {
      const blob = this.pdfService.generateEntityPdf(
        `Viagem ${viagem.numeroViagem}`, fields, 'Documento gerado automaticamente.'
      );
      this.pdfService.downloadPdf(blob, `Viagem_${viagem.numeroViagem}.pdf`);
    } catch {
      this.errorMsg.set('Erro ao gerar PDF da viagem.');
    }
  }

  private resetForm(): void {
    this.form.reset({
      prioridade: 'Media',
      cargaPeso: 0,
      cargaVolume: 0,
      distanciaTotalKm: 0,
      precoPorKm: 0
    });
    this.clienteSearchTerm.set('');
    this.showClienteDropdown.set(false);
    this.veiculoSearchTerm.set('');
    this.showVeiculoDropdown.set(false);
    this.transportadoraSearchTerm.set('');
    this.showTransportadoraDropdown.set(false);
    this.errorMsg.set(null);
  }

  private patchViagem(viagem: GestaoViagem): void {
    this.viagemEmEdicao.set(viagem);
    this.form.patchValue({
      prioridade: viagem.prioridade,
      dataInicioPlaneada: viagem.dataInicioPlaneada?.slice(0, 16) ?? '',
      dataFimPlaneada: viagem.dataFimPlaneada?.slice(0, 16) ?? '',
      dataInicioReal: viagem.dataInicioReal?.slice(0, 16) ?? '',
      dataFimReal: viagem.dataFimReal?.slice(0, 16) ?? '',
      veiculoId: viagem.veiculoId ?? null,
      clienteId: viagem.clienteId ?? null,
      transportadoraId: viagem.transportadoraId ?? null,
      origemNome: viagem.origem ?? '',
      destinoNome: viagem.destino ?? '',
      precoPorKm: viagem.precoPorKm ?? 0,
      cargaDescricao: viagem.cargaDescricao ?? '',
      cargaPeso: viagem.cargaPeso ?? 0,
      cargaVolume: viagem.cargaVolume ?? 0,
      cargaObservacoes: viagem.cargaObservacoes ?? '',
      distanciaTotalKm: viagem.distanciaTotalKm ?? 0,
      distanciaPercorridaKm: viagem.distanciaPercorridaKm ?? 0,
      tempoEstimadoHoras: viagem.tempoEstimadoHoras ?? null,
      observacoes: viagem.observacoes ?? '',
    });
    this.aplicarGuardEmCurso(viagem);
    this.errorMsg.set(null);
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

  progressoViagem(viagem: GestaoViagem): number {
    if (viagem.status === 'Concluida') return 100;
    if (viagem.status === 'Cancelada') return 0;
    if (!viagem.distanciaTotalKm) return 0;
    return Math.min(100, Math.round(((viagem.distanciaPercorridaKm ?? 0) / viagem.distanciaTotalKm) * 100));
  }

  getStatusLabel(status: string): string {
    const labels: Record<string, string> = {
      Planeada: 'Planeada', EmCurso: 'Em Curso',
      Concluida: 'Concluída', Cancelada: 'Cancelada',
    };
    return labels[status] ?? status;
  }

  getStatusClass(status: string): string {
    const classes: Record<string, string> = {
      Planeada: 'status-planeada', EmCurso: 'status-emcurso',
      Concluida: 'status-concluida', Cancelada: 'status-cancelada',
    };
    return classes[status] ?? 'status-planeada';
  }

  getPrioridadeClass(prioridade: string): string {
    const classes: Record<string, string> = {
      Baixa: 'prioridade-baixa', Media: 'prioridade-media',
      Alta: 'prioridade-alta', Urgente: 'prioridade-urgente',
    };
    return classes[prioridade] ?? 'prioridade-media';
  }

  formatarKm(distancia: number): string {
    if (!distancia) return '—';
    return `${distancia.toLocaleString('pt-PT')} km`;
  }

  formatarTempo(horas?: number | null): string {
    if (!horas) return '—';
    const h = Math.floor(horas);
    const m = Math.round((horas - h) * 60);
    return m > 0 ? `${h}h ${m}m` : `${h}h`;
  }

  formatarData(data?: string): string {
    if (!data) return '—';
    return new Date(data).toLocaleDateString('pt-PT', {
      day: '2-digit', month: '2-digit', year: 'numeric',
    });
  }

  formatarDataHora(data?: string): string {
    if (!data) return '—';
    return new Date(data).toLocaleDateString('pt-PT', {
      day: '2-digit', month: '2-digit', year: 'numeric',
      hour: '2-digit', minute: '2-digit',
    });
  }
}