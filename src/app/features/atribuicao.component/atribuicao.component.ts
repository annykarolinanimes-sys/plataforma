import {Component, OnInit, OnDestroy,inject, signal, computed} from '@angular/core';
import { CommonModule } from '@angular/common';
import {ReactiveFormsModule, FormBuilder, FormGroup,Validators, AbstractControl, FormArray, ValidatorFn} from '@angular/forms';
import { Subject, debounceTime, distinctUntilChanged, takeUntil } from 'rxjs';
import { PdfService, PdfField } from '../../core/services/pdf.service';
import {AtribuicaoService, Atribuicao,PagedResult, AtribuicaoCreateDto, AtribuicaoUpdateDto} from '../../core/services/atribuicao.service';
import { UiStateService } from '../../core/services/ui-state.service';
import { UserService, MotoristaDto } from '../../core/services/user.service';
import { VeiculosService, Veiculo } from '../../core/services/veiculos.service';
import { TransportadorasService, Transportadora } from '../../core/services/transportadoras-catalogo.service';

function dataFimValidator(): ValidatorFn {
  return (group: AbstractControl) => {
    const inicio = group.get('dataPrevistaInicio')?.value;
    const fim    = group.get('dataPrevistaFim')?.value;
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

  // ── Serviços ──────────────────────────────────────────────────────────────
  private readonly svc                  = inject(AtribuicaoService);
  private readonly fb                   = inject(FormBuilder);
  private readonly pdfService           = inject(PdfService);
  readonly uiState                      = inject(UiStateService);
  private readonly userService          = inject(UserService);
  private readonly veiculosService      = inject(VeiculosService);
  private readonly transportadorasService = inject(TransportadorasService);
  private readonly destroy$             = new Subject<void>();

  // ── Estado UI ─────────────────────────────────────────────────────────────
  currentState = this.uiState.currentAtribuicaoState;
  editingId    = this.uiState.currentAtribuicaoId;
  isViewing    = signal(false);

  isListView()   { return this.currentState() === 'list';   }
  isCreateView() { return this.currentState() === 'create'; }
  isEditView()   { return this.currentState() === 'edit';   }

  // ── Dados ─────────────────────────────────────────────────────────────────
  pagedResult    = signal<PagedResult<Atribuicao> | null>(null);
  atribuicoes    = computed(() => this.pagedResult()?.items ?? []);
  motoristas     = signal<MotoristaDto[]>([]);
  veiculos       = signal<Veiculo[]>([]);
  transportadoras = signal<Transportadora[]>([]);
  isLoading      = signal(false);
  isSaving       = signal(false);
  errorMsg       = signal<string | null>(null);
  successMsg     = signal<string | null>(null);

  // ── Stats-Grid (computed a partir dos dados carregados) ───────────────────
  totalAtribuicoes   = computed(() => this.pagedResult()?.total ?? 0);
  countEmRota        = computed(() =>
    this.atribuicoes().filter(a => a.status === 'EmProgresso').length
  );
  frotaEmUso         = computed(() => {
    const ids = this.atribuicoes()
      .filter(a => a.status === 'EmProgresso' && a.veiculoId)
      .map(a => a.veiculoId);
    return new Set(ids).size;
  });
  entregasHoje       = computed(() => {
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
  alertasAtraso      = computed(() => {
    const agora = new Date();
    return this.atribuicoes().filter(a =>
      a.status === 'Pendente' &&
      a.dataPrevistaInicio &&
      new Date(a.dataPrevistaInicio) < agora
    ).length;
  });

  // ── Filtros & paginação ───────────────────────────────────────────────────
  filtroStatus  = '';
  filtroSearch  = '';
  currentPage   = 1;
  readonly pageSize = 15;

  totalPages = computed(() => this.pagedResult()?.totalPages ?? 0);
  pages      = computed(() =>
    Array.from({ length: this.totalPages() }, (_, i) => i + 1)
  );

  // ── Modal de confirmação de cancelamento ──────────────────────────────────
  showDeleteConfirm    = signal(false);
  atribuicaoParaDelete = signal<Atribuicao | null>(null);

  readonly prioridades  = ['Baixa', 'Media', 'Alta', 'Urgente'] as const;
  readonly statusList   = ['Pendente', 'EmProgresso', 'Concluida', 'Cancelada'] as const;

  form!: FormGroup;
  private readonly searchInput$ = new Subject<string>();

  // ── Ciclo de vida ─────────────────────────────────────────────────────────

  ngOnInit(): void {
    this.initForm();
    this.carregarMotoristas();
    this.carregarVeiculos();
    this.carregarTransportadoras();

    this.searchInput$
      .pipe(debounceTime(400), distinctUntilChanged(), takeUntil(this.destroy$))
      .subscribe(() => { this.currentPage = 1; this.carregarAtribuicoes(); });

    this.carregarAtribuicoes();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  // ── Formulário ────────────────────────────────────────────────────────────

  private initForm(): void {
    this.form = this.fb.group({
      clienteNome:       ['', [Validators.required, Validators.maxLength(200)]],
      clienteContacto:   ['', Validators.maxLength(100)],
      enderecoOrigem:    ['', Validators.maxLength(300)],
      enderecoDestino:   ['', [Validators.required, Validators.maxLength(300)]],
      dataPrevistaInicio:[''],
      dataPrevistaFim:   [''],
      prioridade:        ['Media', Validators.required],
      observacoes:       ['', Validators.maxLength(500)],
      motoristaId:       [null],
      veiculoId:         [null],
      transportadoraId:  [null],
      distanciaTotalKm:  [0, Validators.min(0)],
      tempoEstimadoHoras:[null, Validators.min(0)],
      entregas:          this.fb.array([]),
    }, { validators: dataFimValidator() });  // FIX: validação cruzada de datas

    this.adicionarEntrega();
  }

  get entregasArray(): FormArray { return this.form.get('entregas') as FormArray; }

  criarEntregaForm(entrega?: any): FormGroup {
    return this.fb.group({
      id:           [entrega?.id ?? null],
      destinatario: [entrega?.destinatario ?? '', Validators.maxLength(200)],
      endereco:     [entrega?.endereco     ?? '', Validators.maxLength(300)],
      contacto:     [entrega?.contacto     ?? '', Validators.maxLength(100)],
      observacoes:  [entrega?.observacoes  ?? '', Validators.maxLength(500)],
      ordem:        [entrega?.ordem ?? (this.entregasArray.length + 1), Validators.min(1)],
      realizada:    [entrega?.realizada ?? false],
    });
  }

  adicionarEntrega(): void {
    this.entregasArray.push(this.criarEntregaForm());
  }

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

  // ── Progressão de entregas ─────────────────────────────────────────────────
  progressoEntregas(a: Atribuicao): number {
    if (!a.totalEntregas) return 0;
    return Math.round((a.entregasRealizadas / a.totalEntregas) * 100);
  }

  // ── Carregamento de dados ──────────────────────────────────────────────────

  carregarMotoristas(): void {
    this.userService.listarMotoristas().subscribe({
      next:  d   => this.motoristas.set(d),
      error: ()  => this.motoristas.set([]),
    });
  }

  carregarVeiculos(): void {
    this.veiculosService.listar({ ativo: true }).subscribe({
      next:  r   => this.veiculos.set(r.items),
      error: ()  => this.veiculos.set([]),
    });
  }

  carregarTransportadoras(): void {
    this.transportadorasService.listar({ ativo: true }).subscribe({
      next:  d   => this.transportadoras.set(d.items),
      error: ()  => this.transportadoras.set([]),
    });
  }

  carregarAtribuicoes(): void {
    this.isLoading.set(true);
    this.svc.listar({
      status:   this.filtroStatus || undefined,
      search:   this.filtroSearch || undefined,
      page:     this.currentPage,
      pageSize: this.pageSize,
    }).subscribe({
      next:  r   => { this.pagedResult.set(r); this.isLoading.set(false); },
      error: err => { this.errorMsg.set(err.message ?? 'Erro ao carregar atribuições.'); this.isLoading.set(false); },
    });
  }

  onSearchChange(value: string): void {
    this.filtroSearch = value;
    this.searchInput$.next(value);
  }

  onStatusChange(value: string): void {
    this.filtroStatus = value;
    this.currentPage  = 1;
    this.carregarAtribuicoes();
  }

  goToPage(page: number): void {
    if (page < 1 || page > this.totalPages()) return;
    this.currentPage = page;
    this.carregarAtribuicoes();
  }

  // ── Navegação ─────────────────────────────────────────────────────────────

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

  // ── PDF ───────────────────────────────────────────────────────────────────

  imprimirPdf(atribuicao: Atribuicao, event?: Event): void {
    if (event) event.stopPropagation();
    const fields: PdfField[] = [
      { label: 'Nº Atribuição',  value: atribuicao.numeroAtribuicao },
      { label: 'Cliente',        value: atribuicao.clienteNome ?? '—' },
      { label: 'Contacto',       value: atribuicao.clienteContacto ?? '—' },
      { label: 'Origem',         value: atribuicao.enderecoOrigem   ?? '—' },
      { label: 'Destino',        value: atribuicao.enderecoDestino  ?? '—' },
      { label: 'Início previsto',value: this.formatarData(atribuicao.dataPrevistaInicio ?? '') },
      { label: 'Fim previsto',   value: this.formatarData(atribuicao.dataPrevistaFim    ?? '') },
      { label: 'Prioridade',     value: atribuicao.prioridade },
      { label: 'Status',         value: this.getStatusLabel(atribuicao.status) },
      { label: 'Motorista',      value: atribuicao.motoristaNome     ?? '—' },
      { label: 'Veículo',        value: atribuicao.veiculoMatricula  ?? '—' },
      { label: 'Transportadora', value: atribuicao.transportadoraNome ?? '—' },
      { label: 'Distância (km)', value: atribuicao.distanciaTotalKm ?? 0 },
      { label: 'Tempo estimado', value: atribuicao.tempoEstimadoHoras ? `${atribuicao.tempoEstimadoHoras}h` : '—' },
      { label: 'Total Entregas', value: atribuicao.totalEntregas },
      { label: 'Realizadas',     value: atribuicao.entregasRealizadas },
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

  // ── CRUD ──────────────────────────────────────────────────────────────────

  salvarAtribuicao(): void {
    this.form.markAllAsTouched();

    // FIX validação cruzada de datas
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
        id:           e.id   || undefined,
        destinatario: e.destinatario?.trim() || undefined,
        endereco:     e.endereco?.trim()     || undefined,
        contacto:     e.contacto?.trim()     || undefined,
        observacoes:  e.observacoes?.trim()  || undefined,
        ordem:        e.ordem || idx + 1,
        realizada:    !!e.realizada,
      }))
      .filter((e: any) => e.destinatario);

    const base: AtribuicaoCreateDto = {
      clienteNome:        v.clienteNome.trim(),
      clienteContacto:    v.clienteContacto?.trim()    || undefined,
      enderecoOrigem:     v.enderecoOrigem?.trim()      || undefined,
      enderecoDestino:    v.enderecoDestino?.trim(),
      dataPrevistaInicio: v.dataPrevistaInicio          || undefined,
      dataPrevistaFim:    v.dataPrevistaFim             || undefined,
      prioridade:         v.prioridade,
      observacoes:        v.observacoes?.trim()         || undefined,
      motoristaId:        v.motoristaId                 || undefined,
      veiculoId:          v.veiculoId                   || undefined,
      transportadoraId:   v.transportadoraId            || undefined,
      distanciaTotalKm:   +v.distanciaTotalKm           || 0,
      tempoEstimadoHoras: v.tempoEstimadoHoras          || undefined,
      entregas:           entregas.length > 0 ? entregas : undefined,
    };

    if (this.isEditView() && this.editingId()) {
      const updateDto: AtribuicaoUpdateDto = {
        ...base,
        status: v.status || undefined,
      } as AtribuicaoUpdateDto;
      this.svc.atualizar(this.editingId()!, updateDto).subscribe({
        next:  () => this._onSaveSuccess('Atribuição actualizada com sucesso.'),
        error: e  => this._onSaveError(e.message),
      });
    } else {
      this.svc.criar(base).subscribe({
        next:  () => this._onSaveSuccess('Atribuição criada com sucesso.'),
        error: e  => this._onSaveError(e.message),
      });
    }
  }

  iniciarAtribuicao(atribuicao: Atribuicao, event?: Event): void {
    if (event) event.stopPropagation();
    if (!confirm(`Iniciar atribuição ${atribuicao.numeroAtribuicao}?`)) return;
    this.svc.iniciar(atribuicao.id).subscribe({
      next:  () => { this.carregarAtribuicoes(); this.showToast('Atribuição iniciada com sucesso.'); },
      error: e  => this.errorMsg.set(e.message),
    });
  }

  concluirAtribuicao(atribuicao: Atribuicao, event?: Event): void {
    if (event) event.stopPropagation();
    if (!confirm(`Concluir atribuição ${atribuicao.numeroAtribuicao}?`)) return;
    this.svc.concluir(atribuicao.id).subscribe({
      next:  () => { this.carregarAtribuicoes(); this.showToast('Atribuição concluída com sucesso.'); },
      error: e  => this.errorMsg.set(e.message),
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
      next:  () => { this.cancelarDelete(); this.carregarAtribuicoes(); this.showToast('Atribuição cancelada.'); },
      error: e  => { this.errorMsg.set(e.message); this.cancelarDelete(); },
    });
  }

  // ── Helpers ───────────────────────────────────────────────────────────────

  private _resetForm(): void {
    while (this.entregasArray.length) this.entregasArray.removeAt(0);
    this.form.reset({ prioridade: 'Media', distanciaTotalKm: 0 });
    this.adicionarEntrega();
    this.errorMsg.set(null);
  }

  // FIX: patchValue centralizado — DRY entre goToEdit e goToDetails
  private _patchAtribuicao(a: Atribuicao): void {
    while (this.entregasArray.length) this.entregasArray.removeAt(0);
    this.form.patchValue({
      clienteNome:        a.clienteNome        ?? '',
      clienteContacto:    a.clienteContacto    ?? '',
      enderecoOrigem:     a.enderecoOrigem     ?? '',
      enderecoDestino:    a.enderecoDestino    ?? '',
      dataPrevistaInicio: a.dataPrevistaInicio?.split('T')[0] ?? '',
      dataPrevistaFim:    a.dataPrevistaFim?.split('T')[0]    ?? '',
      prioridade:         a.prioridade,
      observacoes:        a.observacoes        ?? '',
      motoristaId:        a.motoristaId        ?? null,
      veiculoId:          a.veiculoId          ?? null,
      transportadoraId:   a.transportadoraId   ?? null,
      distanciaTotalKm:   a.distanciaTotalKm   ?? 0,
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
      Alta:  'prioridade-alta',  Urgente: 'prioridade-urgente',
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
