import { Component, OnInit, OnDestroy, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators, AbstractControl } from '@angular/forms';
import { Subject, debounceTime, distinctUntilChanged, takeUntil } from 'rxjs';
import { FechoViagemService, FechoViagem, PagedResult, FechoViagemCreateDto, FechoViagemUpdateDto } from '../../core/services/fecho-viagem.service';
import { AtribuicaoService, Atribuicao } from '../../core/services/atribuicao.service';
import { UiStateService } from '../../core/services/ui-state.service';

@Component({
  selector: 'app-fecho-viagem',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './fecho-viagem.component.html',
  styleUrls: ['./fecho-viagem.component.css']
})
export class FechoViagemComponent implements OnInit, OnDestroy {
  private readonly svc = inject(FechoViagemService);
  private readonly atribuicaoSvc = inject(AtribuicaoService);
  private readonly fb = inject(FormBuilder);
  private readonly uiState = inject(UiStateService);
  private readonly destroy$ = new Subject<void>();

  // Estado da UI
  currentState = this.uiState.currentFechoViagemState;
  editingId = this.uiState.currentFechoViagemId;

  // Dados
  pagedResult = signal<PagedResult<FechoViagem> | null>(null);
  fechos = computed(() => this.pagedResult()?.items ?? []);
  isLoading = signal(false);
  isSaving = signal(false);
  errorMsg = signal<string | null>(null);
  successMsg = signal<string | null>(null);

  // Filtros
  filtroStatus = '';
  filtroSearch = '';
  currentPage = 1;
  readonly pageSize = 15;

  // Formulário
  form!: FormGroup;

  // Lista de atribuições para seleção (apenas concluídas ou pendentes)
  atribuicoes = signal<Atribuicao[]>([]);
  isLoadingAtribuicoes = signal(false);

  // Modal apenas para ELIMINAR
  showDeleteConfirm = signal(false);
  fechoParaDelete = signal<FechoViagem | null>(null);

  private readonly searchInput$ = new Subject<string>();

  readonly statusList = ['Pendente', 'Processado', 'Cancelado'];

  totalFechos = computed(() => this.pagedResult()?.total ?? 0);
  totalPages = computed(() => this.pagedResult()?.totalPages ?? 0);
  pages = computed(() => Array.from({ length: this.totalPages() }, (_, i) => i + 1));

  ngOnInit(): void {
    this.initForm();

    this.searchInput$
      .pipe(debounceTime(400), distinctUntilChanged(), takeUntil(this.destroy$))
      .subscribe(() => {
        this.currentPage = 1;
        this.carregarFechos();
      });

    this.carregarFechos();
    this.carregarAtribuicoes();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private initForm(): void {
    this.form = this.fb.group({
      atribuicaoId: [null, [Validators.required, Validators.min(1)]],
      dataInicioReal: [''],
      dataFimReal: [''],
      combustivelLitros: [null, [Validators.min(0)]],
      combustivelCusto: [null, [Validators.min(0)]],
      portagensCusto: [null, [Validators.min(0)]],
      outrosCustos: [null, [Validators.min(0)]],
      custosExtrasDescricao: ['', Validators.maxLength(500)],
      quilometrosInicio: [null, [Validators.min(0)]],
      quilometrosFim: [null, [Validators.min(0)]],
      entregasNaoRealizadasIds: [[]],
      entregasPendentesObs: ['', Validators.maxLength(500)],
      temIncidentes: [false],
      incidentesDescricao: ['', Validators.maxLength(1000)],
      observacoes: ['', Validators.maxLength(500)]
    });
  }

  ctrl(name: string): AbstractControl { return this.form.get(name)!; }

  hasError(name: string, error?: string): boolean {
    const c = this.ctrl(name);
    if (!c.invalid || !c.touched) return false;
    return error ? c.hasError(error) : true;
  }

  carregarFechos(): void {
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
        this.errorMsg.set(err.message ?? 'Erro ao carregar fechos de viagem');
        this.isLoading.set(false);
      },
    });
  }

  carregarAtribuicoes(): void {
    this.isLoadingAtribuicoes.set(true);
    this.atribuicaoSvc.listar({
      status: 'Concluida',
      page: 1,
      pageSize: 100
    }).subscribe({
      next: (result) => {
        this.atribuicoes.set(result.items);
        this.isLoadingAtribuicoes.set(false);
      },
      error: () => {
        this.isLoadingAtribuicoes.set(false);
      }
    });
  }

  onSearchChange(value: string): void {
    this.filtroSearch = value;
    this.searchInput$.next(value);
  }

  onStatusChange(value: string): void {
    this.filtroStatus = value;
    this.currentPage = 1;
    this.carregarFechos();
  }

  goToPage(page: number): void {
    const total = this.totalPages();
    if (page < 1 || page > total) return;
    this.currentPage = page;
    this.carregarFechos();
  }

  goToCreate(): void {
    this.resetForm();
    this.uiState.goToFechoViagemCreate();
  }

  goToEdit(fecho: FechoViagem, event?: Event): void {
    if (event) event.stopPropagation();
    this.carregarDadosParaEdicao(fecho);
    this.uiState.goToFechoViagemEdit(fecho.id);
  }

  goToList(): void {
    this.uiState.goToFechoViagemList();
    this.resetForm();
    this.carregarFechos();
  }

  cancel(): void {
    this.goToList();
  }

  private resetForm(): void {
    this.form.reset({
      temIncidentes: false
    });
    this.errorMsg.set(null);
  }

  private carregarDadosParaEdicao(fecho: FechoViagem): void {
    this.form.patchValue({
      atribuicaoId: fecho.atribuicaoId,
      dataInicioReal: fecho.dataInicioReal?.split('T')[0] ?? '',
      dataFimReal: fecho.dataFimReal?.split('T')[0] ?? '',
      combustivelLitros: fecho.combustivelLitros,
      combustivelCusto: fecho.combustivelCusto,
      portagensCusto: fecho.portagensCusto,
      outrosCustos: fecho.outrosCustos,
      custosExtrasDescricao: fecho.custosExtrasDescricao ?? '',
      quilometrosInicio: fecho.quilometrosInicio,
      quilometrosFim: fecho.quilometrosFim,
      entregasPendentesObs: fecho.entregasPendentesObs ?? '',
      temIncidentes: fecho.temIncidentes,
      incidentesDescricao: fecho.incidentesDescricao ?? '',
      observacoes: fecho.observacoes ?? ''
    });
    this.errorMsg.set(null);
  }

  salvarFecho(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid) {
      this.errorMsg.set('Corrija os erros no formulário antes de continuar.');
      return;
    }

    this.isSaving.set(true);
    this.errorMsg.set(null);
    const v = this.form.getRawValue();

    // Validar datas
    if (v.dataInicioReal && v.dataFimReal && new Date(v.dataFimReal) < new Date(v.dataInicioReal)) {
      this.errorMsg.set('A data/hora de fim não pode ser anterior à data/hora de início.');
      this.isSaving.set(false);
      return;
    }

    const dto: FechoViagemCreateDto = {
      atribuicaoId: v.atribuicaoId,
      dataInicioReal: v.dataInicioReal || undefined,
      dataFimReal: v.dataFimReal || undefined,
      combustivelLitros: v.combustivelLitros || undefined,
      combustivelCusto: v.combustivelCusto || undefined,
      portagensCusto: v.portagensCusto || undefined,
      outrosCustos: v.outrosCustos || undefined,
      custosExtrasDescricao: v.custosExtrasDescricao?.trim() || undefined,
      quilometrosInicio: v.quilometrosInicio || undefined,
      quilometrosFim: v.quilometrosFim || undefined,
      entregasNaoRealizadasIds: v.entregasNaoRealizadasIds || [],
      entregasPendentesObs: v.entregasPendentesObs?.trim() || undefined,
      temIncidentes: v.temIncidentes || false,
      incidentesDescricao: v.incidentesDescricao?.trim() || undefined,
      observacoes: v.observacoes?.trim() || undefined
    };

    if (this.uiState.isFechoViagemEdit() && this.editingId()) {
      this.svc.atualizar(this.editingId()!, dto).subscribe({
        next: () => this.onSaveSuccess('Fecho de viagem actualizado com sucesso.'),
        error: (err) => this.onSaveError(err.message)
      });
    } else {
      this.svc.criar(dto).subscribe({
        next: () => this.onSaveSuccess('Fecho de viagem criado com sucesso.'),
        error: (err) => this.onSaveError(err.message)
      });
    }
  }

  processarFecho(fecho: FechoViagem, event?: Event): void {
    if (event) event.stopPropagation();
    if (!confirm(`Processar fecho ${fecho.numeroFecho}? Esta ação irá preparar os dados para faturação.`)) return;

    this.svc.processar(fecho.id).subscribe({
      next: () => {
        this.carregarFechos();
        this.showToast('Fecho processado com sucesso! Dados enviados para faturação.');
      },
      error: (err) => this.errorMsg.set(err.message)
    });
  }

  confirmarDelete(fecho: FechoViagem, event?: Event): void {
    if (event) event.stopPropagation();
    this.fechoParaDelete.set(fecho);
    this.showDeleteConfirm.set(true);
  }

  cancelarDelete(): void {
    this.showDeleteConfirm.set(false);
    this.fechoParaDelete.set(null);
  }

  executarDelete(): void {
    const f = this.fechoParaDelete();
    if (!f) return;
    this.svc.deletar(f.id).subscribe({
      next: () => {
        this.cancelarDelete();
        this.carregarFechos();
        this.showToast('Fecho de viagem cancelado com sucesso.');
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
      'Processado': 'status-processado',
      'Cancelado': 'status-cancelado'
    };
    return classes[status] || 'status-pendente';
  }

  formatarData(data: string): string {
    if (!data) return '—';
    return new Date(data).toLocaleDateString('pt-PT', {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric'
    });
  }

  formatarMoeda(valor: number): string {
    if (!valor && valor !== 0) return '—';
    return new Intl.NumberFormat('pt-PT', { style: 'currency', currency: 'EUR' }).format(valor);
  }

  formatarKm(km: number): string {
    if (!km && km !== 0) return '—';
    return `${km.toLocaleString('pt-PT')} km`;
  }

  formatarTempo(tempo?: string): string {
    if (!tempo) return '—';
    // Formato "HH:MM:SS"
    const parts = tempo.split(':');
    if (parts.length >= 2) {
      return `${parts[0]}h ${parts[1]}min`;
    }
    return tempo;
  }

  getDiferencaClass(diferenca?: string): string {
    if (!diferenca) return '';
    const hours = parseInt(diferenca.split(':')[0]);
    if (hours > 0) return 'diferenca-positiva';
    if (hours < 0) return 'diferenca-negativa';
    return 'diferenca-zero';
  }
}