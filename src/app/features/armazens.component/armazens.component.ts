import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ArmazensService, Armazem } from '../../core/services/armazens.service';

@Component({
  selector: 'app-armazens',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './armazens.component.html',
  styleUrls: ['./armazens.component.css']
})
export class ArmazensComponent implements OnInit {
  private armazensService = inject(ArmazensService);

  armazens = signal<Armazem[]>([]);
  isLoading = signal(false);
  isSaving = signal(false);
  errorMsg = signal<string | null>(null);
  successMsg = signal<string | null>(null);

  filtroSearch = '';
  mostrarInativos = signal(false);

  showModal = signal(false);
  isEditing = signal(false);
  editingId = signal<number | null>(null);

  formArmazem: Armazem = this.novoArmazemVazio();

  ngOnInit(): void {
    this.carregarArmazens();
  }

  carregarArmazens(): void {
    this.isLoading.set(true);
    const ativo = this.mostrarInativos() ? undefined : true;
    this.armazensService.listar(this.filtroSearch || undefined, ativo)
      .subscribe({
        next: (data) => {
          this.armazens.set(data);
          this.isLoading.set(false);
        },
        error: (err) => {
          this.errorMsg.set(err.error?.message || 'Erro ao carregar armazéns');
          this.isLoading.set(false);
        }
      });
  }

  private novoArmazemVazio(): Armazem {
    return {
      id: 0,
      codigo: '',
      localizacao: '',
      nome: '',
      tipo: 'principal',
      morada: '',
      codigoPostal: '',
      pais: 'Portugal',
      email: '',
      observacoes: '',
      ativo: true,
      criadoEm: '',
      atualizadoEm: ''
    };
  }

  abrirModalNovo(): void {
    this.isEditing.set(false);
    this.editingId.set(null);
    this.formArmazem = this.novoArmazemVazio();
    this.showModal.set(true);
  }

  abrirModalEditar(armazem: Armazem): void {
    this.isEditing.set(true);
    this.editingId.set(armazem.id);
    this.formArmazem = { ...armazem };
    this.showModal.set(true);
  }

  fecharModal(): void {
    this.showModal.set(false);
  }

  salvarArmazem(): void {
    if (!this.formArmazem.nome?.trim()) {
      this.errorMsg.set('Nome do armazém é obrigatório');
      return;
    }
    if (!this.formArmazem.localizacao?.trim()) {
      this.errorMsg.set('Localização do armazém é obrigatória');
      return;
    }

    this.isSaving.set(true);
    this.errorMsg.set(null);

    const request$ = this.isEditing() && this.editingId()
      ? this.armazensService.atualizar(this.editingId()!, this.formArmazem)
      : this.armazensService.criar(this.formArmazem);

    request$.subscribe({
      next: () => {
        this.isSaving.set(false);
        this.fecharModal();
        this.carregarArmazens();
        this.showToast(this.isEditing() ? 'Armazém actualizado com sucesso' : 'Armazém criado com sucesso');
      },
      error: (err) => {
        this.errorMsg.set(err.error?.message || 'Erro ao guardar armazém');
        this.isSaving.set(false);
      }
    });
  }

  desativarArmazem(armazem: Armazem): void {
    if (confirm(`Deseja desactivar o armazém "${armazem.nome}"?`)) {
      this.armazensService.deletar(armazem.id).subscribe({
        next: () => {
          this.carregarArmazens();
          this.showToast('Armazém desactivado com sucesso');
        },
        error: (err) => {
          this.errorMsg.set(err.error?.message || 'Erro ao desactivar armazém');
        }
      });
    }
  }

  ativarArmazem(armazem: Armazem): void {
    if (confirm(`Deseja activar o armazém "${armazem.nome}"?`)) {
      this.armazensService.ativar(armazem.id).subscribe({
        next: () => {
          this.carregarArmazens();
          this.showToast('Armazém activado com sucesso');
        },
        error: (err) => {
          this.errorMsg.set(err.error?.message || 'Erro ao activar armazém');
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
    this.carregarArmazens();
  }
}