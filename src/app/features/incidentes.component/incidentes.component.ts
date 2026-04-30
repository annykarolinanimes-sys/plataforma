import { Component, OnInit, OnDestroy, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators, AbstractControl } from '@angular/forms';
import { Subject, debounceTime, distinctUntilChanged, takeUntil } from 'rxjs';
import { IncidentesService, Incidente, PagedResult, IncidenteCreateDto, IncidenteUpdateDto, ResolverIncidenteDto } from '../../core/services/incidentes.service';
import { UiStateService } from '../../core/services/ui-state.service';

@Component({
  selector: 'app-incidentes',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './incidentes.component.html',
  styleUrls: ['./incidentes.component.css']
})
export class IncidentesComponent implements OnInit, OnDestroy {
  private readonly svc = inject(IncidentesService);
  private readonly fb = inject(FormBuilder);
  private readonly uiState = inject(UiStateService);
  private readonly destroy$ = new Subject<void>();

  // Estado da UI
  currentState = this.uiState.currentIncidenteState;
  editingId = this.uiState.currentIncidenteId;

  // Dados
  pagedResult = signal<PagedResult<Incidente> | null>(null);
  incidentes = computed(() => this.pagedResult()?.items ?? []);
  isLoading = signal(false);
  isSaving = signal(false);
  errorMsg = signal<string | null>(null);
  successMsg = signal<string | null>(null);

  // Filtros
  filtroStatus = '';
  filtroGravidade = '';
  filtroSearch = '';
  currentPage = 1;
  readonly pageSize = 15;

  // Formulário
  form!: FormGroup;

  // Modal de resolução
  showResolverModal = signal(false);
  incidenteParaResolver = signal<Incidente | null>(null);
  resolverForm!: FormGroup;

  // Modal apenas para ELIMINAR
  showDeleteConfirm = signal(false);
  incidenteParaDelete = signal<Incidente | null>(null);

  private readonly searchInput$ = new Subject<string>();

  readonly tipos = ['Atraso', 'Avaria', 'Danificado', 'EntregaFalha', 'Outro'];
  readonly gravidades = ['Baixa', 'Media', 'Alta', 'Critica'];
  readonly statusList = ['Aberto', 'EmAnalise', 'Resolvido', 'Fechado'];

  totalIncidentes = computed(() => this.pagedResult()?.total ?? 0);
  totalPages = computed(() => this.pagedResult()?.totalPages ?? 0);
  pages = computed(() => Array.from({ length: this.totalPages() }, (_, i) => i + 1));

  ngOnInit(): void {
    this.initForm();
    this.initResolverForm();

    this.searchInput$
      .pipe(debounceTime(400), distinctUntilChanged(), takeUntil(this.destroy$))
      .subscribe(() => {
        this.currentPage = 1;
        this.carregarIncidentes();
      });

    this.carregarIncidentes();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private initForm(): void {
    this.form = this.fb.group({
      tipo: ['', Validators.required],
      gravidade: ['Media', Validators.required],
      titulo: ['', [Validators.required, Validators.maxLength(200)]],
      descricao: ['', Validators.maxLength(2000)],
      dataOcorrencia: [''],
      viagemId: [null],
      veiculoId: [null],
      clienteId: [null],
      atribuicaoId: [null],
      causa: ['', Validators.maxLength(500)],
      acaoCorretiva: ['', Validators.maxLength(1000)],
      responsavelResolucao: ['', Validators.maxLength(200)],
      custoAssociado: [null, [Validators.min(0)]],
      observacoes: ['', Validators.maxLength(500)]
    });
  }

  private initResolverForm(): void {
    this.resolverForm = this.fb.group({
      acaoCorretiva: ['', [Validators.required, Validators.maxLength(1000)]],
      responsavelResolucao: ['', Validators.maxLength(200)],
      custoAssociado: [null, [Validators.min(0)]],
      observacoes: ['', Validators.maxLength(500)]
    });
  }

  ctrl(name: string): AbstractControl { return this.form.get(name)!; }
  resolverCtrl(name: string): AbstractControl { return this.resolverForm.get(name)!; }

  hasError(name: string, error?: string): boolean {
    const c = this.ctrl(name);
    if (!c.invalid || !c.touched) return false;
    return error ? c.hasError(error) : true;
  }

  carregarIncidentes(): void {
    this.isLoading.set(true);
    this.svc.listar({
      status: this.filtroStatus || undefined,
      gravidade: this.filtroGravidade || undefined,
      search: this.filtroSearch || undefined,
      page: this.currentPage,
      pageSize: this.pageSize,
    }).subscribe({
      next: (result) => {
        this.pagedResult.set(result);
        this.isLoading.set(false);
      },
      error: (err) => {
        this.errorMsg.set(err.message ?? 'Erro ao carregar incidentes');
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
    this.carregarIncidentes();
  }

  onGravidadeChange(value: string): void {
    this.filtroGravidade = value;
    this.currentPage = 1;
    this.carregarIncidentes();
  }

  goToPage(page: number): void {
    const total = this.totalPages();
    if (page < 1 || page > total) return;
    this.currentPage = page;
    this.carregarIncidentes();
  }

  goToCreate(): void {
    this.resetForm();
    this.uiState.goToIncidenteCreate();
  }

  goToEdit(incidente: Incidente, event?: Event): void {
    if (event) event.stopPropagation();
    this.carregarDadosParaEdicao(incidente);
    this.uiState.goToIncidenteEdit(incidente.id);
  }

  goToList(): void {
    this.uiState.goToIncidenteList();
    this.resetForm();
    this.carregarIncidentes();
  }

  cancel(): void {
    this.goToList();
  }

  private resetForm(): void {
    this.form.reset({
      gravidade: 'Media',
      dataOcorrencia: new Date().toISOString().split('T')[0]
    });
    this.errorMsg.set(null);
  }

  private carregarDadosParaEdicao(incidente: Incidente): void {
    this.form.patchValue({
      tipo: incidente.tipo,
      gravidade: incidente.gravidade,
      titulo: incidente.titulo,
      descricao: incidente.descricao ?? '',
      dataOcorrencia: incidente.dataOcorrencia?.split('T')[0] ?? '',
      viagemId: incidente.viagemId,
      veiculoId: incidente.veiculoId,
      clienteId: incidente.clienteId,
      atribuicaoId: incidente.atribuicaoId,
      causa: incidente.causa ?? '',
      acaoCorretiva: incidente.acaoCorretiva ?? '',
      responsavelResolucao: incidente.responsavelResolucao ?? '',
      custoAssociado: incidente.custoAssociado,
      observacoes: incidente.observacoes ?? ''
    });
    this.errorMsg.set(null);
  }

  salvarIncidente(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid) {
      this.errorMsg.set('Corrija os erros no formulário antes de continuar.');
      return;
    }

    this.isSaving.set(true);
    this.errorMsg.set(null);
    const v = this.form.getRawValue();

    const dto: IncidenteCreateDto = {
      tipo: v.tipo,
      gravidade: v.gravidade,
      titulo: v.titulo.trim(),
      descricao: v.descricao?.trim() || undefined,
      dataOcorrencia: v.dataOcorrencia || undefined,
      viagemId: v.viagemId || undefined,
      veiculoId: v.veiculoId || undefined,
      clienteId: v.clienteId || undefined,
      atribuicaoId: v.atribuicaoId || undefined,
      causa: v.causa?.trim() || undefined,
      acaoCorretiva: v.acaoCorretiva?.trim() || undefined,
      responsavelResolucao: v.responsavelResolucao?.trim() || undefined,
      custoAssociado: v.custoAssociado || undefined,
      observacoes: v.observacoes?.trim() || undefined
    };

    if (this.uiState.isIncidenteEdit() && this.editingId()) {
      this.svc.atualizar(this.editingId()!, dto).subscribe({
        next: () => this.onSaveSuccess('Incidente actualizado com sucesso.'),
        error: (err) => this.onSaveError(err.message)
      });
    } else {
      this.svc.criar(dto).subscribe({
        next: () => this.onSaveSuccess('Incidente registado com sucesso.'),
        error: (err) => this.onSaveError(err.message)
      });
    }
  }

  abrirResolverModal(incidente: Incidente, event?: Event): void {
    if (event) event.stopPropagation();
    this.incidenteParaResolver.set(incidente);
    this.resolverForm.reset({
      acaoCorretiva: incidente.acaoCorretiva ?? '',
      responsavelResolucao: incidente.responsavelResolucao ?? '',
      custoAssociado: incidente.custoAssociado,
      observacoes: incidente.observacoes ?? ''
    });
    this.showResolverModal.set(true);
  }

  fecharResolverModal(): void {
    this.showResolverModal.set(false);
    this.incidenteParaResolver.set(null);
    this.resolverForm.reset();
  }

  resolverIncidente(): void {
    this.resolverForm.markAllAsTouched();
    if (this.resolverForm.invalid) {
      this.errorMsg.set('A ação corretiva é obrigatória.');
      return;
    }

    const incidente = this.incidenteParaResolver();
    if (!incidente) return;

    this.isSaving.set(true);
    const dto: ResolverIncidenteDto = {
      acaoCorretiva: this.resolverCtrl('acaoCorretiva').value.trim(),
      responsavelResolucao: this.resolverCtrl('responsavelResolucao').value?.trim() || undefined,
      custoAssociado: this.resolverCtrl('custoAssociado').value || undefined,
      observacoes: this.resolverCtrl('observacoes').value?.trim() || undefined
    };

    this.svc.resolver(incidente.id, dto).subscribe({
      next: () => {
        this.isSaving.set(false);
        this.fecharResolverModal();
        this.carregarIncidentes();
        this.showToast('Incidente resolvido com sucesso.');
      },
      error: (err) => {
        this.errorMsg.set(err.message);
        this.isSaving.set(false);
      }
    });
  }

  fecharIncidente(incidente: Incidente, event?: Event): void {
    if (event) event.stopPropagation();
    if (!confirm(`Fechar incidente ${incidente.numeroIncidente}?`)) return;

    this.svc.fechar(incidente.id).subscribe({
      next: () => {
        this.carregarIncidentes();
        this.showToast('Incidente fechado com sucesso.');
      },
      error: (err) => this.errorMsg.set(err.message)
    });
  }

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
    const i = this.incidenteParaDelete();
    if (!i) return;
    this.svc.deletar(i.id).subscribe({
      next: () => {
        this.cancelarDelete();
        this.carregarIncidentes();
        this.showToast('Incidente removido com sucesso.');
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

  getTipoClass(tipo: string): string {
    const classes: Record<string, string> = {
      'Atraso': 'tipo-atraso',
      'Avaria': 'tipo-avaria',
      'Danificado': 'tipo-danificado',
      'EntregaFalha': 'tipo-falha',
      'Outro': 'tipo-outro'
    };
    return classes[tipo] || 'tipo-outro';
  }

  getStatusClass(status: string): string {
    const classes: Record<string, string> = {
      'Aberto': 'status-aberto',
      'EmAnalise': 'status-analise',
      'Resolvido': 'status-resolvido',
      'Fechado': 'status-fechado'
    };
    return classes[status] || 'status-aberto';
  }

  getGravidadeClass(gravidade: string): string {
    const classes: Record<string, string> = {
      'Baixa': 'gravidade-baixa',
      'Media': 'gravidade-media',
      'Alta': 'gravidade-alta',
      'Critica': 'gravidade-critica'
    };
    return classes[gravidade] || 'gravidade-media';
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

  formatarMoeda(valor?: number): string {
    if (!valor && valor !== 0) return '—';
    return new Intl.NumberFormat('pt-PT', { style: 'currency', currency: 'EUR' }).format(valor);
  }
}