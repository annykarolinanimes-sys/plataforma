import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RotasCatalogoService, RotaCatalogo } from '../../core/services/rotas-catalogo.service';
import { TransportadorasCatalogoService, TransportadoraCatalogo } from '../../core/services/transportadoras-catalogo.service';
import { HttpErrorResponse } from '@angular/common/http';

@Component({
  selector: 'app-rotas-catalogo',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './rotas-catalogo.component.html',
  styleUrls: ['./rotas-catalogo.component.css']
})
export class RotasCatalogoComponent implements OnInit {
  private rotasService = inject(RotasCatalogoService);
  private transportadorasService = inject(TransportadorasCatalogoService);

  rotas = signal<RotaCatalogo[]>([]);
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

  formRota: RotaCatalogo = this.novaRotaVazia();

  ngOnInit(): void {
    this.carregarRotas();
    this.carregarTransportadoras();
  }

  carregarRotas(): void {
    this.isLoading.set(true);
    const ativo = this.mostrarInativos() ? undefined : true;
    this.rotasService.listar(this.filtroSearch || undefined, ativo)
      .subscribe({
        next: (data: RotaCatalogo[]) => {
          this.rotas.set(data);
          this.isLoading.set(false);
        },
        error: (err: HttpErrorResponse) => {
          this.errorMsg.set(err.error?.message || 'Erro ao carregar rotas');
          this.isLoading.set(false);
        }
      });
  }

  carregarTransportadoras(): void {
    this.transportadorasService.listar(undefined, true).subscribe({
      next: (data: TransportadoraCatalogo[]) => {
        this.transportadoras.set(data);
      },
      error: (err: HttpErrorResponse) => {
        console.error('Erro ao carregar transportadoras:', err);
      }
    });
  }

  private novaRotaVazia(): RotaCatalogo {
    return {
      id: 0,
      codigo: '',
      nome: '',
      descricao: '',
      origem: '',
      destino: '',
      distanciaKm: undefined,
      tempoEstimadoMin: undefined,
      transportadoraId: undefined,
      ativo: true,
      criadoEm: '',
      atualizadoEm: ''
    };
  }

  abrirModalNovo(): void {
    this.isEditing.set(false);
    this.editingId.set(null);
    this.formRota = this.novaRotaVazia();
    this.showModal.set(true);
  }

  abrirModalEditar(rota: RotaCatalogo): void {
    this.isEditing.set(true);
    this.editingId.set(rota.id);
    this.formRota = { ...rota };
    this.showModal.set(true);
  }

  fecharModal(): void {
    this.showModal.set(false);
  }

  salvarRota(): void {
    if (!this.formRota.codigo?.trim()) {
      this.errorMsg.set('Código da rota é obrigatório');
      return;
    }
    if (!this.formRota.nome?.trim()) {
      this.errorMsg.set('Nome da rota é obrigatório');
      return;
    }

    this.isSaving.set(true);
    this.errorMsg.set(null);

    const request$ = this.isEditing() && this.editingId()
      ? this.rotasService.atualizar(this.editingId()!, this.formRota)
      : this.rotasService.criar(this.formRota);

    request$.subscribe({
      next: (response: RotaCatalogo) => {
        this.isSaving.set(false);
        this.fecharModal();
        this.carregarRotas();
        this.showToast(this.isEditing() ? 'Rota actualizada com sucesso' : 'Rota criada com sucesso');
      },
      error: (err: HttpErrorResponse) => {
        this.errorMsg.set(err.error?.message || 'Erro ao guardar rota');
        this.isSaving.set(false);
      }
    });
  }

  desativarRota(rota: RotaCatalogo): void {
    if (confirm(`Deseja desactivar a rota "${rota.nome}"?`)) {
      this.rotasService.deletar(rota.id).subscribe({
        next: (response: { message: string }) => {
          this.carregarRotas();
          this.showToast('Rota desactivada com sucesso');
        },
        error: (err: HttpErrorResponse) => {
          this.errorMsg.set(err.error?.message || 'Erro ao desactivar rota');
        }
      });
    }
  }

  ativarRota(rota: RotaCatalogo): void {
    if (confirm(`Deseja activar a rota "${rota.nome}"?`)) {
      this.rotasService.ativar(rota.id).subscribe({
        next: (response: { message: string }) => {
          this.carregarRotas();
          this.showToast('Rota activada com sucesso');
        },
        error: (err: HttpErrorResponse) => {
          this.errorMsg.set(err.error?.message || 'Erro ao activar rota');
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
    this.carregarRotas();
  }
}