import { Component, signal, computed, inject, OnInit, OnDestroy } from '@angular/core';
import { CommonModule }               from '@angular/common';
import { FormsModule }                from '@angular/forms';
import { HttpClient, HttpErrorResponse, HttpHeaders } from '@angular/common/http';
import { Router, NavigationEnd }      from '@angular/router';
import { filter }                     from 'rxjs/operators';
import { AuthService }                from '../../core/services/auth.service';
import { environment }                from '../../../environments/environment';


export interface UserDto {
  id:           number;
  nome:         string;
  email:        string;
  role:         'admin' | 'user';
  status:       'ativo' | 'inativo';
  departamento: string | null;
  cargo:        string | null;
  telefone:     string | null;
  avatarUrl:    string | null;
  dataCriacao:  string;
  ultimoLogin:  string | null;
}

export interface AuditLogDto {
  id:        number;
  adminId:   number;
  nomeAdmin: string;
  acao:      string;
  detalhe:   string | null;
  ipAddress: string | null;
  timestamp: string;
}

export interface LoginRecente {
  id: number;
  usuarioNome: string;
  usuarioEmail: string;
  timestamp: string;
  ip: string;
}

export interface AcaoFrequente {
  acao: string;
  quantidade: number;
  percentagem: number;
}

export interface AtividadeRecente {
  id: number;
  usuarioNome: string;
  usuarioEmail: string;
  acao: string;
  entidade: string;
  detalhe: string;
  timestamp: string;
}

export interface SessaoAtiva {
  id: string;
  usuarioId: number;
  usuarioNome: string;
  tokenExpiracao: string;
  ultimaAtividade: string;
  ip: string;
  userAgent: string;
}

type AdminTab = 'atividades' | 'audit' | 'users';

@Component({
  selector: 'app-admin',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './admin.component.html',
  styleUrls: ['./admin.component.css'],
})
export class AdminComponent implements OnInit, OnDestroy {
  private http   = inject(HttpClient);
  readonly auth  = inject(AuthService);
  private router = inject(Router);
  private api    = environment.apiUrl;

  isLoadingStats = signal(true);
  isLoadingUsers = signal(false);
  isLoadingAudit = signal(false);
  isLoadingAtividades = signal(false);
  isLoadingSessoes = signal(false);
  isSaving       = signal(false);
  errorMessage   = signal<string | null>(null);
  toastMessage   = signal<string | null>(null);
  
  activeAdminTab = signal<AdminTab>('atividades');
  
  loginsRecentes = signal<LoginRecente[]>([]);
  acoesFrequentes = signal<AcaoFrequente[]>([]);
  atividadesRecentes = signal<AtividadeRecente[]>([]);
  

  auditLogs = signal<AuditLogDto[]>([]);
  auditPage = signal(1);
  auditTotal = signal(0);
  readonly auditPerPage = 15;
  

  users = signal<UserDto[]>([]);
  searchQuery = signal('');
  roleFilter = signal<'all' | 'admin' | 'user'>('all');
  
  sessoesAtivas = signal<SessaoAtiva[]>([]);
  
  showUserModal = signal(false);
  showDeleteModal = signal(false);
  userToDelete = signal<UserDto | null>(null);
  modalForm = signal({
    nome: '',
    email: '',
    senha: '',
    departamento: '',
    cargo: '',
    telefone: ''
  });
  
  filteredUsers = computed(() => {
    const q = this.searchQuery().trim().toLowerCase();
    const role = this.roleFilter();
    return this.users().filter(u => {
      const matchSearch = !q || u.nome.toLowerCase().includes(q) || u.email.toLowerCase().includes(q);
      const matchRole = role === 'all' || u.role === role;
      return matchSearch && matchRole;
    });
  });
  
  auditTotalPages = computed(() => Math.ceil(this.auditTotal() / this.auditPerPage));
  
  ngOnInit(): void {
    console.log('[AdminComponent] Inicializando, isAdmin:', this.auth.isAdmin());
    
    if (!this.auth.isAdmin()) {
      console.log('[AdminComponent] Usuário não é admin, redirecionando...');
      this.router.navigate(['/dashboard']);
      return;
    }

    this.syncTabWithUrl();
    this.router.events.pipe(filter(event => event instanceof NavigationEnd)).subscribe(() => this.syncTabWithUrl());
  }
  
  ngOnDestroy(): void {
  }
  
  setAdminTab(tab: AdminTab): void {
    this.activeAdminTab.set(tab);
    const route = tab === 'audit' ? '/admin/audit' : tab === 'users' ? '/admin/users' : '/admin/atividades';
    this.router.navigate([route]);

    if (tab === 'audit' && this.auditLogs().length === 0) {
      this.loadAudit();
    }
    if (tab === 'users' && this.sessoesAtivas().length === 0) {
      this.loadSessoes();
    }
    if (tab === 'atividades') {
      this.loadAtividades();
    }
  }

  private syncTabWithUrl(): void {
    const path = this.router.url.split('?')[0];
    if (path.endsWith('/audit')) {
      this.activeAdminTab.set('audit');
      if (this.auditLogs().length === 0) this.loadAudit();
    } else if (path.endsWith('/users')) {
      this.activeAdminTab.set('users');
      if (this.sessoesAtivas().length === 0) this.loadSessoes();
    } else {
      this.activeAdminTab.set('atividades');
      this.loadAtividades();
    }
  }
  
  loadAtividades(): void {
    this.isLoadingAtividades.set(true);
    
    this.http.get<LoginRecente[]>(`${this.api}/admin/atividades/logins-recentes`).subscribe({
      next: (data) => {
        this.loginsRecentes.set(data.slice(0, 10));
      },
      error: (err) => console.error('Erro ao carregar logins recentes:', err)
    });
    
    this.http.get<AcaoFrequente[]>(`${this.api}/admin/atividades/acoes-frequentes`).subscribe({
      next: (data) => {
        this.acoesFrequentes.set(data);
      },
      error: (err) => console.error('Erro ao carregar ações frequentes:', err)
    });
    
    this.http.get<AtividadeRecente[]>(`${this.api}/admin/atividades/atividade-recente`).subscribe({
      next: (data) => {
        this.atividadesRecentes.set(data.slice(0, 20));
        this.isLoadingAtividades.set(false);
      },
      error: (err) => {
        console.error('Erro ao carregar atividade recente:', err);
        this.isLoadingAtividades.set(false);
      }
    });
  }
  
  loadAudit(page = 1): void {
    this.isLoadingAudit.set(true);
    this.http.get<{ total: number; data: AuditLogDto[] }>(
      `${this.api}/admin/audit?page=${page}&perPage=${this.auditPerPage}`
    ).subscribe({
      next: res => {
        this.auditLogs.set(res.data);
        this.auditTotal.set(res.total);
        this.auditPage.set(page);
        this.isLoadingAudit.set(false);
      },
      error: err => {
        this.handleError(err);
        this.isLoadingAudit.set(false);
      }
    });
  }
  
  loadUsers(): void {
    this.isLoadingUsers.set(true);
    this.http.get<UserDto[]>(`${this.api}/admin/users`).subscribe({
      next: users => {
        this.users.set(users);
        this.isLoadingUsers.set(false);
      },
      error: err => {
        this.handleError(err);
        this.isLoadingUsers.set(false);
      }
    });
  }
  
  loadSessoes(): void {
    this.isLoadingSessoes.set(true);
    this.http.get<SessaoAtiva[]>(`${this.api}/admin/sessoes`).subscribe({
      next: sessoes => {
        this.sessoesAtivas.set(sessoes);
        this.isLoadingSessoes.set(false);
      },
      error: err => {
        console.error('Erro ao carregar sessões:', err);
        this.isLoadingSessoes.set(false);
      }
    });
  }
  
  toggleUser(user: UserDto): void {
    if (user.id === this.auth.user()?.userId) {
      this.errorMessage.set('Não pode alterar o estado da sua própria conta.');
      setTimeout(() => this.errorMessage.set(null), 3000);
      return;
    }
    
    this.isSaving.set(true);
    this.http.post<{ userId: number; novoStatus: string }>(
      `${this.api}/admin/users/toggle`,
      { userId: user.id }
    ).subscribe({
      next: res => {
        this.users.update(list =>
          list.map(u => u.id === res.userId
            ? { ...u, status: res.novoStatus as 'ativo' | 'inativo' }
            : u)
        );
        this.showToast(`Conta ${res.novoStatus === 'ativo' ? 'ativada' : 'desativada'} com sucesso`);
        this.isSaving.set(false);
      },
      error: err => {
        this.handleError(err);
        this.isSaving.set(false);
      }
    });
  }
  
  deleteUser(userId: number): void {
    this.isSaving.set(true);
    this.http.delete(`${this.api}/admin/users/${userId}`).subscribe({
      next: () => {
        this.users.update(list => list.filter(u => u.id !== userId));
        this.showToast('Utilizador removido com sucesso');
        this.isSaving.set(false);
        this.showDeleteModal.set(false);
        this.userToDelete.set(null);
      },
      error: err => {
        this.handleError(err);
        this.isSaving.set(false);
      }
    });
  }
  
  terminateSession(sessionId: string, usuarioNome: string): void {
    if (confirm(`Tem certeza que deseja terminar a sessão de ${usuarioNome}?`)) {
      this.http.post(`${this.api}/admin/sessoes/${sessionId}/terminar`, {}).subscribe({
        next: () => {
          this.sessoesAtivas.update(list => list.filter(s => s.id !== sessionId));
          this.showToast(`Sessão de ${usuarioNome} terminada com sucesso`);
        },
        error: err => {
          this.handleError(err);
        }
      });
    }
  }
  
  criarUtilizador(): void {
    const form = this.modalForm();
    
    if (!form.nome.trim() || !form.email.trim() || !form.senha.trim()) {
      this.errorMessage.set('Nome, Email e Senha são obrigatórios.');
      return;
    }
    
    if (form.senha.length < 6) {
      this.errorMessage.set('A senha deve ter pelo menos 6 caracteres.');
      return;
    }
    
    this.isSaving.set(true);
    this.http.post(`${this.api}/auth/register`, {
      nome: form.nome.trim(),
      email: form.email.trim(),
      senha: form.senha,
      departamento: form.departamento || undefined,
      cargo: form.cargo || undefined,
      telefone: form.telefone || undefined
    }).subscribe({
      next: () => {
        this.isSaving.set(false);
        this.showUserModal.set(false);
        this.loadUsers();
        this.showToast('Utilizador criado com sucesso!');
        this.modalForm.set({ nome: '', email: '', senha: '', departamento: '', cargo: '', telefone: '' });
      },
      error: err => {
        this.handleError(err);
        this.isSaving.set(false);
      }
    });
  }
  
  formatDateTime(iso: string): string {
    return new Date(iso).toLocaleString('pt-PT', {
      day: '2-digit', month: '2-digit', year: 'numeric',
      hour: '2-digit', minute: '2-digit'
    });
  }
  
  formatDate(iso: string | null): string {
    if (!iso) return '—';
    return new Date(iso).toLocaleDateString('pt-PT');
  }
  
  formatRelativeTime(iso: string): string {
    const date = new Date(iso);
    const now = new Date();
    const diffMs = now.getTime() - date.getTime();
    const diffMins = Math.floor(diffMs / 60000);
    const diffHours = Math.floor(diffMs / 3600000);
    const diffDays = Math.floor(diffMs / 86400000);
    
    if (diffMins < 1) return 'agora';
    if (diffMins < 60) return `${diffMins} min atrás`;
    if (diffHours < 24) return `${diffHours} h atrás`;
    return `${diffDays} dias atrás`;
  }
  
  private showToast(msg: string): void {
    this.toastMessage.set(msg);
    setTimeout(() => this.toastMessage.set(null), 3500);
  }
  
  private handleError(err: HttpErrorResponse): void {
    let msg: string;
    switch (err.status) {
      case 401:
        msg = 'Sessão expirada. Faça login novamente.';
        this.auth.logout();
        break;
      case 403:
        msg = 'Sem permissão para esta operação.';
        break;
      case 409:
        msg = err.error?.message ?? 'Registo já existe.';
        break;
      default:
        msg = err.error?.message ?? 'Erro ao processar requisição.';
    }
    this.errorMessage.set(msg);
    setTimeout(() => this.errorMessage.set(null), 5000);
  }
  
  setSearchQuery(value: string): void { this.searchQuery.set(value); }
  setRoleFilter(value: string): void { 
    const safe = (['all', 'admin', 'user'].includes(value) ? value : 'all') as 'all' | 'admin' | 'user';
    this.roleFilter.set(safe); 
  }
  openUserModal(): void { 
    this.modalForm.set({ nome: '', email: '', senha: '', departamento: '', cargo: '', telefone: '' });
    this.showUserModal.set(true); 
  }
  closeUserModal(): void { this.showUserModal.set(false); }
  confirmDelete(user: UserDto): void { this.userToDelete.set(user); this.showDeleteModal.set(true); }
  cancelDelete(): void { this.showDeleteModal.set(false); this.userToDelete.set(null); }
  executeDelete(): void { 
    const user = this.userToDelete();
    if (user) this.deleteUser(user.id);
  }
}