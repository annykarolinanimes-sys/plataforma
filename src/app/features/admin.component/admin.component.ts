import { Component, signal, computed, inject, OnInit, OnDestroy } from '@angular/core';
import { CommonModule }               from '@angular/common';
import { FormsModule }                from '@angular/forms';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Router, NavigationEnd }      from '@angular/router';
import { filter, debounceTime, distinctUntilChanged, Subject, takeUntil } from 'rxjs';
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

export interface AdminStats {
  totalAtivos:     number;
  totalInativos:   number;
  totalAlertas:    number;
  alertasNaoLidos: number;
  sessoesAtivas:   number;
  totalUsers:      number;
}

export interface LoginRecente {
  id:           number;
  usuarioNome:  string;
  usuarioEmail: string;
  timestamp:    string;
  ip:           string;
}

export interface AcaoFrequente {
  acao:        string;
  quantidade:  number;
  percentagem: number;
}

export interface AtividadeRecente {
  id:           number;
  usuarioNome:  string;
  usuarioEmail: string;
  acao:         string;
  entidade:     string;
  detalhe:      string;
  timestamp:    string;
}

export interface SessaoAtiva {
  id:              string;
  usuarioId:       number;
  usuarioNome:     string;
  usuarioEmail:    string;
  ip:              string;
  userAgent:       string;
  ultimaAtividade: string;
  dataCriacao:     string;
  dataExpiracao:   string;
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
  private http    = inject(HttpClient);
  readonly auth   = inject(AuthService);
  private router  = inject(Router);
  private api     = environment.apiUrl;
  private destroy$ = new Subject<void>();
  private searchInput$ = new Subject<string>();

  isLoadingStats    = signal(false);
  isLoadingUsers    = signal(false);
  isLoadingAudit    = signal(false);
  isLoadingAtividades = signal(false);
  isLoadingSessoes  = signal(false);
  isSaving          = signal(false);

  errorMessage  = signal<string | null>(null);
  toastMessage  = signal<string | null>(null);

  activeAdminTab = signal<AdminTab>('atividades');

  stats = signal<AdminStats | null>(null);

  loginsRecentes      = signal<LoginRecente[]>([]);
  acoesFrequentes     = signal<AcaoFrequente[]>([]);
  atividadesRecentes  = signal<AtividadeRecente[]>([]);

  auditLogs   = signal<AuditLogDto[]>([]);
  auditPage   = signal(1);
  auditTotal  = signal(0);
  auditSearch = signal('');
  readonly auditPerPage = 15;
  auditTotalPages = computed(() => Math.ceil(this.auditTotal() / this.auditPerPage));

  
  selectedLog = signal<AuditLogDto | null>(null);

  users       = signal<UserDto[]>([]);
  searchQuery = signal('');
  roleFilter  = signal<'all' | 'admin' | 'user'>('all');

  filteredUsers = computed(() => {
    const q    = this.searchQuery().trim().toLowerCase();
    const role = this.roleFilter();
    return this.users().filter(u => {
      const matchSearch = !q ||
        u.nome.toLowerCase().includes(q) ||
        u.email.toLowerCase().includes(q) ||
        (u.departamento ?? '').toLowerCase().includes(q);
      const matchRole = role === 'all' || u.role === role;
      return matchSearch && matchRole;
    });
  });

  usersAtivos   = computed(() => Array.isArray(this.users()) ? this.users().filter(u => u.status === 'ativo').length : 0);
  usersInativos = computed(() => Array.isArray(this.users()) ? this.users().filter(u => u.status === 'inativo').length : 0);
  usersAdmin    = computed(() => Array.isArray(this.users()) ? this.users().filter(u => u.role === 'admin').length : 0);

  showUserModal   = signal(false);
  showDeleteModal = signal(false);
  showLogModal    = signal(false);
  userToDelete    = signal<UserDto | null>(null);

  modalForm = signal({
    nome: '', email: '', senha: '',
    departamento: '', cargo: '', telefone: '',
  });

  sessoesAtivas = signal<SessaoAtiva[]>([]);


  ngOnInit(): void {
    if (!this.auth.isAdmin()) {
      this.router.navigate(['/dashboard']);
      return;
    }

    this.searchInput$
      .pipe(debounceTime(300), distinctUntilChanged(), takeUntil(this.destroy$))
      .subscribe(q => this.searchQuery.set(q));

    this.syncTabWithUrl();
    this.router.events
      .pipe(filter(e => e instanceof NavigationEnd), takeUntil(this.destroy$))
      .subscribe(() => this.syncTabWithUrl());
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }


  setAdminTab(tab: AdminTab): void {
    this.activeAdminTab.set(tab);
    const route = tab === 'audit'
      ? '/admin/audit'
      : tab === 'users'
        ? '/admin/users'
        : '/admin/atividades';
    this.router.navigate([route]);
    this.loadTabData(tab);
  }

  private syncTabWithUrl(): void {
    const path = this.router.url.split('?')[0];
    if      (path.endsWith('/audit')) this.activeAdminTab.set('audit');
    else if (path.endsWith('/users')) this.activeAdminTab.set('users');
    else                              this.activeAdminTab.set('atividades');

    this.loadTabData(this.activeAdminTab());
  }

  private loadTabData(tab: AdminTab): void {
    this.loadStats();
    if (tab === 'atividades')                     this.loadAtividades();
    if (tab === 'audit' && !this.auditLogs().length) this.loadAudit();
    if (tab === 'users') {
      if (!this.users().length) this.loadUsers();
      if (!this.sessoesAtivas().length) this.loadSessoes();
    }
  }


  loadStats(): void {
    this.http.get<AdminStats>(`${this.api}/admin/stats`).subscribe({
      next: s  => this.stats.set(s),
      error: () => {},
    });
  }


  loadAtividades(): void {
    this.isLoadingAtividades.set(true);

    let completedRequests = 0;
    const onAllComplete = () => {
      completedRequests++;
      if (completedRequests === 3) {
        this.isLoadingAtividades.set(false);
      }
    };

    this.http.get<LoginRecente[]>(`${this.api}/admin/atividades/logins-recentes?limite=10`).subscribe({
      next:  data => { this.loginsRecentes.set(data); onAllComplete(); },
      error: err  => { console.error('logins-recentes:', err); onAllComplete(); },
    });

    this.http.get<AcaoFrequente[]>(`${this.api}/admin/atividades/acoes-frequentes`).subscribe({
      next:  data => { this.acoesFrequentes.set(data); onAllComplete(); },
      error: err  => { console.error('acoes-frequentes:', err); onAllComplete(); },
    });

    this.http.get<AtividadeRecente[]>(`${this.api}/admin/atividades/atividade-recente?limite=20`).subscribe({
      next:  data => { this.atividadesRecentes.set(data); onAllComplete(); },
      error: err  => { console.error('atividade-recente:', err); onAllComplete(); },
    });
  }


  loadAudit(page = 1): void {
    this.isLoadingAudit.set(true);
    const search = this.auditSearch() ? `&search=${encodeURIComponent(this.auditSearch())}` : '';
    this.http.get<{ total: number; data: AuditLogDto[] }>(
      `${this.api}/admin/audit?page=${page}&perPage=${this.auditPerPage}${search}`
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
      },
    });
  }

  onAuditSearch(value: string): void {
    this.auditSearch.set(value);
    this.loadAudit(1);
  }

  openLogDetail(log: AuditLogDto): void {
    this.selectedLog.set(log);
    this.showLogModal.set(true);
  }

  formattedLogDetail(log: AuditLogDto): string {
    if (!log.detalhe) return '—';
    try {
      const parsed = JSON.parse(log.detalhe);
      return JSON.stringify(parsed, null, 2);
    } catch {
      return log.detalhe;
    }
  }

  getAuditSummary(log: AuditLogDto): string {
    if (!log.detalhe) return '—';

    try {
      const parsed = JSON.parse(log.detalhe);
      if (typeof parsed === 'string') return parsed;
      if (parsed && typeof parsed === 'object') {
        const payload = (parsed as any).payload;
        if (payload && typeof payload === 'object')
          return this.summarizeAuditPayload(payload);

        if ((parsed as any).routeValues && (parsed as any).routeValues.id)
          return `ID: ${(parsed as any).routeValues.id}`;

        return this.summarizeAuditPayload(parsed);
      }

      return '—';
    } catch {
      return log.detalhe;
    }
  }

  private summarizeAuditPayload(payload: any): string {
    if (payload == null) return '—';
    if (typeof payload === 'string' || typeof payload === 'number' || typeof payload === 'boolean')
      return String(payload);
    if (Array.isArray(payload))
      return payload.map(item => this.summarizeAuditPayload(item)).filter(Boolean).slice(0, 4).join('; ');

    const keys = [
      'nome', 'codigo', 'email', 'status', 'tipo', 'titulo', 'numero', 'matricula',
      'transportadora', 'clienteNome', 'fornecedor', 'veiculo', 'descricao', 'id', 'sessionId'
    ];

    const parts: string[] = [];
    for (const key of keys) {
      if (key in payload && payload[key] != null && payload[key] !== '') {
        parts.push(`${this.formatAuditKey(key)}: ${payload[key]}`);
      }
    }

    if (parts.length > 0)
      return parts.join('; ');

    const entries = Object.entries(payload)
      .filter(([, value]) => value != null && value !== '')
      .slice(0, 4)
      .map(([key, value]) => `${this.formatAuditKey(key)}: ${this.summarizeAuditPayload(value)}`);

    return entries.length > 0 ? entries.join('; ') : '—';
  }

  private formatAuditKey(key: string): string {
    return key
      .replace(/([A-Z])/g, ' $1')
      .replace(/_/g, ' ')
      .replace(/\b([a-z])/g, match => match.toUpperCase());
  }


  loadUsers(): void {
    this.isLoadingUsers.set(true);
    this.http.get<{ total: number; page: number; pageSize: number; data: UserDto[] }>(`${this.api}/admin/users`).subscribe({
      next:  res => { this.users.set(Array.isArray(res.data) ? res.data : []); this.isLoadingUsers.set(false); },
      error: err => { this.handleError(err); this.isLoadingUsers.set(false); },
    });
  }

  onSearchChange(value: string): void {
    this.searchInput$.next(value);
  }

  toggleUser(user: UserDto): void {
    if (user.id === this.auth.user()?.userId) {
      this.showToast('Não pode alterar o estado da sua própria conta.', 'error');
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
      error: err => { this.handleError(err); this.isSaving.set(false); },
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
      error: err => { this.handleError(err); this.isSaving.set(false); },
    });
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
      nome:         form.nome.trim(),
      email:        form.email.trim(),
      senha:        form.senha,
      departamento: form.departamento || undefined,
      cargo:        form.cargo        || undefined,
      telefone:     form.telefone     || undefined,
    }).subscribe({
      next: () => {
        this.isSaving.set(false);
        this.showUserModal.set(false);
        this.loadUsers();
        this.showToast('Utilizador criado com sucesso!');
        this.modalForm.set({ nome: '', email: '', senha: '', departamento: '', cargo: '', telefone: '' });
      },
      error: err => { this.handleError(err); this.isSaving.set(false); },
    });
  }


  loadSessoes(): void {
    this.isLoadingSessoes.set(true);
    this.http.get<SessaoAtiva[]>(`${this.api}/admin/sessoes`).subscribe({
      next:  s   => { this.sessoesAtivas.set(s); this.isLoadingSessoes.set(false); },
      error: err => { console.error(err); this.isLoadingSessoes.set(false); },
    });
  }

  terminateSession(sessionId: string, usuarioNome: string): void {
    if (!confirm(`Deseja terminar a sessão de ${usuarioNome}?`)) return;
    this.http.post(`${this.api}/admin/sessoes/${sessionId}/terminar`, {}).subscribe({
      next: () => {
        this.sessoesAtivas.update(list => list.filter(s => s.id !== sessionId));
        this.showToast(`Sessão de ${usuarioNome} terminada com sucesso`);
      },
      error: err => this.handleError(err),
    });
  }


  formatDateTime(iso: string): string {
    return new Date(iso).toLocaleString('pt-PT', {
      day: '2-digit', month: '2-digit', year: 'numeric',
      hour: '2-digit', minute: '2-digit',
    });
  }

  formatDate(iso: string | null): string {
    if (!iso) return '—';
    return new Date(iso).toLocaleDateString('pt-PT');
  }

  formatRelativeTime(iso: string): string {
    const diff = Date.now() - new Date(iso).getTime();
    const mins  = Math.floor(diff / 60000);
    const hours = Math.floor(diff / 3600000);
    const days  = Math.floor(diff / 86400000);
    if (mins  < 1)  return 'agora';
    if (mins  < 60) return `${mins} min atrás`;
    if (hours < 24) return `${hours} h atrás`;
    return `${days} dias atrás`;
  }

  getPaginationArray(count: number): number[] {
    return Array.from({ length: count }, (_, i) => i);
  }

  actionIcon(acao: string): string {
    if (acao.includes('USER'))    return 'la-user';
    if (acao.includes('SESSION')) return 'la-desktop';
    if (acao.includes('LOGIN'))   return 'la-sign-in-alt';
    if (acao.includes('DELETE'))  return 'la-trash-alt';
    return 'la-history';
  }


  openUserModal(): void {
    this.modalForm.set({ nome: '', email: '', senha: '', departamento: '', cargo: '', telefone: '' });
    this.errorMessage.set(null);
    this.showUserModal.set(true);
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
    if (user) this.deleteUser(user.id);
  }

  setSearchQuery(value: string): void { this.onSearchChange(value); }

  setRoleFilter(value: string): void {
    const safe = (['all', 'admin', 'user'].includes(value) ? value : 'all') as 'all' | 'admin' | 'user';
    this.roleFilter.set(safe);
  }


  private showToast(msg: string, type: 'success' | 'error' = 'success'): void {
    this.toastMessage.set(msg);
    setTimeout(() => this.toastMessage.set(null), 3500);
  }

  private handleError(err: HttpErrorResponse): void {
    const msg = err.status === 401 ? 'Sessão expirada.'
              : err.status === 403 ? 'Sem permissão para esta operação.'
              : err.status === 409 ? (err.error?.message ?? 'Registo já existe.')
              : (err.error?.message ?? 'Erro ao processar requisição.');
    this.errorMessage.set(msg);
    setTimeout(() => this.errorMessage.set(null), 5000);
  }
}
