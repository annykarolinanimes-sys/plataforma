import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../core/services/auth.service';
import { UserService } from '../../core/services/user.service';
import { TranslationService } from '../../core/services/translation.service';
import { TranslatePipe } from '../../core/pipes/translate.pipe';


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
  imports: [CommonModule, FormsModule, TranslatePipe],
  templateUrl: './definicao.component.html',
  styleUrls: ['./definicao.component.css']
})
export class DefinicaoComponent implements OnInit {
  private authService = inject(AuthService);
  private userService = inject(UserService);
  private translationService = inject(TranslationService);
  

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
        this.translationService.setLanguage(this.prefLanguage as 'pt' | 'en');
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
    this.translationService.setLanguage(this.prefLanguage as 'pt' | 'en');
    this.aplicarTema();
    this.showToast('message.preferencesSaved');
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
    /* Background and main containers */
    body.dark-mode {
      background-color: #0a0a0a !important;
      color: #e5e5e5 !important;
    }

    /* Sidebar */
    body.dark-mode .sidebar {
      background: linear-gradient(180deg, #1a1a1a 0%, #0f0f0f 100%) !important;
      border-right: 1px solid #2a2a2a !important;
    }

    body.dark-mode .sidebar-footer {
      background: #0f0f0f !important;
      border-top: 1px solid #2a2a2a !important;
    }

    body.dark-mode .nav-section-header {
      color: #e5e5e5 !important;
      background: transparent !important;
    }

    body.dark-mode .nav-section-header:hover {
      background: rgba(255, 255, 255, 0.05) !important;
      color: #ffffff !important;
    }

    body.dark-mode .nav-item {
      color: #b3b3b3 !important;
    }

    body.dark-mode .nav-item:hover,
    body.dark-mode .nav-item.active {
      background: rgba(255, 255, 255, 0.08) !important;
      color: #ffffff !important;
    }

    body.dark-mode .nav-submenu {
      background: #1a1a1a !important;
    }

    body.dark-mode .floating-submenu {
      background: #1a1a1a !important;
      border: 1px solid #2a2a2a !important;
      box-shadow: 0 4px 20px rgba(0, 0, 0, 0.5) !important;
    }

    body.dark-mode .nav-subitem {
      color: #b3b3b3 !important;
    }

    body.dark-mode .nav-subitem:hover {
      background: rgba(255, 255, 255, 0.08) !important;
      color: #ffffff !important;
    }

    /* Navbar */
    body.dark-mode .navbar {
      background: linear-gradient(90deg, #1a1a1a 0%, #0f0f0f 100%) !important;
      border-bottom: 1px solid #2a2a2a !important;
      color: #e5e5e5 !important;
    }

    body.dark-mode .navbar .nav-link {
      color: #b3b3b3 !important;
    }

    body.dark-mode .navbar .nav-link:hover {
      color: #ffffff !important;
      background: rgba(255, 255, 255, 0.05) !important;
    }

    /* Footer */
    body.dark-mode .footer {
      background: #0f0f0f !important;
      border-top: 1px solid #2a2a2a !important;
      color: #888888 !important;
    }

    /* Cards and components */
    body.dark-mode .card,
    body.dark-mode .settings-card,
    body.dark-mode .page-header,
    body.dark-mode .filters-card,
    body.dark-mode .wms-header,
    body.dark-mode .env-header,
    body.dark-mode .modal-content {
      background-color: #1a1a1a !important;
      border-color: #2a2a2a !important;
      color: #e5e5e5 !important;
    }

    body.dark-mode .card-header h3,
    body.dark-mode .settings-card h3,
    body.dark-mode h1,
    body.dark-mode h2,
    body.dark-mode h3,
    body.dark-mode h4 {
      color: #ffffff !important;
    }

    body.dark-mode .info-item p,
    body.dark-mode .stat-value,
    body.dark-mode .kpi-value {
      color: #e5e5e5 !important;
    }

    body.dark-mode .info-item label,
    body.dark-mode .stat-label,
    body.dark-mode .kpi-label,
    body.dark-mode .filter-group label {
      color: #b3b3b3 !important;
    }

    /* Form elements */
    body.dark-mode .form-group input,
    body.dark-mode .form-group select,
    body.dark-mode .filter-group input,
    body.dark-mode .filter-group select {
      background-color: #262626 !important;
      border-color: #404040 !important;
      color: #e5e5e5 !important;
    }

    body.dark-mode .form-group input:focus,
    body.dark-mode .form-group select:focus {
      border-color: #666666 !important;
      box-shadow: 0 0 0 2px rgba(102, 102, 102, 0.2) !important;
    }

    /* Tables */
    body.dark-mode .data-table {
      background-color: #1a1a1a !important;
      border-color: #2a2a2a !important;
    }

    body.dark-mode .data-table th {
      background-color: #262626 !important;
      color: #ffffff !important;
      border-color: #404040 !important;
    }

    body.dark-mode .data-table td {
      border-color: #2a2a2a !important;
      color: #e5e5e5 !important;
    }

    body.dark-mode .data-table tr:hover td {
      background-color: #262626 !important;
    }

    /* Buttons */
    body.dark-mode .btn-secondary,
    body.dark-mode .btn-outline {
      background-color: #262626 !important;
      border-color: #404040 !important;
      color: #e5e5e5 !important;
    }

    body.dark-mode .btn-secondary:hover,
    body.dark-mode .btn-outline:hover {
      background-color: #404040 !important;
      border-color: #666666 !important;
      color: #ffffff !important;
    }

    body.dark-mode .btn-primary {
      background-color: #3b82f6 !important;
      border-color: #3b82f6 !important;
      color: #ffffff !important;
    }

    body.dark-mode .btn-primary:hover {
      background-color: #2563eb !important;
      border-color: #2563eb !important;
    }

    /* Modal */
    body.dark-mode .modal-overlay {
      background-color: rgba(0, 0, 0, 0.8) !important;
    }

    body.dark-mode .modal-footer {
      background-color: #1a1a1a !important;
      border-color: #2a2a2a !important;
    }

    /* Statistics */
    body.dark-mode .stat-item {
      background-color: #262626 !important;
      border: 1px solid #404040 !important;
    }

    /* Alerts */
    body.dark-mode .alert-error {
      background-color: rgba(239, 68, 68, 0.1) !important;
      border-color: #dc2626 !important;
      color: #fca5a5 !important;
    }

    body.dark-mode .alert-success {
      background-color: rgba(34, 197, 94, 0.1) !important;
      border-color: #16a34a !important;
      color: #86efac !important;
    }

    /* Switches */
    body.dark-mode .switch {
      background-color: #262626 !important;
      border-color: #404040 !important;
    }

    body.dark-mode .switch input:checked + span {
      background-color: #3b82f6 !important;
    }

    /* Links and text */
    body.dark-mode a {
      color: #60a5fa !important;
    }

    body.dark-mode a:hover {
      color: #93c5fd !important;
    }

    /* Scrollbar */
    body.dark-mode ::-webkit-scrollbar {
      width: 8px;
    }

    body.dark-mode ::-webkit-scrollbar-track {
      background: #1a1a1a;
    }

    body.dark-mode ::-webkit-scrollbar-thumb {
      background: #404040;
      border-radius: 4px;
    }

    body.dark-mode ::-webkit-scrollbar-thumb:hover {
      background: #666666;
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
      this.errorMsg.set(this.translationService.translate('settings.fullName') + ' ' + this.translationService.translate('common.required'));
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
        this.showToast('message.profileUpdated');
      },
      error: (err) => {
        this.errorMsg.set(err.error?.message || this.translationService.translate('common.errorUpdatingProfile'));
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
      this.passwordError.set(this.translationService.translate('modal.currentPassword') + ' ' + this.translationService.translate('common.required'));
      return;
    }
    if (this.passwordForm.newPassword.length < 6) {
      this.passwordError.set(this.translationService.translate('modal.newPassword') + ' ' + this.translationService.translate('common.mustHaveAtLeast6Chars'));
      return;
    }
    if (this.passwordForm.newPassword !== this.passwordForm.confirmPassword) {
      this.passwordError.set(this.translationService.translate('common.passwordsDontMatch'));
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
        this.showToast('message.passwordChanged');
      },
      error: (err) => {
        this.passwordError.set(err.error?.message || this.translationService.translate('common.errorChangingPassword'));
        this.isSaving.set(false);
      }
    });
  }

  logout(): void {
    this.authService.logout();
  }

  getAvatarUrl(): string {
    const nome = this.user()?.nome || 'Utilizador';
    return `https://ui-avatars.com/api/?background=2563eb&color=fff&bold=true&name=${encodeURIComponent(nome)}`;
  }

  formatarData(data: string): string {
    if (!data) return this.translationService.translate('common.undefined');
    const locale = this.translationService.getLanguage() === 'en' ? 'en-US' : 'pt-PT';
    return new Date(data).toLocaleDateString(locale, {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit'
    });
  }

  showToast(key: string): void {
    const message = this.translationService.translate(key as any);
    this.successMsg.set(message);
    setTimeout(() => this.successMsg.set(null), 3000);
  }

  clearError(): void {
    this.errorMsg.set(null);
  }
}