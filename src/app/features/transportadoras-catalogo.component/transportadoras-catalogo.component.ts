import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TransportadorasCatalogoService, TransportadoraCatalogo } from '../../core/services/transportadoras-catalogo.service';

@Component({
  selector: 'app-transportadoras-catalogo',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './transportadoras-catalogo.component.html',
  styleUrls: ['./transportadoras-catalogo.component.css']
})
export class TransportadorasCatalogoComponent implements OnInit {
  private transportadorasService = inject(TransportadorasCatalogoService);

  transportadoras = signal<TransportadoraCatalogo[]>([]);
  isLoading = signal(false);
  isSaving = signal(false);
  errorMsg = signal<string | null>(null);
  successMsg = signal<string | null>(null);

  filtroSearch = '';
  mostrarInativos = signal(false);

  showModal = signal(false);
  isEditing = signal(false);
  editingId = signal<number | null>(null);

  formTransportadora: TransportadoraCatalogo = this.novoTransportadoraVazio();

  ngOnInit(): void {
    this.carregarTransportadoras();
  }

  carregarTransportadoras(): void {
    this.isLoading.set(true);
    const ativo = this.mostrarInativos() ? undefined : true;
    this.transportadorasService.listar(this.filtroSearch || undefined, ativo)
      .subscribe({
        next: (data) => {
          this.transportadoras.set(data);
          this.isLoading.set(false);
        },
        error: (err) => {
          this.errorMsg.set(err.error?.message || 'Erro ao carregar transportadoras');
          this.isLoading.set(false);
        }
      });
  }

  private novoTransportadoraVazio(): TransportadoraCatalogo {
    return {
      id: 0,
      codigo: '',
      nome: '',
      nif: '',
      telefone: '',
      email: '',
      morada: '',
      localidade: '',
      codigoPostal: '',
      pais: 'Portugal',
      contactoNome: '',
      contactoTelefone: '',
      observacoes: '',
      ativo: true,
      criadoEm: '',
      atualizadoEm: ''
    };
  }

  abrirModalNovo(): void {
    this.isEditing.set(false);
    this.editingId.set(null);
    this.formTransportadora = this.novoTransportadoraVazio();
    this.showModal.set(true);
  }

  abrirModalEditar(transportadora: TransportadoraCatalogo): void {
    this.isEditing.set(true);
    this.editingId.set(transportadora.id);
    this.formTransportadora = { ...transportadora };
    this.showModal.set(true);
  }

  fecharModal(): void {
    this.showModal.set(false);
  }

  salvarTransportadora(): void {
    if (!this.formTransportadora.codigo?.trim()) {
      this.errorMsg.set('Código da transportadora é obrigatório');
      return;
    }
    if (!this.formTransportadora.nome?.trim()) {
      this.errorMsg.set('Nome da transportadora é obrigatório');
      return;
    }

    this.isSaving.set(true);
    this.errorMsg.set(null);

    const request$ = this.isEditing() && this.editingId()
      ? this.transportadorasService.atualizar(this.editingId()!, this.formTransportadora)
      : this.transportadorasService.criar(this.formTransportadora);

    request$.subscribe({
      next: () => {
        this.isSaving.set(false);
        this.fecharModal();
        this.carregarTransportadoras();
        this.showToast(this.isEditing() ? 'Transportadora actualizada com sucesso' : 'Transportadora criada com sucesso');
      },
      error: (err) => {
        this.errorMsg.set(err.error?.message || 'Erro ao guardar transportadora');
        this.isSaving.set(false);
      }
    });
  }

  desativarTransportadora(transportadora: TransportadoraCatalogo): void {
    if (confirm(`Deseja desactivar a transportadora "${transportadora.nome}"?`)) {
      this.transportadorasService.deletar(transportadora.id).subscribe({
        next: () => {
          this.carregarTransportadoras();
          this.showToast('Transportadora desactivada com sucesso');
        },
        error: (err) => {
          this.errorMsg.set(err.error?.message || 'Erro ao desactivar transportadora');
        }
      });
    }
  }

  ativarTransportadora(transportadora: TransportadoraCatalogo): void {
    if (confirm(`Deseja activar a transportadora "${transportadora.nome}"?`)) {
      this.transportadorasService.ativar(transportadora.id).subscribe({
        next: () => {
          this.carregarTransportadoras();
          this.showToast('Transportadora activada com sucesso');
        },
        error: (err) => {
          this.errorMsg.set(err.error?.message || 'Erro ao activar transportadora');
        }
      });
    }
  }

  showToast(msg: string): void {
    this.successMsg.set(msg);
    setTimeout(() => this.successMsg.set(null), 3000);
  }

  clearError(): void {
    this.errorMsg.set(null);
  }

  toggleInativos(): void {
    this.mostrarInativos.update(v => !v);
    this.carregarTransportadoras();
  }
}