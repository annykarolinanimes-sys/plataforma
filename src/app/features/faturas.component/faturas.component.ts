
import {
  Component, OnInit, OnDestroy, inject, signal, computed, effect
} from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  ReactiveFormsModule, FormBuilder, FormGroup, FormArray,
  Validators, AbstractControl, ValidationErrors
} from '@angular/forms';
import { FormsModule } from '@angular/forms';
import { Subject, takeUntil, forkJoin, of, throwError } from 'rxjs';
import { catchError, switchMap } from 'rxjs/operators';
import {
  InvoiceService, Invoice, InvoiceItem, FaturaAnexo
} from '../../core/services/invoice.service';
import { ClientesCatalogoService, ClienteModel } from '../../core/services/clientes-catalogo.service';
import { PdfService } from '../../core/services/pdf.service';
import { UiStateService } from '../../core/services/ui-state.service';

function itensNaoVazios(control: AbstractControl): ValidationErrors | null {
  const arr = control as FormArray;
  const validos = arr.controls.filter(g => {
    const v = g.value;
    const preco = Number(v?.precoUnitario ?? 0);
    return Boolean(v?.marca)
      && Boolean(v?.modelo)
      && Boolean(v?.matricula)
      && Number(v?.quantidade ?? 0) > 0
      && v?.precoUnitario !== null
      && v?.precoUnitario !== ''
      && Number.isFinite(preco)
      && preco >= 0;
  });
  return validos.length > 0 ? null : { itensVazios: true };
}

// Ficheiro pendente antes de ser enviado para o servidor
export interface PendingFile {
  file: File;
  id: string;          // UUID local para tracking no template
  previewUrl?: string; // Para imagens
  erro?: string;
}

@Component({
  selector: 'app-faturas',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, FormsModule],
  templateUrl: './faturas.component.html',
  styleUrls: ['./faturas.component.css']
})
export class FaturasComponent implements OnInit, OnDestroy {

  private fb             = inject(FormBuilder);
  private invoiceService = inject(InvoiceService);
  private clientesService= inject(ClientesCatalogoService);
  private pdfService     = inject(PdfService);
  private uiState        = inject(UiStateService);
  private destroy$       = new Subject<void>();

  currentState = this.uiState.currentFaturaState;
  editingId    = this.uiState.currentFaturaId;

  isViewing = computed(() => this.currentState() === 'details');
  isEditing = computed(() => this.currentState() === 'edit');

  faturas            = signal<Invoice[]>([]);
  clientes           = signal<ClienteModel[]>([]);
  selectedFatura     = signal<Invoice | null>(null);
  isLoading          = signal(false);
  isClientesLoading  = signal(false);
  isSaving           = signal(false);
  errorMsg           = signal<string | null>(null);
  successMsg         = signal<string | null>(null);
  hasSubmitted       = false;
  clienteSearchTerm  = signal('');
  showClienteDropdown = signal(false);
  filteredClientes   = computed(() => {
    const term = this.clienteSearchTerm().trim().toLowerCase();
    if (term.length < 2) return [];
    return this.clientes()
      .filter(c =>
        (c.codigo ?? '').toLowerCase().includes(term) ||
        c.nome.toLowerCase().includes(term) ||
        (c.contribuinte ?? '').toLowerCase().includes(term) ||
        (c.telefone ?? '').toLowerCase().includes(term)
      )
      .slice(0, 10);
  });

  filtroSearch = '';
  filtroEstado = '';

  showDeleteConfirm  = signal(false);
  faturaParaDelete   = signal<Invoice | null>(null);

  form!: FormGroup;

  // ── Estado dos Anexos ─────────────────────────────────────────────────────

  /** Ficheiros selecionados ainda não enviados (apenas em criação/edição) */
  pendingFiles = signal<PendingFile[]>([]);

  /** Anexos já persistidos no servidor (visíveis em view/edit) */
  anexosExistentes = signal<FaturaAnexo[]>([]);

  /** IDs dos ficheiros a fazer upload (para mostrar spinner) */
  uploadingFiles = signal<Set<string>>(new Set());

  /** ID do anexo a ser removido (para mostrar spinner) */
  removingAnexoId = signal<number | null>(null);

  /** Arrastar sobre a zona de drop */
  isDragOver = signal(false);

  private readonly MAX_FILE_SIZE = 5 * 1024 * 1024; // 5 MB
  private readonly ALLOWED_TYPES = ['application/pdf', 'image/jpeg', 'image/jpg', 'image/png'];
  private readonly ALLOWED_EXT   = ['.pdf', '.jpg', '.jpeg', '.png'];

  // ── Computed stats ────────────────────────────────────────────────────────

  totalFaturado = computed(() =>
    this.faturas()
      .filter(f => f.estado === 'Paga')
      .reduce((s, f) => s + f.valorTotal, 0)
  );

  totalPendentes = computed(() =>
    this.faturas().filter(f => f.estado === 'Pendente').length
  );

  faturasMes = computed(() => {
    const now = new Date();
    const ano = now.getFullYear();
    const mes = now.getMonth();
    return this.faturas().filter(f => {
      const d = new Date(f.dataDoc);
      return d.getFullYear() === ano && d.getMonth() === mes;
    }).length;
  });

  valorTotalForm = signal(0);

  // ── Lifecycle ─────────────────────────────────────────────────────────────

  ngOnInit(): void {
    this.buildForm();
    this.carregarClientes();
    this.carregarFaturas();
  }

  ngOnDestroy(): void {
    // Revogar objectURLs de preview para não ter memory leaks
    this.pendingFiles().forEach(pf => {
      if (pf.previewUrl) URL.revokeObjectURL(pf.previewUrl);
    });
    this.destroy$.next();
    this.destroy$.complete();
  }

  // ── Form ──────────────────────────────────────────────────────────────────

  private buildForm(): void {
    this.form = this.fb.group({
      clienteId:         [null as number | null],
      clienteNome:       ['', [Validators.required, Validators.maxLength(200)]],
      clienteContacto:   ['', [Validators.required, Validators.maxLength(100)]],
      clienteEmail:      ['', [Validators.email, Validators.maxLength(200)]],
      clienteMorada:     ['', Validators.maxLength(300)],
      clienteNif:        ['', [Validators.required, Validators.pattern(/^[0-9]{9}$/)]],
      dataDoc:           [new Date().toISOString().split('T')[0], Validators.required],
      estado:            ['Pendente', Validators.required],
      observacoes:       [''],
      quemExecutou:      ['', Validators.maxLength(200)],
      horasTrabalho:     [null as number | null, Validators.min(0)],
      materialUtilizado: [''],
      itens: this.fb.array([], itensNaoVazios)
    });

    this.adicionarItem();

    this.itensArray.valueChanges
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => this.recalcularTotal());
  }

  get itensArray(): FormArray {
    return this.form.get('itens') as FormArray;
  }

  hasError(campo: string, erro?: string): boolean {
    const ctrl = this.form.get(campo);
    if (!ctrl || !ctrl.touched) return false;
    return erro ? ctrl.hasError(erro) : ctrl.invalid;
  }

  hasItemError(index: number, campo: string, erro?: string): boolean {
    const ctrl = this.itensArray.at(index).get(campo);
    if (!ctrl || !ctrl.touched) return false;
    return erro ? ctrl.hasError(erro) : ctrl.invalid;
  }

  // ── Data Loading ──────────────────────────────────────────────────────────

  carregarFaturas(): void {
    this.isLoading.set(true);
    this.invoiceService
      .listar(this.filtroEstado || undefined, this.filtroSearch || undefined)
      .subscribe({
        next:  data => { this.faturas.set(data); this.isLoading.set(false); },
        error: err  => {
          this.errorMsg.set(err.error?.message || 'Erro ao carregar faturas');
          this.isLoading.set(false);
        }
      });
  }

  carregarClientes(): void {
    this.isClientesLoading.set(true);
    this.clientesService.listar({ ativo: true, pageSize: 100 }).subscribe({
      next:  r  => { this.clientes.set(r.items); this.isClientesLoading.set(false); },
      error: () => { this.clientes.set([]); this.isClientesLoading.set(false); }
    });
  }

  // ── Cliente Autocomplete ──────────────────────────────────────────────────

  onClienteNomeInput(event: Event): void {
    const value = (event.target as HTMLInputElement).value;
    this.clienteSearchTerm.set(value);
    this.form.patchValue({
      clienteId: null, clienteNome: value,
      clienteContacto: '', clienteMorada: '', clienteNif: ''
    });
    this.showClienteDropdown.set(value.trim().length >= 2 && this.filteredClientes().length > 0);
  }

  onClienteNomeFocus(): void {
    this.showClienteDropdown.set(this.filteredClientes().length > 0);
  }

  closeClienteDropdown(): void {
    setTimeout(() => this.showClienteDropdown.set(false), 120);
  }

  selecionarCliente(cliente: ClienteModel): void {
    this.form.patchValue({
      clienteId:       cliente.id,
      clienteNome:     cliente.nome,
      clienteContacto: cliente.telefone || '',
      clienteEmail:    cliente.email    || '',
      clienteMorada:   cliente.morada   || '',
      clienteNif:      cliente.contribuinte || ''
    });
    this.clienteSearchTerm.set(cliente.nome);
    this.showClienteDropdown.set(false);
  }

  // ── Navigation ────────────────────────────────────────────────────────────

  goToCreate(): void {
    this.resetForm();
    this.pendingFiles.set([]);
    this.anexosExistentes.set([]);
    this.uiState.goToFaturaCreate();
  }

  goToEdit(fatura: Invoice, event?: Event): void {
    if (event) event.stopPropagation();
    this.carregarDadosParaEdicao(fatura);
    this.pendingFiles.set([]);
    this.anexosExistentes.set(fatura.anexos ?? []);
    this.uiState.goToFaturaEdit(fatura.id);
  }

  goToDetails(fatura: Invoice, event?: Event): void {
    if (event) event.stopPropagation();
    this.selectedFatura.set(fatura);
    this.anexosExistentes.set(fatura.anexos ?? []);
    this.uiState.goToFaturaDetails(fatura.id);
  }

  onRowClick(fatura: Invoice): void {
    this.goToDetails(fatura);
  }

  goToList(): void {
    this.uiState.goToFaturaList();
    this.resetForm();
    this.selectedFatura.set(null);
    this.pendingFiles.set([]);
    this.anexosExistentes.set([]);
    this.carregarFaturas();
  }

  cancel(): void {
    this.goToList();
  }

  private resetForm(): void {
    this.form.reset({
      clienteId: null, clienteNome: '', clienteContacto: '',
      clienteEmail: '', clienteMorada: '', clienteNif: '',
      dataDoc: new Date().toISOString().split('T')[0],
      estado: 'Pendente', observacoes: '',
      quemExecutou: '', horasTrabalho: null, materialUtilizado: ''
    });
    this.itensArray.clear();
    this.adicionarItem();
    this.errorMsg.set(null);
    this.hasSubmitted = false;
    this.form.markAsPristine();
    this.form.markAsUntouched();
  }

  private carregarDadosParaEdicao(fatura: Invoice): void {
    this.form.patchValue({
      clienteId:         fatura.clienteId ?? null,
      clienteNome:       fatura.clienteNome,
      clienteContacto:   fatura.clienteContacto,
      clienteEmail:      fatura.clienteEmail   || '',
      clienteMorada:     fatura.clienteMorada  || '',
      clienteNif:        fatura.clienteNif     || '',
      dataDoc:           fatura.dataDoc,
      estado:            fatura.estado,
      observacoes:       fatura.observacoes    || '',
      quemExecutou:      fatura.quemExecutou   || '',
      horasTrabalho:     fatura.horasTrabalho  ?? null,
      materialUtilizado: fatura.materialUtilizado || ''
    });
    this.itensArray.clear();
    fatura.itens.forEach(item => this.adicionarItem(item));
    this.recalcularTotal();
    this.hasSubmitted = false;
  }

  // ── Itens ─────────────────────────────────────────────────────────────────

  adicionarItem(item?: Partial<InvoiceItem>): void {
    const g = this.fb.group({
      marca:         [item?.marca        || '', Validators.required],
      modelo:        [item?.modelo       || '', Validators.required],
      cor:           [item?.cor          || ''],
      matricula:     [item?.matricula    || '', Validators.required],
      quantidade:    [item?.quantidade   ?? 1,  [Validators.required, Validators.min(1)]],
      precoUnitario: [item?.precoUnitario ?? 0,  [Validators.required, Validators.min(0)]],
      subtotal:      [{ value: item?.subtotal ?? 0, disabled: true }]
    });

    g.get('quantidade')!.valueChanges
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => this.calcularSubtotal(this.itensArray.controls.indexOf(g)));

    g.get('precoUnitario')!.valueChanges
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => this.calcularSubtotal(this.itensArray.controls.indexOf(g)));

    this.itensArray.push(g);
  }

  removerItem(index: number): void {
    this.itensArray.removeAt(index);
    if (this.itensArray.length === 0) this.adicionarItem();
    this.recalcularTotal();
  }

  calcularSubtotal(index: number): void {
    const g   = this.itensArray.at(index) as FormGroup;
    const qty = +(g.get('quantidade')!.value  || 0);
    const pu  = +(g.get('precoUnitario')!.value || 0);
    g.get('subtotal')!.setValue(+(qty * pu).toFixed(2), { emitEvent: false });
    this.recalcularTotal();
  }

  private recalcularTotal(): void {
    const total = this.itensArray.controls.reduce((sum, ctrl) =>
      sum + (+(ctrl.get('subtotal')!.value) || 0), 0);
    this.valorTotalForm.set(total);
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
    if (files) this.processarFicheiros(Array.from(files));
  }

  onFileSelect(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (input.files) {
      this.processarFicheiros(Array.from(input.files));
      // Limpar o input para permitir re-selecionar o mesmo ficheiro
      input.value = '';
    }
  }

  private processarFicheiros(files: File[]): void {
    const novos: PendingFile[] = [];

    for (const file of files) {
      const ext = '.' + file.name.split('.').pop()?.toLowerCase();

      if (file.size > this.MAX_FILE_SIZE) {
        this.errorMsg.set(`"${file.name}" excede o limite de 5 MB.`);
        continue;
      }

      if (!this.ALLOWED_TYPES.includes(file.type) && !this.ALLOWED_EXT.includes(ext)) {
        this.errorMsg.set(`"${file.name}" — tipo não suportado. Use PDF, JPG ou PNG.`);
        continue;
      }

      const pending: PendingFile = {
        file,
        id: crypto.randomUUID()
      };

      if (file.type.startsWith('image/')) {
        pending.previewUrl = URL.createObjectURL(file);
      }

      novos.push(pending);
    }

    if (novos.length > 0) {
      this.pendingFiles.update(prev => [...prev, ...novos]);
      this.errorMsg.set(null);
    }
  }

  removerFicheiroPendente(id: string): void {
    const file = this.pendingFiles().find(f => f.id === id);
    if (file?.previewUrl) URL.revokeObjectURL(file.previewUrl);
    this.pendingFiles.update(prev => prev.filter(f => f.id !== id));
  }

  /** Remove um anexo já persistido no servidor */
  removerAnexoExistente(anexo: FaturaAnexo): void {
    const faturaId = this.isEditing()
      ? this.editingId()
      : this.selectedFatura()?.id;

    if (!faturaId) return;

    this.removingAnexoId.set(anexo.id);

    this.invoiceService.removerAnexo(faturaId, anexo.id).subscribe({
      next: res => {
        this.anexosExistentes.update(prev => prev.filter(a => a.id !== anexo.id));
        // Actualizar também a fatura no signal de selectedFatura
        if (this.selectedFatura()) {
          const updated = { ...this.selectedFatura()!, anexos: this.anexosExistentes() };
          this.selectedFatura.set(updated);
        }
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
  downloadAnexo(anexo: FaturaAnexo): void {
    const faturaId = this.selectedFatura()?.id ?? this.editingId();
    if (!faturaId) return;

    this.invoiceService.downloadAnexo(faturaId, anexo.id).subscribe({
      next: blob => {
        const url = URL.createObjectURL(blob);
        const a   = Object.assign(document.createElement('a'), {
          href: url,
          download: anexo.nomeOriginal
        });
        a.click();
        URL.revokeObjectURL(url);
      },
      error: () => this.errorMsg.set('Erro ao descarregar o ficheiro.')
    });
  }

  /** Devolve o ícone Line Awesome adequado ao MIME type */
  getFileIcon(mimeType: string): string {
    if (mimeType === 'application/pdf')              return 'la-file-pdf';
    if (mimeType.startsWith('image/'))               return 'la-file-image';
    return 'la-file-alt';
  }

  formatarTamanho(bytes: number): string {
    if (bytes < 1024)              return `${bytes} B`;
    if (bytes < 1024 * 1024)       return `${(bytes / 1024).toFixed(1)} KB`;
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  }

  // ── Save / Upload ─────────────────────────────────────────────────────────

  salvarFatura(): void {
    this.hasSubmitted = true;
    this.form.markAllAsTouched();

    if (this.form.invalid) {
      if (this.itensArray.hasError('itensVazios')) {
        this.errorMsg.set('Adicione pelo menos um equipamento válido com marca, modelo, matrícula, quantidade > 0 e preço unitário obrigatório.');
      } else {
        this.errorMsg.set('Corrija os erros assinalados no formulário antes de guardar.');
      }
      return;
    }

    this.isSaving.set(true);
    this.errorMsg.set(null);

    const raw = this.form.getRawValue();
    const itensValidos: InvoiceItem[] = raw.itens
      .filter((i: any) => i.marca && i.modelo && i.matricula && i.quantidade > 0);

    const payload = {
      clienteId:         raw.clienteId  || undefined,
      clienteNome:       raw.clienteNome,
      clienteContacto:   raw.clienteContacto,
      clienteEmail:      raw.clienteEmail      || undefined,
      clienteMorada:     raw.clienteMorada     || undefined,
      clienteNif:        raw.clienteNif        || undefined,
      dataDoc:           raw.dataDoc,
      estado:            raw.estado,
      observacoes:       raw.observacoes       || undefined,
      quemExecutou:      raw.quemExecutou      || undefined,
      horasTrabalho:     raw.horasTrabalho     || undefined,
      materialUtilizado: raw.materialUtilizado || undefined,
      itens:             itensValidos
    };

    const save$ = this.isEditing() && this.editingId()
      ? this.invoiceService.atualizar(this.editingId()!, payload)
      : this.invoiceService.criar(payload);

    save$.pipe(
      switchMap(fatura => {
        // Após guardar a fatura, fazer upload dos ficheiros pendentes
        const uploads = this.pendingFiles().map(pf =>
          this.invoiceService.uploadAnexo(fatura.id, pf.file).pipe(
            catchError(err => {
              console.error('Erro no upload de', pf.file.name, err);
              return throwError(() => ({ fileName: pf.file.name, error: err }));
            })
          )
        );

        if (uploads.length === 0) return of(fatura);

        return forkJoin(uploads).pipe(
          // Retornar a fatura original — a lista actualizada virá do reload
          switchMap(() => of(fatura))
        );
      })
    ).subscribe({
      next: () => {
        this.isSaving.set(false);
        this.pendingFiles.set([]);
        this.showToast('Fatura guardada com sucesso!');
        this.goToList();
      },
      error: err => {
        this.isSaving.set(false);
        // If upload failed, err may be an object { fileName, error }
        if (err && (err as any).fileName) {
          const fileName = (err as any).fileName;
          const inner = (err as any).error;
          const serverMsg = inner?.error?.message ?? inner?.message ?? inner?.statusText ?? null;
          this.errorMsg.set(`Erro no upload de ${fileName}: ${serverMsg ?? 'ver console para detalhes'}`);
        } else {
          this.errorMsg.set(err.error?.message || 'Erro ao guardar fatura.');
        }
      }
    });
  }

  // ── PDF ───────────────────────────────────────────────────────────────────

  imprimirPdf(fatura: Invoice, event?: Event): void {
    if (event) event.stopPropagation();
    try {
      const blob = this.pdfService.generateInvoicePdf(fatura);
      const url  = URL.createObjectURL(blob);
      const a    = Object.assign(document.createElement('a'), {
        href: url,
        download: `Fatura_${fatura.numeroFatura}.pdf`
      });
      a.click();
      URL.revokeObjectURL(url);
    } catch {
      this.errorMsg.set('Erro ao gerar PDF. Tente novamente.');
    }
  }

  // ── Delete ────────────────────────────────────────────────────────────────

  confirmarDelete(fatura: Invoice, event?: Event): void {
    if (event) event.stopPropagation();
    this.faturaParaDelete.set(fatura);
    this.showDeleteConfirm.set(true);
  }

  fecharConfirmacao(): void {
    this.showDeleteConfirm.set(false);
    this.faturaParaDelete.set(null);
  }

  eliminarFatura(): void {
    const fatura = this.faturaParaDelete();
    if (!fatura) return;

    this.invoiceService.deletar(fatura.id).subscribe({
      next: res => { this.showToast(res.message); this.fecharConfirmacao(); this.carregarFaturas(); },
      error: err => { this.errorMsg.set(err.error?.message || 'Erro ao eliminar'); this.fecharConfirmacao(); }
    });
  }

  // ── Helpers ───────────────────────────────────────────────────────────────

  getEstadoClass(estado: string): string {
    const map: Record<string, string> = {
      'Pendente':  'badge--pendente',
      'Paga':      'badge--paga',
      'Cancelada': 'badge--cancelada'
    };
    return map[estado] ?? 'badge--pendente';
  }

  formatarData(data: string): string {
    if (!data) return '—';
    return new Date(data).toLocaleDateString('pt-PT');
  }

  formatarMoeda(valor: number): string {
    return new Intl.NumberFormat('pt-PT', { style: 'currency', currency: 'EUR' }).format(valor);
  }

  showToast(msg: string): void {
    this.successMsg.set(msg);
    setTimeout(() => this.successMsg.set(null), 3500);
  }

  clearError(): void {
    this.errorMsg.set(null);
  }
}