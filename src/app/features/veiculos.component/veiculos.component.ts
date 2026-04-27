import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { VeiculosService, Veiculo } from '../../core/services/veiculos.service';

@Component({
  selector: 'app-veiculos',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './veiculos.component.html',
  styleUrls: ['./veiculos.component.css']
})
export class VeiculosComponent implements OnInit {
  private veiculosService = inject(VeiculosService);

  veiculos = signal<Veiculo[]>([]);
  isLoading = signal(false);
  isSaving = signal(false);
  errorMsg = signal<string | null>(null);
  successMsg = signal<string | null>(null);

  filtroSearch = '';
  mostrarInativos = signal(false);

  showModal = signal(false);
  isEditing = signal(false);
  editingId = signal<number | null>(null);

  formVeiculo: Veiculo = this.novoVeiculoVazio();

  ngOnInit(): void {
    this.carregarVeiculos();
  }

  carregarVeiculos(): void {
    this.isLoading.set(true);
    const ativo = this.mostrarInativos() ? undefined : true;
    this.veiculosService.listar(this.filtroSearch || undefined, ativo)
      .subscribe({
        next: (data) => {
          this.veiculos.set(data);
          this.isLoading.set(false);
        },
        error: (err) => {
          this.errorMsg.set(err.error?.message || 'Erro ao carregar veículos');
          this.isLoading.set(false);
        }
      });
  }

  private novoVeiculoVazio(): Veiculo {
    return {
      id: 0,
      matricula: '',
      marca: '',
      modelo: '',
      cor: '',
      ano: undefined,
      vin: '',
      tipoCombustivel: '',
      cilindrada: undefined,
      potencia: undefined,
      lugares: undefined,
      peso: undefined,
      proprietarioId: undefined,
      ativo: true,
      observacoes: '',
      criadoEm: '',
      atualizadoEm: ''
    };
  }

  abrirModalNovo(): void {
    this.isEditing.set(false);
    this.editingId.set(null);
    this.formVeiculo = this.novoVeiculoVazio();
    this.showModal.set(true);
  }

  abrirModalEditar(veiculo: Veiculo): void {
    this.isEditing.set(true);
    this.editingId.set(veiculo.id);
    this.formVeiculo = { ...veiculo };
    this.showModal.set(true);
  }

  fecharModal(): void {
    this.showModal.set(false);
  }

  salvarVeiculo(): void {
    if (!this.formVeiculo.matricula?.trim()) {
      this.errorMsg.set('Matrícula é obrigatória');
      return;
    }
    if (!this.formVeiculo.marca?.trim()) {
      this.errorMsg.set('Marca é obrigatória');
      return;
    }
    if (!this.formVeiculo.modelo?.trim()) {
      this.errorMsg.set('Modelo é obrigatório');
      return;
    }

    this.isSaving.set(true);
    this.errorMsg.set(null);

    const request$ = this.isEditing() && this.editingId()
      ? this.veiculosService.atualizar(this.editingId()!, this.formVeiculo)
      : this.veiculosService.criar(this.formVeiculo);

    request$.subscribe({
      next: () => {
        this.isSaving.set(false);
        this.fecharModal();
        this.carregarVeiculos();
        this.showToast(this.isEditing() ? 'Veículo actualizado com sucesso' : 'Veículo criado com sucesso');
      },
      error: (err) => {
        this.errorMsg.set(err.error?.message || 'Erro ao guardar veículo');
        this.isSaving.set(false);
      }
    });
  }

  desativarVeiculo(veiculo: Veiculo): void {
    if (confirm(`Deseja desactivar o veículo "${veiculo.matricula}" (${veiculo.marca} ${veiculo.modelo})?`)) {
      this.veiculosService.deletar(veiculo.id).subscribe({
        next: () => {
          this.carregarVeiculos();
          this.showToast('Veículo desactivado com sucesso');
        },
        error: (err) => {
          this.errorMsg.set(err.error?.message || 'Erro ao desactivar veículo');
        }
      });
    }
  }

  ativarVeiculo(veiculo: Veiculo): void {
    if (confirm(`Deseja activar o veículo "${veiculo.matricula}"?`)) {
      this.veiculosService.ativar(veiculo.id).subscribe({
        next: () => {
          this.carregarVeiculos();
          this.showToast('Veículo activado com sucesso');
        },
        error: (err) => {
          this.errorMsg.set(err.error?.message || 'Erro ao activar veículo');
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
    this.carregarVeiculos();
  }
}