// src/app/features/faturas.component/faturas.component.ts
import { Component, OnInit, inject, signal, computed, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { InvoiceService, Invoice, InvoiceItem, CreateInvoiceRequest } from '../../core/services/invoice.service';

@Component({
  selector: 'app-faturas',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './faturas.component.html',
  styleUrls: ['./faturas.component.css']
})
export class FaturasComponent implements OnInit {
  private invoiceService = inject(InvoiceService);
  private cdr = inject(ChangeDetectorRef);

  // Estado
  isLoading = this.invoiceService.isLoading;
  faturas = signal<Invoice[]>([]);
  errorMsg = signal<string | null>(null);
  successMsg = signal<string | null>(null);
  isSaving = signal(false);

  // Filtros
  filtroSearch = '';
  filtroEstado = '';

  // Modais
  showDetailModal = signal(false);
  showFormModal = signal(false);
  showDeleteConfirm = signal(false);
  isEditing = signal(false);
  editingId = signal<number | null>(null);

  // Dados selecionados
  selectedFatura = signal<Invoice | null>(null);
  faturaParaDelete = signal<Invoice | null>(null);

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

  valorTotalForm = computed(() => {
    return this.formItens.reduce((sum, item) => sum + (item.subtotal || 0), 0);
  });

  ngOnInit(): void {
    this.carregarFaturas();
  }

  carregarFaturas(): void {
    console.log('Carregando faturas...');
    this.invoiceService.listar(this.filtroEstado || undefined, this.filtroSearch || undefined).subscribe({
      next: (data) => {
        console.log('Faturas recebidas:', data);
        this.faturas.set(data);
        this.cdr.detectChanges();
      },
      error: (err) => {
        console.error('Erro ao carregar faturas:', err);
        this.errorMsg.set(err.error?.message || 'Erro ao carregar faturas');
      }
    });
  }

  selecionarFatura(fatura: Invoice): void {
    this.selectedFatura.set(fatura);
    this.showDetailModal.set(true);
  }

  fecharModalDetalhe(): void {
    this.showDetailModal.set(false);
    this.selectedFatura.set(null);
  }

  abrirModalNovaFatura(): void {
    this.isEditing.set(false);
    this.editingId.set(null);
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
    this.showFormModal.set(true);
  }

  abrirModalEditarFatura(fatura: Invoice): void {
    this.isEditing.set(true);
    this.editingId.set(fatura.id);
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
    this.showFormModal.set(true);
  }

  fecharModalForm(): void {
    this.showFormModal.set(false);
    this.formItens = [];
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

    const request: CreateInvoiceRequest = {
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

    this.isSaving.set(true);
    this.errorMsg.set(null);

    if (this.isEditing() && this.editingId()) {
      this.invoiceService.atualizar(this.editingId()!, request).subscribe({
        next: () => {
          this.isSaving.set(false);
          this.fecharModalForm();
          this.carregarFaturas();
          this.showToast('Fatura atualizada com sucesso!');
        },
        error: (err) => {
          this.errorMsg.set(err.error?.message || 'Erro ao atualizar fatura');
          this.isSaving.set(false);
        }
      });
    } else {
      this.invoiceService.criar(request).subscribe({
        next: () => {
          this.isSaving.set(false);
          this.fecharModalForm();
          this.carregarFaturas();
          this.showToast('Fatura criada com sucesso!');
        },
        error: (err) => {
          this.errorMsg.set(err.error?.message || 'Erro ao criar fatura');
          this.isSaving.set(false);
        }
      });
    }
  }

  imprimirPdf(fatura: Invoice): void {
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

  confirmarDelete(fatura: Invoice): void {
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
        if (this.selectedFatura()?.id === fatura.id) {
          this.fecharModalDetalhe();
        }
      },
      error: (err) => {
        this.errorMsg.set(err.error?.message || 'Erro ao eliminar fatura');
        this.fecharConfirmacao();
      }
    });
  }

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