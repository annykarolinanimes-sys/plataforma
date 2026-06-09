import { Component, OnInit, OnDestroy, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators, AbstractControl, FormArray, ValidatorFn } from '@angular/forms';
import { Subject, debounceTime, distinctUntilChanged, takeUntil } from 'rxjs';
import { PdfService, PdfField } from '../../core/services/pdf.service';
import { AtribuicaoService, Atribuicao, PagedResult, AtribuicaoCreateDto, AtribuicaoUpdateDto } from '../../core/services/atribuicao.service';
import { UiStateService } from '../../core/services/ui-state.service';
import { MotoristasService, Motorista } from '../../core/services/motoristas.service';
import { VeiculosService, Veiculo } from '../../core/services/veiculos.service';
import { TransportadorasService, Transportadora } from '../../core/services/transportadoras-catalogo.service';
import { ClientesCatalogoService, ClienteModel } from '../../core/services/clientes-catalogo.service';

function dataFimValidator(): ValidatorFn {
  return (group: AbstractControl) => {
    const inicio = group.get('dataPrevistaInicio')?.value;
    const fim = group.get('dataPrevistaFim')?.value;
    if (inicio && fim && new Date(fim) < new Date(inicio)) {
      return { dataFimAnterior: true };
    }
    return null;
  };
}

@Component({
  selector: 'app-atribuicao',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './atribuicao.component.html',
  styleUrls: ['./atribuicao.component.css'],
})
export class AtribuicaoComponent implements OnInit, OnDestroy {

  private readonly svc = inject(AtribuicaoService);
  private readonly fb = inject(FormBuilder);
  private readonly pdfService = inject(PdfService);
  readonly uiState = inject(UiStateService);
  // CORRIGIDO: usar UserService para motoristas (retorna array direto, não paginado)
  private readonly motoristaService = inject(MotoristasService);
  private readonly veiculosService = inject(VeiculosService);
  private readonly transportadorasService = inject(TransportadorasService);
  private readonly clientesService = inject(ClientesCatalogoService);
  private readonly destroy$ = new Subject<void>();

  currentState = this.uiState.currentAtribuicaoState;
  editingId = this.uiState.currentAtribuicaoId;
  isViewing = signal(false);
  selectedAtribuicao = computed(() => this.atribuicoes().find(a => a.id === this.editingId()) ?? null);

  isListView() { return this.currentState() === 'list'; }
  isCreateView() { return this.currentState() === 'create'; }
  isEditView() { return this.currentState() === 'edit'; }

  pagedResult = signal<PagedResult<Atribuicao> | null>(null);
  atribuicoes = computed(() => this.pagedResult()?.items ?? []);

  motoristas = signal<Motorista[]>([]);
  veiculos = signal<Veiculo[]>([]);
  transportadoras = signal<Transportadora[]>([]);
  clientes = signal<ClienteModel[]>([]);

  isLoading = signal(false);
  isSaving = signal(false);
  errorMsg = signal<string | null>(null);
  successMsg = signal<string | null>(null);

  clienteSearchTerm = signal('');
  showClienteDropdown = signal(false);

  filteredClientes = computed(() => {
    const term = this.clienteSearchTerm().trim().toLowerCase();
    if (term.length < 2) return [] as ClienteModel[];
    return this.clientes()
      .filter(c => {
        const idFormatado = c.id.toString().padStart(3, '0');
        const termSemZeros = term.replace(/^0+/, '');
        return c.nome.toLowerCase().includes(term) ||
          (c.contribuinte?.toLowerCase().includes(term) ?? false) ||
          (c.telefone?.toLowerCase().includes(term) ?? false) ||
          (c.email?.toLowerCase().includes(term) ?? false) ||
          idFormatado.includes(term) ||
          c.id.toString().includes(termSemZeros);
      }).slice(0, 10);
  });

  motoristaSearchTerm = signal('');
  showMotoristaDropdown = signal(false);

  filteredMotoristas = computed(() => {
    const term = this.motoristaSearchTerm().trim().toLowerCase();
    const isNumericSearch = /^\d+$/.test(term);
    if (term.length < 2 && !isNumericSearch) return [] as Motorista[];

    const motoristasAtivos = this.motoristas().filter(m => m.ativo === true);

    return motoristasAtivos
      .filter(m => {
        const idFormatado = m.id.toString().padStart(3, '0');
        const termSemZeros = term.replace(/^0+/, '');
        return m.nome.toLowerCase().includes(term) ||
          (m.telefone?.toLowerCase().includes(term) ?? false) ||
          (m.cartaConducao?.toLowerCase().includes(term) ?? false) ||
          idFormatado.includes(term) ||
          m.id.toString().includes(termSemZeros);
      }).slice(0, 10);
  });

  veiculoSearchTerm = signal('');
  showVeiculoDropdown = signal(false);

  filteredVeiculos = computed(() => {
    const term = this.veiculoSearchTerm().trim().toLowerCase();
    if (term.length < 2) return [] as Veiculo[];
    return this.veiculos()
      .filter(v => {
        const idFormatado = v.id.toString().padStart(3, '0');
        const termSemZeros = term.replace(/^0+/, '');
        return v.matricula.toLowerCase().includes(term) ||
          (v.marca?.toLowerCase().includes(term) ?? false) ||
          (v.modelo?.toLowerCase().includes(term) ?? false) ||
          (v.vin?.toLowerCase().includes(term) ?? false) ||
          idFormatado.includes(term) ||
          v.id.toString().includes(termSemZeros);
      }).slice(0, 10);
  });

  transportadoraSearchTerm = signal('');
  showTransportadoraDropdown = signal(false);

  filteredTransportadoras = computed(() => {
    const term = this.transportadoraSearchTerm().trim().toLowerCase();
    if (term.length < 2) return [] as Transportadora[];
    return this.transportadoras()
      .filter(t => {
        const idFormatado = t.id.toString().padStart(3, '0');
        const termSemZeros = term.replace(/^0+/, '');
        return t.nome.toLowerCase().includes(term) ||
          (t.nif?.toLowerCase().includes(term) ?? false) ||
          (t.telefone?.toLowerCase().includes(term) ?? false) ||
          (t.email?.toLowerCase().includes(term) ?? false) ||
          idFormatado.includes(term) ||
          t.id.toString().includes(termSemZeros);
      }).slice(0, 10);
  });

  totalAtribuicoes = computed(() => this.pagedResult()?.total ?? 0);
  countEmRota = computed(() => this.atribuicoes().filter(a => a.status === 'EmProgresso').length);
  frotaEmUso = computed(() => {
    const ids = this.atribuicoes()
      .filter(a => a.status === 'EmProgresso' && a.veiculoId)
      .map(a => a.veiculoId);
    return new Set(ids).size;
  });
  entregasHoje = computed(() => {
    const hoje = new Date().toDateString();
    return this.atribuicoes()
      .filter(a => a.status !== 'Cancelada')
      .reduce((sum, a) => {
        const data = a.dataPrevistaInicio
          ? new Date(a.dataPrevistaInicio).toDateString()
          : '';
        return data === hoje ? sum + a.totalEntregas : sum;
      }, 0);
  });
  alertasAtraso = computed(() => {
    const agora = new Date();
    return this.atribuicoes().filter(a =>
      a.status === 'Pendente' &&
      a.dataPrevistaInicio &&
      new Date(a.dataPrevistaInicio) < agora
    ).length;
  });

  filtroStatus = '';
  filtroSearch = '';
  currentPage = 1;
  readonly pageSize = 15;

  totalPages = computed(() => this.pagedResult()?.totalPages ?? 0);
  pages = computed(() => Array.from({ length: this.totalPages() }, (_, i) => i + 1));

  showDeleteConfirm = signal(false);
  atribuicaoParaDelete = signal<Atribuicao | null>(null);

  readonly prioridades = ['Baixa', 'Media', 'Alta', 'Urgente'] as const;
  readonly statusList = ['Pendente', 'EmProgresso', 'Concluida', 'Cancelada'] as const;

  form!: FormGroup;
  private readonly searchInput$ = new Subject<string>();

  ngOnInit(): void {
    this.initForm();
    this.carregarMotoristas();
    this.carregarVeiculos();
    this.carregarTransportadoras();
    this.carregarClientes();

    this.searchInput$
      .pipe(debounceTime(400), distinctUntilChanged(), takeUntil(this.destroy$))
      .subscribe(() => { this.currentPage = 1; this.carregarAtribuicoes(); });

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
      clienteEmail: ['', Validators.maxLength(200)],
      clienteNif: ['', Validators.maxLength(50)],
      enderecoOrigem: ['', Validators.maxLength(300)],
      enderecoDestino: ['', [Validators.required, Validators.maxLength(300)]],
      dataPrevistaInicio: [''],
      dataPrevistaFim: [''],
      prioridade: ['Media', Validators.required],
      observacoes: ['', Validators.maxLength(500)],
      motoristaId: [null],
      veiculoId: [null],
      transportadoraId: [null],
      distanciaTotalKm: [0, Validators.min(0)],
      tempoEstimadoHoras: [null, Validators.min(0)],
      entregas: this.fb.array([]),
    }, { validators: dataFimValidator() });

    this.adicionarEntrega();
  }

  get entregasArray(): FormArray { return this.form.get('entregas') as FormArray; }

  criarEntregaForm(entrega?: any): FormGroup {
    return this.fb.group({
      id: [entrega?.id ?? null],
      destinatario: [entrega?.destinatario ?? '', Validators.maxLength(200)],
      endereco: [entrega?.endereco ?? '', Validators.maxLength(300)],
      contacto: [entrega?.contacto ?? '', Validators.maxLength(100)],
      observacoes: [entrega?.observacoes ?? '', Validators.maxLength(500)],
      ordem: [entrega?.ordem ?? (this.entregasArray.length + 1), Validators.min(1)],
      realizada: [entrega?.realizada ?? false],
    });
  }

  adicionarEntrega(): void { this.entregasArray.push(this.criarEntregaForm()); }

  removerEntrega(index: number): void {
    this.entregasArray.removeAt(index);
    if (this.entregasArray.length === 0) this.adicionarEntrega();
  }

  ctrl(name: string): AbstractControl { return this.form.get(name)!; }

  hasError(name: string, error?: string): boolean {
    const c = this.ctrl(name);
    if (!c.invalid || !c.touched) return false;
    return error ? c.hasError(error) : true;
  }

  hasFormError(error: string): boolean {
    return this.form.hasError(error) && (
      this.form.get('dataPrevistaInicio')?.touched ||
      this.form.get('dataPrevistaFim')?.touched
    ) === true;
  }

  entregaCtrl(i: number, name: string): AbstractControl {
    return (this.entregasArray.at(i) as FormGroup).get(name)!;
  }

  progressoEntregas(a: Atribuicao): number {
    if (!a.totalEntregas) return 0;
    return Math.round((a.entregasRealizadas / a.totalEntregas) * 100);
  }

  carregarMotoristas(): void {
    this.motoristaService.listar({ ativo: true, pageSize: 500, orderBy: 'nome', orderDir: 'asc' }).subscribe({
      next: (r) => {
        this.motoristas.set(r?.items ?? []);
      },
      error: (err) => {
        console.error('Erro ao carregar motoristas:', err);
        this.motoristas.set([]);
      },
    });
  }

  carregarVeiculos(): void {
    this.veiculosService.listar({ ativo: true, pageSize: 500 }).subscribe({
      next: (r) => this.veiculos.set(r?.items ?? []),
      error: () => this.veiculos.set([]),
    });
  }

  carregarTransportadoras(): void {
    this.transportadorasService.listar({ ativo: true }).subscribe({
      next: (d) => this.transportadoras.set(d?.items ?? []),
      error: () => this.transportadoras.set([]),
    });
  }

  carregarClientes(): void {
    this.clientesService.listar({ ativo: true, pageSize: 500 }).subscribe({
      next: r => this.clientes.set(r?.items ?? []),
      error: () => this.clientes.set([]),
    });
  }

  onClienteNomeInput(event: Event): void {
    const value = (event.target as HTMLInputElement).value;
    this.clienteSearchTerm.set(value);
    this.form.patchValue({ clienteContacto: '', clienteEmail: '', clienteNif: '' });
    this.showClienteDropdown.set(value.trim().length >= 2 && this.filteredClientes().length > 0);
  }

  onClienteNomeFocus(): void {
    this.showClienteDropdown.set(this.filteredClientes().length > 0);
  }

  closeClienteDropdown(): void {
    setTimeout(() => this.showClienteDropdown.set(false), 120);
  }

  selecionarCliente(c: ClienteModel): void {
    this.form.patchValue({
      clienteNome: c.nome,
      clienteContacto: c.telefone || '',
      clienteEmail: c.email || '',
      clienteNif: c.contribuinte || ''
    });
    this.clienteSearchTerm.set(c.nome);
    this.showClienteDropdown.set(false);
  }

  getMotoristaSelecionado(): Motorista | null {
    const id = this.form.get('motoristaId')?.value;
    if (!id) return null;
    return this.motoristas().find(m => m.id === id) ?? null;
  }

  getVeiculoSelecionado(): Veiculo | null {
    const id = this.form.get('veiculoId')?.value;
    if (!id) return null;
    return this.veiculos().find(v => v.id === id) ?? null;
  }

  getTransportadoraSelecionado(): Transportadora | null {
    const id = this.form.get('transportadoraId')?.value;
    if (!id) return null;
    return this.transportadoras().find(t => t.id === id) ?? null;
  }

  onMotoristaSearch(value: string): void {
    this.motoristaSearchTerm.set(value || '');
    const term = this.motoristaSearchTerm().trim();
    const isNumericSearch = /^\d+$/.test(term);
    const shouldShow = (term.length >= 2 || isNumericSearch) && this.filteredMotoristas().length > 0;
    this.showMotoristaDropdown.set(shouldShow);
    if (!shouldShow && term.length === 0) {
      this.form.patchValue({ motoristaId: null });
    }
  }

  onMotoristaFocus(): void {
    const term = this.motoristaSearchTerm().trim();
    const isNumericSearch = /^\d+$/.test(term);
    const shouldShow = (term.length >= 2 || isNumericSearch) && this.filteredMotoristas().length > 0;
    this.showMotoristaDropdown.set(shouldShow);
  }

  closeMotoristaDropdown(): void {
    setTimeout(() => this.showMotoristaDropdown.set(false), 200);
  }

  selecionarMotorista(m: Motorista): void {
    this.form.patchValue({ motoristaId: m.id });
    this.motoristaSearchTerm.set('');
    this.showMotoristaDropdown.set(false);
  }

  limparMotorista(): void {
    this.form.patchValue({ motoristaId: null });
    this.motoristaSearchTerm.set('');
    this.showMotoristaDropdown.set(false);
  }

  onVeiculoSearch(value: string): void {
    this.veiculoSearchTerm.set(value || '');
    this.showVeiculoDropdown.set(this.veiculoSearchTerm().trim().length >= 2 && this.filteredVeiculos().length > 0);
    if (this.veiculoSearchTerm().trim().length === 0) {
      this.form.patchValue({ veiculoId: null });
    }
  }

  onVeiculoFocus(): void {
    this.showVeiculoDropdown.set(this.filteredVeiculos().length > 0);
  }

  closeVeiculoDropdown(): void {
    setTimeout(() => this.showVeiculoDropdown.set(false), 200);
  }

  selecionarVeiculo(v: Veiculo): void {
    this.form.patchValue({ veiculoId: v.id });
    this.veiculoSearchTerm.set('');
    this.showVeiculoDropdown.set(false);
  }

  limparVeiculo(): void {
    this.form.patchValue({ veiculoId: null });
    this.veiculoSearchTerm.set('');
    this.showVeiculoDropdown.set(false);
  }

  onTransportadoraSearch(value: string): void {
    this.transportadoraSearchTerm.set(value || '');
    this.showTransportadoraDropdown.set(this.transportadoraSearchTerm().trim().length >= 2 && this.filteredTransportadoras().length > 0);
    if (this.transportadoraSearchTerm().trim().length === 0) {
      this.form.patchValue({ transportadoraId: null });
    }
  }

  onTransportadoraFocus(): void {
    this.showTransportadoraDropdown.set(this.filteredTransportadoras().length > 0);
  }

  closeTransportadoraDropdown(): void {
    setTimeout(() => this.showTransportadoraDropdown.set(false), 200);
  }

  selecionarTransportadora(t: Transportadora): void {
    this.form.patchValue({ transportadoraId: t.id });
    this.transportadoraSearchTerm.set('');
    this.showTransportadoraDropdown.set(false);
  }

  limparTransportadora(): void {
    this.form.patchValue({ transportadoraId: null });
    this.transportadoraSearchTerm.set('');
    this.showTransportadoraDropdown.set(false);
  }

  carregarAtribuicoes(): void {
    this.isLoading.set(true);
    this.svc.listar({
      status: this.filtroStatus || undefined,
      search: this.filtroSearch || undefined,
      page: this.currentPage,
      pageSize: this.pageSize,
    }).subscribe({
      next: r => { this.pagedResult.set(r); this.isLoading.set(false); },
      error: err => { this.errorMsg.set(err.message ?? 'Erro ao carregar atribuições.'); this.isLoading.set(false); },
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
    if (page < 1 || page > this.totalPages()) return;
    this.currentPage = page;
    this.carregarAtribuicoes();
  }

  goToCreate(): void {
    this.isViewing.set(false);
    this._resetForm();
    this.uiState.goToAtribuicaoCreate();
  }

  goToEdit(atribuicao: Atribuicao, event?: Event): void {
    if (event) event.stopPropagation();
    this.isViewing.set(false);
    this._patchAtribuicao(atribuicao);
    this.uiState.goToAtribuicaoEdit(atribuicao.id);
  }

  goToDetails(atribuicao: Atribuicao, event?: Event): void {
    if (event) event.stopPropagation();
    this._patchAtribuicao(atribuicao);
    this.isViewing.set(true);
    this.uiState.goToAtribuicaoEdit(atribuicao.id);
  }

  goToList(): void {
    this.isViewing.set(false);
    this.uiState.goToAtribuicaoList();
    this._resetForm();
    this.carregarAtribuicoes();
  }

  cancel(): void { this.goToList(); }

  imprimirPdf(atribuicao: Atribuicao, event?: Event): void {
    if (event) event.stopPropagation();
    const fields: PdfField[] = [
      { label: 'Nº Atribuição', value: atribuicao.numeroAtribuicao },
      { label: 'Cliente', value: atribuicao.clienteNome ?? '—' },
      { label: 'Contacto', value: atribuicao.clienteContacto ?? '—' },
      { label: 'Origem', value: atribuicao.enderecoOrigem ?? '—' },
      { label: 'Destino', value: atribuicao.enderecoDestino ?? '—' },
      { label: 'Início previsto', value: this.formatarData(atribuicao.dataPrevistaInicio ?? '') },
      { label: 'Fim previsto', value: this.formatarData(atribuicao.dataPrevistaFim ?? '') },
      { label: 'Prioridade', value: atribuicao.prioridade },
      { label: 'Status', value: this.getStatusLabel(atribuicao.status) },
      { label: 'Motorista', value: atribuicao.motoristaNome ?? '—' },
      { label: 'Veículo', value: atribuicao.veiculoMatricula ?? '—' },
      { label: 'Transportadora', value: atribuicao.transportadoraNome ?? '—' },
      { label: 'Distância (km)', value: atribuicao.distanciaTotalKm ?? 0 },
      { label: 'Tempo estimado', value: atribuicao.tempoEstimadoHoras ? `${atribuicao.tempoEstimadoHoras}h` : '—' },
      { label: 'Total Entregas', value: atribuicao.totalEntregas },
      { label: 'Realizadas', value: atribuicao.entregasRealizadas },
    ];
    (atribuicao.entregas ?? []).forEach((e, i) => {
      fields.push({ label: `Entrega ${i + 1}`, value: `${e.destinatario ?? '—'} — ${e.endereco ?? '—'}` });
    });
    try {
      const blob = this.pdfService.generateEntityPdf(
        `Atribuição ${atribuicao.numeroAtribuicao}`, fields, 'Documento gerado automaticamente.'
      );
      this.pdfService.downloadPdf(blob, `Atribuicao_${atribuicao.numeroAtribuicao}.pdf`);
    } catch {
      this.errorMsg.set('Erro ao gerar PDF da atribuição.');
    }
  }

  salvarAtribuicao(): void {
    this.form.markAllAsTouched();

    if (this.form.hasError('dataFimAnterior')) {
      this.errorMsg.set('A data de fim não pode ser anterior à data de início.');
      return;
    }

    if (this.form.invalid) {
      this.errorMsg.set('Corrija os erros no formulário antes de continuar.');
      return;
    }

    this.isSaving.set(true);
    this.errorMsg.set(null);
    const v = this.form.getRawValue();

    const entregas = v.entregas
      .map((e: any, idx: number) => ({
        id: e.id || undefined,
        destinatario: e.destinatario?.trim() || undefined,
        endereco: e.endereco?.trim() || undefined,
        contacto: e.contacto?.trim() || undefined,
        observacoes: e.observacoes?.trim() || undefined,
        ordem: e.ordem || idx + 1,
        realizada: !!e.realizada,
      }))
      .filter((e: any) => e.destinatario);

    const base: AtribuicaoCreateDto = {
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
      distanciaTotalKm: +v.distanciaTotalKm || 0,
      tempoEstimadoHoras: v.tempoEstimadoHoras || undefined,
      entregas: entregas.length > 0 ? entregas : undefined,
    };

    if (this.isEditView() && this.editingId()) {
      const updateDto: AtribuicaoUpdateDto = { ...base } as AtribuicaoUpdateDto;
      this.svc.atualizar(this.editingId()!, updateDto).subscribe({
        next: () => this._onSaveSuccess('Atribuição atualizada com sucesso.'),
        error: e => this._onSaveError(e.message),
      });
    } else {
      this.svc.criar(base).subscribe({
        next: () => this._onSaveSuccess('Atribuição criada com sucesso.'),
        error: e => this._onSaveError(e.message),
      });
    }
  }

  iniciarAtribuicao(atribuicao: Atribuicao, event?: Event): void {
    if (event) event.stopPropagation();
    if (!confirm(`Iniciar atribuição ${atribuicao.numeroAtribuicao}?`)) return;
    this.svc.iniciar(atribuicao.id).subscribe({
      next: () => { this.carregarAtribuicoes(); this.showToast('Atribuição iniciada com sucesso.'); },
      error: e => this.errorMsg.set(e.message),
    });
  }

  concluirAtribuicao(atribuicao: Atribuicao, event?: Event): void {
    if (event) event.stopPropagation();
    if (!confirm(`Concluir atribuição ${atribuicao.numeroAtribuicao}?`)) return;
    this.svc.concluir(atribuicao.id).subscribe({
      next: () => { this.carregarAtribuicoes(); this.showToast('Atribuição concluída com sucesso.'); },
      error: e => this.errorMsg.set(e.message),
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
      next: () => { this.cancelarDelete(); this.carregarAtribuicoes(); this.showToast('Atribuição cancelada.'); },
      error: e => { this.errorMsg.set(e.message); this.cancelarDelete(); },
    });
  }

  private _resetForm(): void {
    while (this.entregasArray.length) this.entregasArray.removeAt(0);
    this.form.reset({ prioridade: 'Media', distanciaTotalKm: 0 });
    this.adicionarEntrega();
    this.clienteSearchTerm.set('');
    this.showClienteDropdown.set(false);
    this.motoristaSearchTerm.set('');
    this.showMotoristaDropdown.set(false);
    this.veiculoSearchTerm.set('');
    this.showVeiculoDropdown.set(false);
    this.transportadoraSearchTerm.set('');
    this.showTransportadoraDropdown.set(false);
    this.errorMsg.set(null);
  }

  private _patchAtribuicao(a: Atribuicao): void {
    while (this.entregasArray.length) this.entregasArray.removeAt(0);
    this.form.patchValue({
      clienteNome: a.clienteNome ?? '',
      clienteContacto: a.clienteContacto ?? '',
      clienteEmail: (a as any).clienteEmail ?? '',
      clienteNif: (a as any).clienteNif ?? '',
      enderecoOrigem: a.enderecoOrigem ?? '',
      enderecoDestino: a.enderecoDestino ?? '',
      dataPrevistaInicio: a.dataPrevistaInicio?.split('T')[0] ?? '',
      dataPrevistaFim: a.dataPrevistaFim?.split('T')[0] ?? '',
      prioridade: a.prioridade,
      observacoes: a.observacoes ?? '',
      motoristaId: a.motoristaId ?? null,
      veiculoId: a.veiculoId ?? null,
      transportadoraId: a.transportadoraId ?? null,
      distanciaTotalKm: a.distanciaTotalKm ?? 0,
      tempoEstimadoHoras: a.tempoEstimadoHoras ?? null,
    });
    (a.entregas ?? []).forEach(e => this.entregasArray.push(this.criarEntregaForm(e)));
    if (this.entregasArray.length === 0) this.adicionarEntrega();
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

  getStatusLabel(status: string): string {
    const m: Record<string, string> = {
      Pendente: 'Pendente', EmProgresso: 'Em Rota',
      Concluida: 'Concluída', Cancelada: 'Cancelada',
    };
    return m[status] ?? status;
  }

  getStatusClass(status: string): string {
    const m: Record<string, string> = {
      Pendente: 'status-pendente', EmProgresso: 'status-progresso',
      Concluida: 'status-concluida', Cancelada: 'status-cancelada',
    };
    return m[status] ?? 'status-pendente';
  }

  getPrioridadeClass(p: string): string {
    const m: Record<string, string> = {
      Baixa: 'prioridade-baixa', Media: 'prioridade-media',
      Alta: 'prioridade-alta', Urgente: 'prioridade-urgente',
    };
    return m[p] ?? 'prioridade-media';
  }

  formatarData(data: string): string {
    if (!data) return '—';
    return new Date(data).toLocaleDateString('pt-PT', {
      day: '2-digit', month: '2-digit', year: 'numeric',
      hour: '2-digit', minute: '2-digit',
    });
  }

  formatarDataSimples(data: string): string {
    if (!data) return '—';
    return new Date(data).toLocaleDateString('pt-PT');
  }

  isAtribuicaoFinalizada(a: Atribuicao): boolean {
    return a.status === 'Concluida' || a.status === 'Cancelada';
  }
}