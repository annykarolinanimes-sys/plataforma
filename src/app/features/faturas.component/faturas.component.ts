import {Component, OnInit, OnDestroy, inject, signal, computed, effect} from '@angular/core';
import { CommonModule } from '@angular/common';
import {ReactiveFormsModule, FormBuilder, FormGroup, FormArray,Validators, AbstractControl, ValidationErrors} from '@angular/forms';
import { FormsModule } from '@angular/forms';
import { Subject, takeUntil } from 'rxjs';
import { InvoiceService, Invoice, InvoiceItem } from '../../core/services/invoice.service';
import { ClientesCatalogoService, ClienteModel } from '../../core/services/clientes-catalogo.service';
import { PdfService } from '../../core/services/pdf.service';
import { UiStateService } from '../../core/services/ui-state.service';

function itensNaoVazios(control: AbstractControl): ValidationErrors | null {
  const arr = control as FormArray;
  const validos = arr.controls.filter(g => {
    const v = g.value;
    return v.marca && v.modelo && v.matricula && v.quantidade > 0;
  });
  return validos.length > 0 ? null : { itensVazios: true };
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

  faturas         = signal<Invoice[]>([]);
  clientes        = signal<ClienteModel[]>([]);
  selectedFatura  = signal<Invoice | null>(null);
  isLoading       = signal(false);
  isClientesLoading = signal(false);
  isSaving        = signal(false);
  errorMsg        = signal<string | null>(null);
  successMsg      = signal<string | null>(null);

  filtroSearch = '';
  filtroEstado = '';

  showDeleteConfirm  = signal(false);
  faturaParaDelete   = signal<Invoice | null>(null);

  form!: FormGroup;

  totalFaturado = computed(() =>
    this.faturas()
      .filter(f => f.estado === 'Paga')
      .reduce((s, f) => s + f.valorTotal, 0)
  );

  totalPendentes = computed(() =>
    this.faturas().filter(f => f.estado === 'Pendente').length
  );

  faturasMes = computed(() => {
    const now  = new Date();
    const ano  = now.getFullYear();
    const mes  = now.getMonth();
    return this.faturas().filter(f => {
      const d = new Date(f.dataDoc);
      return d.getFullYear() === ano && d.getMonth() === mes;
    }).length;
  });

  valorTotalForm = signal(0);

  ngOnInit(): void {
    this.buildForm();
    this.carregarClientes();
    this.carregarFaturas();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

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

  carregarFaturas(): void {
    this.isLoading.set(true);
    this.invoiceService
      .listar(this.filtroEstado || undefined, this.filtroSearch || undefined)
      .subscribe({
        next:  data  => { this.faturas.set(data); this.isLoading.set(false); },
        error: err   => {
          this.errorMsg.set(err.error?.message || 'Erro ao carregar faturas');
          this.isLoading.set(false);
        }
      });
  }

  carregarClientes(): void {
    this.isClientesLoading.set(true);
    this.clientesService.listar({ ativo: true, pageSize: 100 }).subscribe({
      next:  r   => { this.clientes.set(r.items); this.isClientesLoading.set(false); },
      error: ()  => { this.clientes.set([]); this.isClientesLoading.set(false); }
    });
  }

  onClienteChange(event: Event): void {
    const val       = (event.target as HTMLSelectElement).value;
    const clienteId = val ? Number(val) : null;
    this.form.patchValue({ clienteId });

    if (!clienteId) return;

    const c = this.clientes().find(x => x.id === clienteId);
    if (!c) return;

    this.form.patchValue({
      clienteNome:     c.nome,
      clienteContacto: c.telefone     || this.form.value.clienteContacto,
      clienteMorada:   c.morada       || this.form.value.clienteMorada,
      clienteNif:      c.contribuinte || this.form.value.clienteNif
    });
  }

  goToCreate(): void {
    this.resetForm();
    this.uiState.goToFaturaCreate();
  }

  goToEdit(fatura: Invoice, event?: Event): void {
    if (event) event.stopPropagation();
    this.carregarDadosParaEdicao(fatura);
    this.uiState.goToFaturaEdit(fatura.id);
  }

  goToDetails(fatura: Invoice, event?: Event): void {
    if (event) event.stopPropagation();
    this.selectedFatura.set(fatura);
    this.uiState.goToFaturaDetails(fatura.id);
  }

  onRowClick(fatura: Invoice): void {
    this.goToDetails(fatura);
  }

  goToList(): void {
    this.uiState.goToFaturaList();
    this.resetForm();
    this.selectedFatura.set(null);
    this.carregarFaturas();
  }

  cancel(): void {
    this.goToList();
  }

  // ── Reset e carregamento do form ──────────────────────────────────────────
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
  }

  // ── Gestão de itens ───────────────────────────────────────────────────────
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

    // Recalcula subtotal ao alterar quantidade ou preço
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
    const total = this.itensArray.controls.reduce((sum, ctrl) => {
      return sum + (+(ctrl.get('subtotal')!.value) || 0);
    }, 0);
    this.valorTotalForm.set(total);
  }

  // ── Guardar fatura ────────────────────────────────────────────────────────
  salvarFatura(): void {
    this.form.markAllAsTouched();

    if (this.form.invalid) {
      if (this.itensArray.hasError('itensVazios')) {
        this.errorMsg.set('Adicione pelo menos um equipamento válido (marca, modelo, matrícula e quantidade > 0).');
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

    const obs$ = this.isEditing() && this.editingId()
      ? this.invoiceService.atualizar(this.editingId()!, payload)
      : this.invoiceService.criar(payload);

    obs$.subscribe({
      next:  () => { this.isSaving.set(false); this.showToast('Fatura guardada com sucesso!'); this.goToList(); },
      error: (err) => { this.errorMsg.set(err.error?.message || 'Erro ao guardar fatura'); this.isSaving.set(false); }
    });
  }

  // ── PDF ───────────────────────────────────────────────────────────────────
  imprimirPdf(fatura: Invoice, event?: Event): void {
    if (event) event.stopPropagation();
    try {
      const blob = this.pdfService.generateInvoicePdf(fatura);
      const url  = URL.createObjectURL(blob);
      const a    = Object.assign(document.createElement('a'), { href: url, download: `Fatura_${fatura.numeroFatura}.pdf` });
      a.click();
      URL.revokeObjectURL(url);
    } catch {
      this.errorMsg.set('Erro ao gerar PDF. Tente novamente.');
    }
  }

  // ── Eliminar ──────────────────────────────────────────────────────────────
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
