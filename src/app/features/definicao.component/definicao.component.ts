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
    this.showToast('Preferências guardadas com sucesso');
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
      background-color: #0f172a !important;
    }
    body.dark-mode .card,
    body.dark-mode .settings-card,
    body.dark-mode .page-header,
    body.dark-mode .filters-card,
    body.dark-mode .wms-header,
    body.dark-mode .env-header,
    body.dark-mode .modal-content {
      background-color: #1e293b !important;
      border-color: #334155 !important;
    }
    body.dark-mode .card-header h3,
    body.dark-mode .settings-card h3,
    body.dark-mode h1,
    body.dark-mode h2,
    body.dark-mode h3,
    body.dark-mode h4 {
      color: #f1f5f9 !important;
    }
    body.dark-mode .info-item p,
    body.dark-mode .stat-value,
    body.dark-mode .kpi-value {
      color: #f1f5f9 !important;
    }
    body.dark-mode .info-item label,
    body.dark-mode .stat-label,
    body.dark-mode .kpi-label,
    body.dark-mode .filter-group label {
      color: #94a3b8 !important;
    }
    body.dark-mode .form-group input,
    body.dark-mode .form-group select,
    body.dark-mode .filter-group input,
    body.dark-mode .filter-group select {
      background-color: #334155 !important;
      border-color: #475569 !important;
      color: #f1f5f9 !important;
    }
    body.dark-mode .data-table th {
      background-color: #334155 !important;
      color: #cbd5e1 !important;
    }
    body.dark-mode .data-table td {
      border-color: #334155 !important;
      color: #cbd5e1 !important;
    }
    body.dark-mode .data-table tr:hover td {
      background-color: #334155 !important;
    }
    body.dark-mode .stat-item {
      background-color: #334155 !important;
    }
    body.dark-mode .btn-secondary,
    body.dark-mode .btn-outline {
      background-color: #334155 !important;
      border-color: #475569 !important;
      color: #cbd5e1 !important;
    }
    body.dark-mode .btn-edit {
      background-color: #334155 !important;
      border-color: #475569 !important;
      color: #cbd5e1 !important;
    }
    body.dark-mode .modal-footer {
      background-color: #1e293b !important;
      border-color: #334155 !important;
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

  salvarPerfil(): void {
    if (!this.editForm.nome.trim()) {
      this.errorMsg.set('Nome é obrigatório');
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
        this.showToast('Perfil atualizado com sucesso');
      },
      error: (err) => {
        this.errorMsg.set(err.error?.message || 'Erro ao atualizar perfil');
        this.isSaving.set(false);
      }
    });
  }

  abrirModalAlterarSenha(): void {
    this.passwordForm = { currentPassword: '', newPassword: '', confirmPassword: '' };
    this.passwordError.set(null);
    this.showChangePasswordModal.set(true);
  }

  fecharModalSenha(): void {
    this.showChangePasswordModal.set(false);
  }

  alterarSenha(): void {
    if (!this.passwordForm.currentPassword) {
      this.passwordError.set('A palavra-passe actual é obrigatória');
      return;
    }
    if (this.passwordForm.newPassword.length < 6) {
      this.passwordError.set('A nova palavra-passe deve ter pelo menos 6 caracteres');
      return;
    }
    if (this.passwordForm.newPassword !== this.passwordForm.confirmPassword) {
      this.passwordError.set('As palavras-passe não coincidem');
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
        this.showToast('Palavra-passe alterada com sucesso');
      },
      error: (err) => {
        this.passwordError.set(err.error?.message || 'Erro ao alterar palavra-passe');
        this.isSaving.set(false);
      }
    });
  }

  uploadAvatar(): void {
    const input = document.querySelector('#avatarInput') as HTMLInputElement;
    input?.click();
  }

  onAvatarSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (input.files && input.files.length > 0) {
      const file = input.files[0];
      this.isSaving.set(true);
      this.userService.uploadAvatar(file).subscribe({
        next: (res) => {
          this.user.update(u => u ? { ...u, avatarUrl: res.avatarUrl } : null);
          this.isSaving.set(false);
          this.showToast('Foto actualizada com sucesso');
        },
        error: (err) => {
          this.errorMsg.set(err.error?.message || 'Erro ao fazer upload da foto');
          this.isSaving.set(false);
        }
      });
    }
  }

  logout(): void {
    this.authService.logout();
  }

  getAvatarUrl(): string {
    const nome = this.user()?.nome || 'Utilizador';
    return `https://ui-avatars.com/api/?background=2563eb&color=fff&bold=true&name=${encodeURIComponent(nome)}`;
  }

  formatarData(data: string): string {
    if (!data) return '—';
    return new Date(data).toLocaleDateString('pt-PT', {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit'
    });
  }

  showToast(msg: string): void {
    this.successMsg.set(msg);
    setTimeout(() => this.successMsg.set(null), 3000);
  }

  clearError(): void {
    this.errorMsg.set(null);
  }
}