import { Component, signal, computed, inject } from '@angular/core';
import { Router } from '@angular/router';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { AuthService } from '../../core/services/auth.service';

type AppMode = 'login' | 'signup' | 'forgot';
interface ToastState   { visible: boolean; message: string; }
interface MessageState { visible: boolean; text: string; type: 'error' | 'success' | ''; }

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './login.component.html',
  styleUrls: ['./login.component.css'],
})
export class LoginComponent {
  private auth   = inject(AuthService);
  private router = inject(Router);

  mode            = signal<AppMode>('login');
  name            = signal('');
  email           = signal('');
  password        = signal('');
  confirmPassword = signal('');
  rememberMe      = signal(false);
  showPassword    = signal(false);
  isLoading       = signal(false);

  toast   = signal<ToastState>  ({ visible: false, message: '' });
  message = signal<MessageState>({ visible: false, text: '', type: '' });
  private toastTimer: ReturnType<typeof setTimeout> | null = null;

  formTitle = computed(() =>
    ({ login: 'Bem-vindo', signup: 'Criar conta', forgot: 'Recuperar senha' }[this.mode()]));

  formSubtitle = computed(() =>
    ({
      login:  'Faça login para acessar sua conta',
      signup: 'Preencha os dados para criar sua conta',
      forgot: 'Informe seu e-mail para receber as instruções',
    }[this.mode()]));

  actionText = computed(() =>
    ({ login: 'Entrar', signup: 'Criar conta', forgot: 'Enviar instruções' }[this.mode()]));

  isLogin  = computed(() => this.mode() === 'login');
  isSignup = computed(() => this.mode() === 'signup');
  isForgot = computed(() => this.mode() === 'forgot');

  passwordInputType  = computed(() => this.showPassword() ? 'text' : 'password');
  togglePasswordIcon = computed(() => this.showPassword() ? 'la-eye-slash' : 'la-eye');

  switchMode(target: AppMode): void {
    this.mode.set(target);
    this.clearMessage();
    this.resetFields();
  }

  togglePasswordVisibility(): void {
    this.showPassword.update(v => !v);
  }

  onSubmit(): void {
    this.clearMessage();
    if (this.isLogin())  { this.handleLogin();  return; }
    if (this.isSignup()) { this.handleSignup(); return; }
    if (this.isForgot()) { this.handleForgot(); return; }
  }

  private handleLogin(): void {
    const email    = this.email().trim();
    const password = this.password();

    if (!email || !password) { this.showMessage('Preencha todos os campos.', 'error'); return; }
    if (!this.isValidEmail(email)) { this.showMessage('E-mail inválido.', 'error'); return; }

    this.isLoading.set(true);

    this.auth.login({ email, senha: password }).subscribe({
      next: () => this.router.navigate(['/dashboard']),
      error: (err: HttpErrorResponse) => {
        this.isLoading.set(false);
        const msg =
          err.status === 401 ? 'Email ou senha inválidos.' :
          err.status === 403 ? 'Conta desativada. Contacte o administrador.' :
          err.error?.message ?? 'Erro ao ligar ao servidor.';
        this.showMessage(msg, 'error');
      },
    });
  }

  private handleSignup(): void {
    const nome  = this.name().trim();
    const email = this.email().trim();
    const senha = this.password();
    const confirm = this.confirmPassword();

    if (!nome || !email || !senha || !confirm) {
      this.showMessage('Preencha todos os campos.', 'error'); return;
    }
    if (!this.isValidEmail(email)) {
      this.showMessage('E-mail inválido.', 'error'); return;
    }
    if (senha.length < 8) {
      this.showMessage('Senha com mínimo de 8 caracteres.', 'error'); return;
    }
    if (senha !== confirm) {
      this.showMessage('As senhas não coincidem.', 'error'); return;
    }

    this.isLoading.set(true);

    this.auth.register({ nome, email, senha }).subscribe({
      next: () => {
        this.isLoading.set(false);
        this.showMessage('Conta criada com sucesso! Redirecionando para o login...', 'success');
        setTimeout(() => this.switchMode('login'), 1800);
      },
      error: (err: HttpErrorResponse) => {
        this.isLoading.set(false);
        const msg =
          err.status === 409 ? 'E-mail já registado.' :
          err.error?.message ?? 'Erro ao criar conta.';
        this.showMessage(msg, 'error');
      },
    });
  }

  private handleForgot(): void {
    const email = this.email().trim();
    if (!email)                   { this.showMessage('Informe seu e-mail.', 'error'); return; }
    if (!this.isValidEmail(email)) { this.showMessage('E-mail inválido.', 'error'); return; }
    this.showMessage('Instruções enviadas! (simulação)', 'success');
  }

  private isValidEmail(e: string): boolean { return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(e); }

  private showMessage(text: string, type: 'error' | 'success'): void {
    this.message.set({ visible: true, text, type });
  }
  private clearMessage(): void { this.message.set({ visible: false, text: '', type: '' }); }

  private resetFields(): void {
    this.name.set('');
    this.email.set('');
    this.password.set('');
    this.confirmPassword.set('');
    this.rememberMe.set(false);
    this.showPassword.set(false);
  }

  
}
