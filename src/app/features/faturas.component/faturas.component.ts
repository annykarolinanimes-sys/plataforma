import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { InvoiceService, Invoice, InvoiceItem } from '../../core/services/invoice.service';
import { UiStateService } from '../../core/services/ui-state.service';

@Component({
  selector: 'app-faturas',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './faturas.component.html',
  styleUrls: ['./faturas.component.css']
})
export class FaturasComponent implements OnInit {
  private invoiceService = inject(InvoiceService);
  private uiState = inject(UiStateService);

  // Estado da UI
  currentState = this.uiState.currentFaturaState;
  editingId = this.uiState.currentFaturaId;

  // Dados
  faturas = signal<Invoice[]>([]);
  isLoading = signal(false);
  isSaving = signal(false);
  errorMsg = signal<string | null>(null);
  successMsg = signal<string | null>(null);

  // Filtros
  filtroSearch = '';
  filtroEstado = '';

  // Formulário
  formCliente = {
    clienteNome: '',
    clienteContacto: '',
    clienteEmail: '',
    clienteMorada: '',
    clienteNif: '',
    dataDoc: new Date().toISOString().split('T')[0],
    estado: 'Pendente',
    observacoes: '',
    quemExecutou: '',
    horasTrabalho: null as number | null,
    materialUtilizado: ''
  };
  formItens: InvoiceItem[] = [];

  // Fatura em detalhe
  selectedFatura = signal<Invoice | null>(null);

  // Modal apenas para ELIMINAR
  showDeleteConfirm = signal(false);
  faturaParaDelete = signal<Invoice | null>(null);

  valorTotalForm = computed(() => {
    return this.formItens.reduce((sum, item) => sum + (item.subtotal || 0), 0);
  });

  ngOnInit(): void {
    this.carregarFaturas();
  }

  carregarFaturas(): void {
    this.isLoading.set(true);
    this.invoiceService.listar(this.filtroEstado || undefined, this.filtroSearch || undefined).subscribe({
      next: (data) => {
        this.faturas.set(data);
        this.isLoading.set(false);
      },
      error: (err) => {
        this.errorMsg.set(err.error?.message || 'Erro ao carregar faturas');
        this.isLoading.set(false);
      }
    });
  }

  // ─── Ações de navegação ─────────────────────────────────────────────────

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

  // Clicar na linha da tabela
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

  // ─── Formulário ─────────────────────────────────────────────────────────

  private resetForm(): void {
    this.formCliente = {
      clienteNome: '',
      clienteContacto: '',
      clienteEmail: '',
      clienteMorada: '',
      clienteNif: '',
      dataDoc: new Date().toISOString().split('T')[0],
      estado: 'Pendente',
      observacoes: '',
      quemExecutou: '',
      horasTrabalho: null,
      materialUtilizado: ''
    };
    this.formItens = [this.novoItemVazio()];
    this.errorMsg.set(null);
  }

  private carregarDadosParaEdicao(fatura: Invoice): void {
    this.formCliente = {
      clienteNome: fatura.clienteNome,
      clienteContacto: fatura.clienteContacto,
      clienteEmail: fatura.clienteEmail || '',
      clienteMorada: fatura.clienteMorada || '',
      clienteNif: fatura.clienteNif || '',
      dataDoc: fatura.dataDoc,
      estado: fatura.estado,
      observacoes: fatura.observacoes || '',
      quemExecutou: fatura.quemExecutou || '',
      horasTrabalho: fatura.horasTrabalho || null,
      materialUtilizado: fatura.materialUtilizado || ''
    };
    this.formItens = fatura.itens.map(item => ({
      marca: item.marca,
      modelo: item.modelo,
      cor: item.cor,
      matricula: item.matricula,
      quantidade: item.quantidade,
      precoUnitario: item.precoUnitario,
      subtotal: item.subtotal
    }));
  }

  adicionarItem(): void {
    this.formItens.push(this.novoItemVazio());
  }

  removerItem(index: number): void {
    this.formItens.splice(index, 1);
    if (this.formItens.length === 0) {
      this.formItens.push(this.novoItemVazio());
    }
  }

  calcularSubtotal(index: number): void {
    const item = this.formItens[index];
    item.subtotal = (item.quantidade || 0) * (item.precoUnitario || 0);
  }

  private novoItemVazio(): InvoiceItem {
    return {
      marca: '',
      modelo: '',
      cor: '',
      matricula: '',
      quantidade: 1,
      precoUnitario: 0,
      subtotal: 0
    };
  }

  salvarFatura(): void {
    if (!this.formCliente.clienteNome || !this.formCliente.clienteContacto) {
      this.errorMsg.set('Preencha o nome e contacto do cliente.');
      return;
    }

    const itensValidos = this.formItens.filter(i => i.marca && i.modelo && i.matricula);
    if (itensValidos.length === 0) {
      this.errorMsg.set('Adicione pelo menos um equipamento válido.');
      return;
    }

    this.isSaving.set(true);
    this.errorMsg.set(null);

    const requestData = {
      clienteNome: this.formCliente.clienteNome,
      clienteContacto: this.formCliente.clienteContacto,
      clienteEmail: this.formCliente.clienteEmail || undefined,
      clienteMorada: this.formCliente.clienteMorada || undefined,
      clienteNif: this.formCliente.clienteNif || undefined,
      dataDoc: this.formCliente.dataDoc,
      estado: this.formCliente.estado,
      observacoes: this.formCliente.observacoes || undefined,
      quemExecutou: this.formCliente.quemExecutou || undefined,
      horasTrabalho: this.formCliente.horasTrabalho || undefined,
      materialUtilizado: this.formCliente.materialUtilizado || undefined,
      itens: itensValidos
    };

    if (this.uiState.isFaturaEdit() && this.editingId()) {
      this.invoiceService.atualizar(this.editingId()!, requestData).subscribe({
        next: () => this.onSaveSuccess('Fatura atualizada com sucesso!'),
        error: (err) => this.onSaveError(err)
      });
    } else {
      this.invoiceService.criar(requestData).subscribe({
        next: () => this.onSaveSuccess('Fatura criada com sucesso!'),
        error: (err) => this.onSaveError(err)
      });
    }
  }

  private onSaveSuccess(msg: string): void {
    this.isSaving.set(false);
    this.showToast(msg);
    this.goToList();
  }

  private onSaveError(err: any): void {
    this.errorMsg.set(err.error?.message || 'Erro ao guardar fatura');
    this.isSaving.set(false);
  }

  // ─── Ações ──────────────────────────────────────────────────────────────

  imprimirPdf(fatura: Invoice, event?: Event): void {
    if (event) event.stopPropagation();
    this.invoiceService.gerarPdf(fatura.id).subscribe({
      next: (blob) => {
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `Fatura_${fatura.numeroFatura}.pdf`;
        a.click();
        window.URL.revokeObjectURL(url);
      },
      error: (err) => {
        this.errorMsg.set(err.error?.message || 'Erro ao gerar PDF');
      }
    });
  }

  // ─── Modal apenas para ELIMINAR ─────────────────────────────────────────

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
      next: (res) => {
        this.showToast(res.message);
        this.fecharConfirmacao();
        this.carregarFaturas();
      },
      error: (err) => {
        this.errorMsg.set(err.error?.message || 'Erro ao eliminar fatura');
        this.fecharConfirmacao();
      }
    });
  }

  // ─── Helpers ────────────────────────────────────────────────────────────

  getEstadoClass(estado: string): string {
    const classes: Record<string, string> = {
      'Pendente': 'badge-pendente',
      'Paga': 'badge-paga',
      'Cancelada': 'badge-cancelada'
    };
    return classes[estado] || 'badge-pendente';
  }

  formatarData(data: string): string {
    if (!data) return '—';
    return new Date(data).toLocaleDateString('pt-PT');
  }

  formatarMoeda(valor: number): string {
    return new Intl.NumberFormat('pt-PT', {
      style: 'currency',
      currency: 'EUR'
    }).format(valor);
  }

  showToast(msg: string): void {
    this.successMsg.set(msg);
    setTimeout(() => this.successMsg.set(null), 3000);
  }

  clearError(): void {
    this.errorMsg.set(null);
  }
}