import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../core/services/auth.service';
import { UserService } from '../../core/services/user.service';

interface UserProfile {
  id: number;
  nome: string;
  email: string;
  role: string;
  status: string;
  departamento?: string;
  cargo?: string;
  telefone?: string;
  avatarUrl?: string;
  dataCriacao: string;
  ultimoLogin?: string;
}

@Component({
  selector: 'app-definicao',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './definicao.component.html',
  styleUrls: ['./definicao.component.css']
})
export class DefinicaoComponent implements OnInit {
  private authService = inject(AuthService);
  private userService = inject(UserService);
  
  isLoading = signal(true);
  isSaving = signal(false);
  errorMsg = signal<string | null>(null);
  successMsg = signal<string | null>(null);

  user = signal<UserProfile | null>(null);

  totalDocumentos = signal(0);
  totalEnvios = signal(0);
  alertasNaoLidos = signal(0);
  diasRegistado = computed(() => {
    const data = this.user()?.dataCriacao;
    if (!data) return 0;
    const criacao = new Date(data);
    const hoje = new Date();
    const diff = hoje.getTime() - criacao.getTime();
    return Math.floor(diff / (1000 * 60 * 60 * 24));
  });

  prefEmailNotifications = true;
  prefDarkMode = false;
  prefLanguage = 'pt';

  showEditProfileModal = signal(false);
  showChangePasswordModal = signal(false);

  editForm = {
    nome: '',
    departamento: '',
    cargo: '',
    telefone: ''
  };

  passwordForm = {
    currentPassword: '',
    newPassword: '',
    confirmPassword: ''
  };

  passwordError = signal<string | null>(null);

  ngOnInit(): void {
    this.carregarDados();
    this.carregarPreferencias();
  }

  carregarDados(): void {
    this.isLoading.set(true);
    this.errorMsg.set(null);

    this.userService.getMe().subscribe({
      next: (data) => {
        this.user.set(data);
        this.editForm = {
          nome: data.nome,
          departamento: data.departamento || '',
          cargo: data.cargo || '',
          telefone: data.telefone || ''
        };
        this.isLoading.set(false);
      },
      error: (err) => {
        this.errorMsg.set(err.error?.message || 'Erro ao carregar perfil');
        this.isLoading.set(false);
      }
    });

    this.totalEnvios.set(0);

    this.userService.getAlertas().subscribe({
      next: (alertas) => this.alertasNaoLidos.set(alertas.filter(a => !a.lido).length),
      error: () => this.alertasNaoLidos.set(0)
    });
  }

  carregarPreferencias(): void {
    const saved = localStorage.getItem('user_preferences');
    if (saved) {
      try {
        const prefs = JSON.parse(saved);
        this.prefEmailNotifications = prefs.emailNotifications ?? true;
        this.prefDarkMode = prefs.darkMode ?? false;
        this.prefLanguage = prefs.language ?? 'pt';
        this.aplicarTema();
      } catch (e) {
        console.error('Erro ao carregar preferências', e);
      }
    }
  }

  salvarPreferencias(): void {
    const prefs = {
      emailNotifications: this.prefEmailNotifications,
      darkMode: this.prefDarkMode,
      language: this.prefLanguage
    };
    localStorage.setItem('user_preferences', JSON.stringify(prefs));
    this.aplicarTema();
    this.showToast('Preferências salvas');
  }

  toggleDarkMode(): void {
    this.aplicarTema();
    this.salvarPreferencias();
  }

  aplicarTema(): void {
    if (this.prefDarkMode) {
      document.body.classList.add('dark-mode');
      this.injetarEstilosDarkMode();
    } else {
      document.body.classList.remove('dark-mode');
      this.removerEstilosDarkMode();
    }
  }

  private injetarEstilosDarkMode(): void {
    if (document.getElementById('dark-mode-styles')) return;

    const style = document.createElement('style');
    style.id = 'dark-mode-styles';
    style.textContent = `
      body.dark-mode {
        background-color: #0a0a0a !important;
        color: #e5e5e5 !important;
      }
      body.dark-mode .sidebar {
        background: linear-gradient(180deg, #1a1a1a 0%, #0f0f0f 100%) !important;
        border-right: 1px solid #2a2a2a !important;
      }
      body.dark-mode .card,
      body.dark-mode .settings-card,
      body.dark-mode .page-header {
        background-color: #1a1a1a !important;
        border-color: #2a2a2a !important;
        color: #e5e5e5 !important;
      }
      body.dark-mode .form-group input,
      body.dark-mode .form-group select {
        background-color: #262626 !important;
        border-color: #404040 !important;
        color: #e5e5e5 !important;
      }
      body.dark-mode .data-table {
        background-color: #1a1a1a !important;
      }
      body.dark-mode .data-table th {
        background-color: #262626 !important;
        color: #ffffff !important;
      }
      body.dark-mode .data-table td {
        border-color: #2a2a2a !important;
        color: #e5e5e5 !important;
      }
      body.dark-mode .btn-secondary {
        background-color: #262626 !important;
        border-color: #404040 !important;
        color: #e5e5e5 !important;
      }
    `;
    document.head.appendChild(style);
  }

  private removerEstilosDarkMode(): void {
    const style = document.getElementById('dark-mode-styles');
    if (style) style.remove();
  }

  editarPerfil(): void {
    this.showEditProfileModal.set(true);
  }

  fecharModalPerfil(): void {
    this.showEditProfileModal.set(false);
  }

  fecharModalSenha(): void {
    this.showChangePasswordModal.set(false);
  }

  salvarPerfil(): void {
    if (!this.editForm.nome.trim()) {
      this.errorMsg.set('Nome é obrigatório.');
      return;
    }

    this.isSaving.set(true);
    this.errorMsg.set(null);

    this.userService.updateMe({
      nome: this.editForm.nome,
      departamento: this.editForm.departamento || null,
      cargo: this.editForm.cargo || null,
      telefone: this.editForm.telefone || null
    }).subscribe({
      next: (updatedUser) => {
        this.user.set(updatedUser);
        this.isSaving.set(false);
        this.fecharModalPerfil();
        this.showToast('Perfil atualizado com sucesso.');
      },
      error: (err) => {
        this.errorMsg.set(err.error?.message || 'Erro ao atualizar o perfil.');
        this.isSaving.set(false);
      }
    });
  }

  abrirModalAlterarSenha(): void {
    this.passwordForm = { currentPassword: '', newPassword: '', confirmPassword: '' };
    this.passwordError.set(null);
    this.showChangePasswordModal.set(true);
  }



  alterarSenha(): void {
    if (!this.passwordForm.currentPassword) {
      this.passwordError.set('A senha atual é obrigatória.');
      return;
    }
    if (this.passwordForm.newPassword.length < 6) {
      this.passwordError.set('A nova senha deve ter pelo menos 6 caracteres.');
      return;
    }
    if (this.passwordForm.newPassword !== this.passwordForm.confirmPassword) {
      this.passwordError.set('As senhas não coincidem.');
      return;
    }

    this.isSaving.set(true);
    this.passwordError.set(null);

    this.userService.changePassword({
      currentPassword: this.passwordForm.currentPassword,
      newPassword: this.passwordForm.newPassword
    }).subscribe({
      next: () => {
        this.isSaving.set(false);
        this.fecharModalSenha();
        this.showToast('Senha alterada com sucesso.');
      },
      error: (err) => {
        this.passwordError.set(err.error?.message || 'Erro ao alterar a senha.');
        this.isSaving.set(false);
      }
    });
  }

  logout(): void {
    this.authService.logout();
  }

  getAvatarUrl(): string {
    const nome = this.user()?.nome || 'User';
    return `https://ui-avatars.com/api/?background=f59e0b&color=fff&bold=true&name=${encodeURIComponent(nome)}`;
  }

  formatarData(data: string): string {
    if (!data) return 'Indefinido';
    const locale = this.prefLanguage === 'en' ? 'en-US' : 'pt-PT';
    return new Date(data).toLocaleDateString(locale, {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit'
    });
  }

  showToast(message: string): void {
    this.successMsg.set(message);
    setTimeout(() => this.successMsg.set(null), 3000);
  }

  clearError(): void {
    this.errorMsg.set(null);
  }
}