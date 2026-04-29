import { Component, OnInit, OnDestroy, inject, signal, computed, ViewChild, ElementRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Subject, debounceTime, distinctUntilChanged, takeUntil } from 'rxjs';
import { DocumentosService, DocumentoGeral, PagedResult, EstatisticasDocumentos } from '../../core/services/documentos.service';
import { UiStateService } from '../../core/services/ui-state.service';

@Component({
  selector: 'app-documentos',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './documentos.component.html',
  styleUrls: ['./documentos.component.css']
})
export class DocumentosComponent implements OnInit, OnDestroy {
  private readonly svc = inject(DocumentosService);
  private readonly fb = inject(FormBuilder);
  private readonly uiState = inject(UiStateService);
  private readonly destroy$ = new Subject<void>();

  @ViewChild('fileInput') fileInput!: ElementRef<HTMLInputElement>;

  // Estado da UI
  currentState = this.uiState.currentDocumentoState;
  editingId = this.uiState.currentDocumentoId;

  // Dados
  pagedResult = signal<PagedResult<DocumentoGeral> | null>(null);
  documentos = computed(() => this.pagedResult()?.items ?? []);
  estatisticas = signal<EstatisticasDocumentos | null>(null);
  isLoading = signal(false);
  isUploading = signal(false);
  isSaving = signal(false);
  errorMsg = signal<string | null>(null);
  successMsg = signal<string | null>(null);
  showUploadModal = signal(false);

  // Filtros
  filtroTipo = '';
  filtroCategoria = '';
  filtroSearch = '';
  filtroFavorito = false;
  filtroEntidadeId: number | null = null;
  filtroEntidadeRelacionada = '';
  currentPage = 1;
  readonly pageSize = 15;

  // Formulário de upload
  uploadForm!: FormGroup;
  selectedFile = signal<File | null>(null);

  // Modal apenas para ELIMINAR
  showDeleteConfirm = signal(false);
  documentoParaDelete = signal<DocumentoGeral | null>(null);

  private readonly searchInput$ = new Subject<string>();

  readonly tipos = ['Fatura', 'POD', 'Relatorio', 'Outro'];
  readonly categorias = ['Financeiro', 'Operacional', 'Comercial', 'Legal', 'Outro'];
  readonly entidadesRelacionadas = ['Cliente', 'Fornecedor', 'Fatura', 'Viagem'];

  // Listas para selects
  clientes = signal<{ id: number; nome: string }[]>([]);
  fornecedores = signal<{ id: number; nome: string }[]>([]);
  faturas = signal<{ id: number; numeroFatura: string }[]>([]);

  totalDocumentos = computed(() => this.pagedResult()?.total ?? 0);
  totalPages = computed(() => this.pagedResult()?.totalPages ?? 0);
  pages = computed(() => Array.from({ length: this.totalPages() }, (_, i) => i + 1));

  ngOnInit(): void {
    this.initForm();
    this.carregarDadosAuxiliares();
    this.carregarEstatisticas();

    this.searchInput$
      .pipe(debounceTime(400), distinctUntilChanged(), takeUntil(this.destroy$))
      .subscribe(() => {
        this.currentPage = 1;
        this.carregarDocumentos();
      });

    this.carregarDocumentos();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private initForm(): void {
    this.uploadForm = this.fb.group({
      tipo: ['', Validators.required],
      nome: ['', [Validators.required, Validators.maxLength(200)]],
      descricao: ['', Validators.maxLength(500)],
      dataDocumento: [''],
      entidadeRelacionada: [''],
      entidadeId: [null],
      tags: ['', Validators.maxLength(500)],
      categoria: [''],
      observacoes: ['', Validators.maxLength(500)]
    });
  }

  carregarDadosAuxiliares(): void {
    this.svc.obterClientes().subscribe({
      next: (data) => this.clientes.set(data),
      error: () => this.clientes.set([])
    });
    this.svc.obterFornecedores().subscribe({
      next: (data) => this.fornecedores.set(data),
      error: () => this.fornecedores.set([])
    });
    this.svc.obterFaturas().subscribe({
      next: (data) => this.faturas.set(data),
      error: () => this.faturas.set([])
    });
  }

  carregarEstatisticas(): void {
    this.svc.getEstatisticas().subscribe({
      next: (data) => this.estatisticas.set(data),
      error: () => {}
    });
  }

  carregarDocumentos(): void {
    this.isLoading.set(true);
    this.svc.listar({
      tipo: this.filtroTipo || undefined,
      categoria: this.filtroCategoria || undefined,
      search: this.filtroSearch || undefined,
      entidadeId: this.filtroEntidadeId || undefined,
      entidadeRelacionada: this.filtroEntidadeRelacionada || undefined,
      favorito: this.filtroFavorito || undefined,
      page: this.currentPage,
      pageSize: this.pageSize,
    }).subscribe({
      next: (result) => {
        this.pagedResult.set(result);
        this.isLoading.set(false);
      },
      error: (err) => {
        this.errorMsg.set(err.message ?? 'Erro ao carregar documentos');
        this.isLoading.set(false);
      },
    });
  }

  onSearchChange(value: string): void {
    this.filtroSearch = value;
    this.searchInput$.next(value);
  }

  onTipoChange(value: string): void {
    this.filtroTipo = value;
    this.currentPage = 1;
    this.carregarDocumentos();
  }

  onCategoriaChange(value: string): void {
    this.filtroCategoria = value;
    this.currentPage = 1;
    this.carregarDocumentos();
  }

  onEntidadeRelacionadaChange(value: string): void {
    this.filtroEntidadeRelacionada = value;
    this.filtroEntidadeId = null;
    this.currentPage = 1;
    this.carregarDocumentos();
  }

  onEntidadeChange(event: Event): void {
    const target = event.target as HTMLSelectElement;
    this.filtroEntidadeId = target.value ? parseInt(target.value) : null;
    this.currentPage = 1;
    this.carregarDocumentos();
  }

  toggleFavoritoFilter(): void {
    this.filtroFavorito = !this.filtroFavorito;
    this.currentPage = 1;
    this.carregarDocumentos();
  }

  limparFiltros(): void {
    this.filtroTipo = '';
    this.filtroCategoria = '';
    this.filtroSearch = '';
    this.filtroFavorito = false;
    this.filtroEntidadeId = null;
    this.filtroEntidadeRelacionada = '';
    this.currentPage = 1;
    this.carregarDocumentos();
  }

  goToPage(page: number): void {
    const total = this.totalPages();
    if (page < 1 || page > total) return;
    this.currentPage = page;
    this.carregarDocumentos();
  }

  abrirUploadModal(): void {
    this.uploadForm.reset({});
    this.selectedFile.set(null);
    this.showUploadModal.set(true);
  }

  fecharUploadModal(): void {
    this.showUploadModal.set(false);
    if (this.fileInput) this.fileInput.nativeElement.value = '';
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (input.files && input.files.length > 0) {
      this.selectedFile.set(input.files[0]);
    }
  }

  onEntidadeSelecionadaChange(): void {
    const entidadeRelacionada = this.uploadForm.get('entidadeRelacionada')?.value;
    this.uploadForm.patchValue({ entidadeId: null });
  }

  fazerUpload(): void {
    this.uploadForm.markAllAsTouched();
    if (this.uploadForm.invalid) {
      this.errorMsg.set('Preencha todos os campos obrigatórios.');
      return;
    }

    const file = this.selectedFile();
    if (!file) {
      this.errorMsg.set('Selecione um ficheiro para upload.');
      return;
    }

    this.isUploading.set(true);
    this.errorMsg.set(null);

    const formData = new FormData();
    formData.append('ficheiro', file);
    formData.append('tipo', this.uploadForm.get('tipo')?.value);
    formData.append('nome', this.uploadForm.get('nome')?.value);
    formData.append('descricao', this.uploadForm.get('descricao')?.value || '');
    formData.append('dataDocumento', this.uploadForm.get('dataDocumento')?.value || '');
    formData.append('entidadeRelacionada', this.uploadForm.get('entidadeRelacionada')?.value || '');
    formData.append('entidadeId', this.uploadForm.get('entidadeId')?.value || '');
    formData.append('tags', this.uploadForm.get('tags')?.value || '');
    formData.append('categoria', this.uploadForm.get('categoria')?.value || '');
    formData.append('observacoes', this.uploadForm.get('observacoes')?.value || '');

    this.svc.upload(formData).subscribe({
      next: () => {
        this.isUploading.set(false);
        this.fecharUploadModal();
        this.carregarDocumentos();
        this.carregarEstatisticas();
        this.showToast('Documento enviado com sucesso!');
      },
      error: (err) => {
        this.errorMsg.set(err.error?.message || 'Erro ao enviar documento');
        this.isUploading.set(false);
      }
    });
  }

  downloadDocumento(documento: DocumentoGeral, event?: Event): void {
    if (event) event.stopPropagation();
    this.svc.download(documento.id).subscribe({
      next: (blob) => {
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = documento.nome;
        a.click();
        window.URL.revokeObjectURL(url);
        this.carregarEstatisticas();
      },
      error: (err) => this.errorMsg.set(err.message)
    });
  }

  alternarFavorito(documento: DocumentoGeral, event?: Event): void {
    if (event) event.stopPropagation();
    this.svc.alternarFavorito(documento.id).subscribe({
      next: () => {
        this.carregarDocumentos();
        this.carregarEstatisticas();
      },
      error: (err) => this.errorMsg.set(err.message)
    });
  }

  editarDocumento(documento: DocumentoGeral, event?: Event): void {
    if (event) event.stopPropagation();
    // Implementar edição inline se necessário
    this.showToast('Funcionalidade em desenvolvimento');
  }

  confirmarDelete(documento: DocumentoGeral, event?: Event): void {
    if (event) event.stopPropagation();
    this.documentoParaDelete.set(documento);
    this.showDeleteConfirm.set(true);
  }

  cancelarDelete(): void {
    this.showDeleteConfirm.set(false);
    this.documentoParaDelete.set(null);
  }

  executarDelete(): void {
    const doc = this.documentoParaDelete();
    if (!doc) return;
    this.svc.deletar(doc.id).subscribe({
      next: () => {
        this.cancelarDelete();
        this.carregarDocumentos();
        this.carregarEstatisticas();
        this.showToast('Documento removido com sucesso.');
      },
      error: (err) => { this.errorMsg.set(err.message); this.cancelarDelete(); }
    });
  }

  showToast(msg: string): void {
    this.successMsg.set(msg);
    setTimeout(() => this.successMsg.set(null), 3500);
  }

  clearError(): void { this.errorMsg.set(null); }

  getIconePorTipo(tipo: string): string {
    const icons: Record<string, string> = {
      'Fatura': 'la-file-invoice',
      'POD': 'la-check-circle',
      'Relatorio': 'la-chart-line',
      'Outro': 'la-file-alt'
    };
    return icons[tipo] || 'la-file';
  }

  getCorPorTipo(tipo: string): string {
    const cores: Record<string, string> = {
      'Fatura': '#f59e0b',
      'POD': '#10b981',
      'Relatorio': '#3b82f6',
      'Outro': '#6b7280'
    };
    return cores[tipo] || '#6b7280';
  }

  formatarData(data: string): string {
    if (!data) return '—';
    return new Date(data).toLocaleDateString('pt-PT');
  }

  formatarTamanho(bytes: number): string {
    if (!bytes) return '0 B';
    const units = ['B', 'KB', 'MB', 'GB'];
    let size = bytes;
    let unitIndex = 0;
    while (size >= 1024 && unitIndex < units.length - 1) {
      size /= 1024;
      unitIndex++;
    }
    return `${size.toFixed(1)} ${units[unitIndex]}`;
  }
}