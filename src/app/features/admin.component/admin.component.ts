import { Component, signal, computed, inject, OnInit, OnDestroy } from '@angular/core';
import { CommonModule }               from '@angular/common';
import { FormsModule }                from '@angular/forms';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Router }                     from '@angular/router';
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

export interface AdminStatsDto {
  totalUsuariosAtivos:   number;
  totalUsuariosInativos: number;
  totalAlertas:          number;
  alertasNaoLidos:       number;
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

export interface NovoUsuarioForm {
  nome:         string;
  email:        string;
  senha:        string;
  departamento: string;
  cargo:        string;
  telefone:     string;
  role:         'admin' | 'user';
}

type ActiveTab    = 'users' | 'audit';
type RoleFilter   = 'all' | 'admin' | 'user';


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

  private toastTimer: ReturnType<typeof setTimeout> | null = null;


  isLoadingStats = signal(true);
  isLoadingUsers = signal(true);
  isLoadingAudit = signal(false);
  isSaving       = signal(false);
  errorMessage   = signal<string | null>(null);
  toastMessage   = signal<string | null>(null);


  activeTab = signal<ActiveTab>('users');


  stats = signal<AdminStatsDto | null>(null);

  users       = signal<UserDto[]>([]);
  searchQuery = signal('');
  roleFilter  = signal<RoleFilter>('all');

  filteredUsers = computed(() => {
    const q    = this.searchQuery().trim().toLowerCase();
    const role = this.roleFilter();
    return this.users().filter(u => {
      const matchSearch = !q ||
        u.nome.toLowerCase().includes(q) ||
        u.email.toLowerCase().includes(q);
      const matchRole   = role === 'all' || u.role === role;
      return matchSearch && matchRole;
    });
  });


  auditLogs         = signal<AuditLogDto[]>([]);
  auditPage         = signal(1);
  auditTotal        = signal(0);
  readonly auditPerPage = 15;

  auditTotalPages  = computed(() => Math.ceil(this.auditTotal() / this.auditPerPage));
  auditPageNumbers = computed(() =>
    Array.from({ length: Math.min(this.auditTotalPages(), 5) }, (_, i) => i + 1)
  );


  showModal    = signal(false);
  showPassword = signal(false);
  modalForm    = signal<NovoUsuarioForm>(this.emptyForm());


  showDeleteModal = signal(false);
  userToDelete    = signal<UserDto | null>(null);


  ngOnInit(): void {
    if (!this.auth.isAdmin()) {
      this.router.navigate(['/dashboard']);
      return;
    }

    this.loadStats();
    this.loadUsers();
  }

  ngOnDestroy(): void {
    if (this.toastTimer) clearTimeout(this.toastTimer);
  }


  setTab(tab: ActiveTab): void {
    this.activeTab.set(tab);
    if (tab === 'audit' && this.auditLogs().length === 0) {
      this.loadAudit();
    }
  }

  loadStats(): void {
    this.isLoadingStats.set(true);
    this.http.get<AdminStatsDto>(`${this.api}/admin/stats`).subscribe({
      next:  s  => { this.stats.set(s); this.isLoadingStats.set(false); },
      error: () => this.isLoadingStats.set(false),
    });
  }

  loadUsers(): void {
    this.isLoadingUsers.set(true);
    this.http.get<UserDto[]>(`${this.api}/admin/users`).subscribe({
      next:  users => { this.users.set(users); this.isLoadingUsers.set(false); },
      error: err   => { this.handleError(err); this.isLoadingUsers.set(false); },
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
      error: err => { this.handleError(err); this.isLoadingAudit.set(false); },
    });
  }

  toggleUser(user: UserDto): void {
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
        this.showToast(
          `Conta ${res.novoStatus === 'ativo' ? 'ativada' : 'desativada'} com sucesso`
        );
      },
      error: err => this.handleError(err),
    });
  }

  openModal(): void {
    this.modalForm.set(this.emptyForm());
    this.errorMessage.set(null);
    this.showModal.set(true);
  }

  closeModal(): void {
    this.showModal.set(false);
    this.errorMessage.set(null);
  }

  closeModalOnBackdrop(e: MouseEvent): void {
    if ((e.target as HTMLElement).classList.contains('modal-backdrop')) {
      this.closeModal();
    }
  }

  saveUser(): void {
    const f = this.modalForm();

    if (!f.nome.trim() || !f.email.trim() || !f.senha.trim()) {
      this.errorMessage.set('Os campos Nome, Email e Senha são obrigatórios.');
      return;
    }

    this.isSaving.set(true);
    this.errorMessage.set(null);

    this.http.post<{ token: string; nome: string; role: string; userId: number }>(
      `${this.api}/auth/register`,
      {
        nome:         f.nome.trim(),
        email:        f.email.trim(),
        senha:        f.senha,
        departamento: f.departamento.trim() || undefined,
        cargo:        f.cargo.trim()        || undefined,
        telefone:     f.telefone.trim()     || undefined,
      }
    ).subscribe({
      next: () => {
        this.isSaving.set(false);
        this.closeModal();
        this.loadUsers();
        this.loadStats();
        this.showToast('Utilizador criado com sucesso!');
      },
      error: err => {
        this.isSaving.set(false);
        this.handleError(err);
      },
    });
  }

  confirmDelete(user: UserDto): void {
    this.userToDelete.set(user);
    this.showDeleteModal.set(true);
  }

  cancelDelete(): void {
    this.showDeleteModal.set(false);
    this.userToDelete.set(null);
  }

  executeDelete(): void {
    const user = this.userToDelete();
    if (!user) return;
    this.toggleUser(user);
    this.cancelDelete();
  }

  toggleShowPassword(): void {
    this.showPassword.update(v => !v);
  }

  setSearchQuery(value: string): void {
    this.searchQuery.set(value);
  }

  setRoleFilter(value: string): void {
    const safe = (['all', 'admin', 'user'].includes(value) ? value : 'all') as RoleFilter;
    this.roleFilter.set(safe);
  }

  formatDate(iso: string | null): string {
    if (!iso) return '—';
    return new Date(iso).toLocaleDateString('pt-PT', {
      day: '2-digit', month: '2-digit', year: 'numeric',
    });
  }

  formatDateTime(iso: string): string {
    return new Date(iso).toLocaleString('pt-PT', {
      day: '2-digit', month: '2-digit', year: 'numeric',
      hour: '2-digit', minute: '2-digit',
    });
  }

  private emptyForm(): NovoUsuarioForm {
    return {
      nome: '', email: '', senha: '',
      departamento: '', cargo: '', telefone: '',
      role: 'user',
    };
  }

  private showToast(msg: string): void {
    if (this.toastTimer) clearTimeout(this.toastTimer);
    this.toastMessage.set(msg);
    this.toastTimer = setTimeout(() => this.toastMessage.set(null), 3500);
  }

  private handleError(err: HttpErrorResponse): void {
    let msg: string;

    switch (err.status) {
      case 401:
        msg = 'Sessão expirada. Por favor, faça login novamente.';
        this.auth.logout();
        break;
      case 403:
        msg = 'Não tem permissão para realizar esta operação.';
        break;
      case 409:
        msg = err.error?.message ?? 'Já existe um registo com esses dados.';
        break;
      case 0:
        msg = 'Sem ligação ao servidor. Verifique a sua rede.';
        break;
      default:
        msg = err.error?.message ?? 'Ocorreu um erro. Tente novamente.';
    }

    this.errorMessage.set(msg);
    setTimeout(() => this.errorMessage.set(null), 6000);
  }
}
