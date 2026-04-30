import { Component, OnInit, OnDestroy, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators, AbstractControl } from '@angular/forms';
import { Subject, debounceTime, distinctUntilChanged, takeUntil } from 'rxjs';
import { GestaoViagemService, GestaoViagem, PagedResult, GestaoViagemCreateDto, GestaoViagemUpdateDto } from '../../core/services/gestao-viagens.service';
import { UiStateService } from '../../core/services/ui-state.service';

@Component({
  selector: 'app-gestao-viagens',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './gestao-viagens.component.html',
  styleUrls: ['./gestao-viagens.component.css']
})
export class GestaoViagensComponent implements OnInit, OnDestroy {
  private readonly svc = inject(GestaoViagemService);
  private readonly fb = inject(FormBuilder);
  private readonly uiState = inject(UiStateService);
  private readonly destroy$ = new Subject<void>();

  // Estado da UI
  currentState = this.uiState.currentGestaoViagemState;
  editingId = this.uiState.currentGestaoViagemId;

  // Dados
  pagedResult = signal<PagedResult<GestaoViagem> | null>(null);
  viagens = computed(() => this.pagedResult()?.items ?? []);
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

  // Modal apenas para ELIMINAR
  showDeleteConfirm = signal(false);
  viagemParaDelete = signal<GestaoViagem | null>(null);

  private readonly searchInput$ = new Subject<string>();

  readonly statusList = ['Planeada', 'EmCurso', 'Concluida', 'Cancelada'];
  readonly prioridades = ['Baixa', 'Media', 'Alta', 'Urgente'];

  totalViagens = computed(() => this.pagedResult()?.total ?? 0);
  totalPages = computed(() => this.pagedResult()?.totalPages ?? 0);
  pages = computed(() => Array.from({ length: this.totalPages() }, (_, i) => i + 1));

  ngOnInit(): void {
    this.initForm();

    this.searchInput$
      .pipe(debounceTime(400), distinctUntilChanged(), takeUntil(this.destroy$))
      .subscribe(() => {
        this.currentPage = 1;
        this.carregarViagens();
      });

    this.carregarViagens();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private initForm(): void {
    this.form = this.fb.group({
      prioridade: ['Media', Validators.required],
      dataInicioPlaneada: [''],
      dataFimPlaneada: [''],
      rotaId: [null],
      veiculoId: [null],
      motoristaId: [null],
      transportadoraId: [null],
      cargaDescricao: ['', Validators.maxLength(500)],
      cargaPeso: [0, [Validators.required, Validators.min(0)]],
      cargaVolume: [0, [Validators.required, Validators.min(0)]],
      cargaObservacoes: ['', Validators.maxLength(500)],
      distanciaTotalKm: [0, [Validators.required, Validators.min(0)]],
      tempoEstimadoHoras: [null, [Validators.min(0)]],
      observacoes: ['', Validators.maxLength(1000)]
    });
  }

  ctrl(name: string): AbstractControl { return this.form.get(name)!; }

  hasError(name: string, error?: string): boolean {
    const c = this.ctrl(name);
    if (!c.invalid || !c.touched) return false;
    return error ? c.hasError(error) : true;
  }

  carregarViagens(): void {
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
        this.errorMsg.set(err.message ?? 'Erro ao carregar viagens');
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
    this.carregarViagens();
  }

  goToPage(page: number): void {
    const total = this.totalPages();
    if (page < 1 || page > total) return;
    this.currentPage = page;
    this.carregarViagens();
  }

  goToCreate(): void {
    this.resetForm();
    this.uiState.goToGestaoViagemCreate();
  }

  goToEdit(viagem: GestaoViagem, event?: Event): void {
    if (event) event.stopPropagation();
    this.carregarDadosParaEdicao(viagem);
    this.uiState.goToGestaoViagemEdit(viagem.id);
  }

  goToList(): void {
    this.uiState.goToGestaoViagemList();
    this.resetForm();
    this.carregarViagens();
  }

  cancel(): void {
    this.goToList();
  }

  private resetForm(): void {
    this.form.reset({
      prioridade: 'Media',
      cargaPeso: 0,
      cargaVolume: 0,
      distanciaTotalKm: 0
    });
    this.errorMsg.set(null);
  }

  private carregarDadosParaEdicao(viagem: GestaoViagem): void {
    this.form.patchValue({
      prioridade: viagem.prioridade,
      dataInicioPlaneada: viagem.dataInicioPlaneada?.split('T')[0] ?? '',
      dataFimPlaneada: viagem.dataFimPlaneada?.split('T')[0] ?? '',
      rotaId: viagem.rotaId,
      veiculoId: viagem.veiculoId,
      motoristaId: viagem.motoristaId,
      transportadoraId: viagem.transportadoraId,
      cargaDescricao: viagem.cargaDescricao ?? '',
      cargaPeso: viagem.cargaPeso,
      cargaVolume: viagem.cargaVolume,
      cargaObservacoes: viagem.cargaObservacoes ?? '',
      distanciaTotalKm: viagem.distanciaTotalKm,
      tempoEstimadoHoras: viagem.tempoEstimadoHoras,
      observacoes: viagem.observacoes ?? ''
    });
    this.errorMsg.set(null);
  }

  salvarViagem(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid) {
      this.errorMsg.set('Corrija os erros no formulário antes de continuar.');
      return;
    }

    this.isSaving.set(true);
    this.errorMsg.set(null);
    const v = this.form.getRawValue();

    // Validar datas
    if (v.dataInicioPlaneada && v.dataFimPlaneada && new Date(v.dataFimPlaneada) < new Date(v.dataInicioPlaneada)) {
      this.errorMsg.set('A data/hora de fim não pode ser anterior à data/hora de início.');
      this.isSaving.set(false);
      return;
    }

    const dto: GestaoViagemCreateDto = {
      prioridade: v.prioridade,
      dataInicioPlaneada: v.dataInicioPlaneada || undefined,
      dataFimPlaneada: v.dataFimPlaneada || undefined,
      rotaId: v.rotaId || undefined,
      veiculoId: v.veiculoId || undefined,
      motoristaId: v.motoristaId || undefined,
      transportadoraId: v.transportadoraId || undefined,
      cargaDescricao: v.cargaDescricao?.trim() || undefined,
      cargaPeso: v.cargaPeso || 0,
      cargaVolume: v.cargaVolume || 0,
      cargaObservacoes: v.cargaObservacoes?.trim() || undefined,
      distanciaTotalKm: v.distanciaTotalKm || 0,
      tempoEstimadoHoras: v.tempoEstimadoHoras || undefined,
      observacoes: v.observacoes?.trim() || undefined
    };

    if (this.uiState.isGestaoViagemEdit() && this.editingId()) {
      this.svc.atualizar(this.editingId()!, dto).subscribe({
        next: () => this.onSaveSuccess('Viagem actualizada com sucesso.'),
        error: (err) => this.onSaveError(err.message)
      });
    } else {
      this.svc.criar(dto).subscribe({
        next: () => this.onSaveSuccess('Viagem criada com sucesso.'),
        error: (err) => this.onSaveError(err.message)
      });
    }
  }

  iniciarViagem(viagem: GestaoViagem, event?: Event): void {
    if (event) event.stopPropagation();
    if (!confirm(`Iniciar viagem ${viagem.numeroViagem}?`)) return;

    this.svc.iniciar(viagem.id).subscribe({
      next: () => {
        this.carregarViagens();
        this.showToast('Viagem iniciada com sucesso.');
      },
      error: (err) => this.errorMsg.set(err.message)
    });
  }

  concluirViagem(viagem: GestaoViagem, event?: Event): void {
    if (event) event.stopPropagation();
    if (!confirm(`Concluir viagem ${viagem.numeroViagem}?`)) return;

    this.svc.concluir(viagem.id).subscribe({
      next: () => {
        this.carregarViagens();
        this.showToast('Viagem concluída com sucesso.');
      },
      error: (err) => this.errorMsg.set(err.message)
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
    const v = this.viagemParaDelete();
    if (!v) return;
    this.svc.deletar(v.id).subscribe({
      next: () => {
        this.cancelarDelete();
        this.carregarViagens();
        this.showToast('Viagem cancelada com sucesso.');
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
      'Planeada': 'status-planeada',
      'EmCurso': 'status-emcurso',
      'Concluida': 'status-concluida',
      'Cancelada': 'status-cancelada'
    };
    return classes[status] || 'status-planeada';
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
      year: 'numeric'
    });
  }

  formatarDataHora(data: string): string {
    if (!data) return '—';
    return new Date(data).toLocaleDateString('pt-PT', {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit'
    });
  }

  formatarTempo(horas?: number): string {
    if (!horas) return '—';
    const h = Math.floor(horas);
    const m = Math.round((horas - h) * 60);
    return `${h}h ${m}min`;
  }

  formatarPeso(peso: number): string {
    return `${peso.toLocaleString('pt-PT')} kg`;
  }

  formatarVolume(volume: number): string {
    return `${volume.toLocaleString('pt-PT')} m³`;
  }

  formatarKm(km: number): string {
    return `${km.toLocaleString('pt-PT')} km`;
  }

  getDesempenhoClass(viagem: GestaoViagem): string {
    if (viagem.status !== 'Concluida') return '';
    if (viagem.atrasoHoras && viagem.atrasoHoras > 0) return 'desempenho-ruim';
    if (viagem.tempoRealHoras && viagem.tempoEstimadoHoras && viagem.tempoRealHoras < viagem.tempoEstimadoHoras * 0.9) return 'desempenho-bom';
    return 'desempenho-normal';
  }
}