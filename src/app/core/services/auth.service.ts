import { Injectable, signal, computed, effect } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { map, tap } from 'rxjs/operators';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface AuthResponse {
  token:  string;
  nome:   string;
  email:  string;
  role:   'admin' | 'user';
  userId: number;
}

export interface LoginPayload {
  email: string;
  password: string;
}

export interface RegisterPayload {
  nome:         string;
  email:        string;
  password:        string;
  departamento?: string;
  cargo?:        string;
  telefone?:     string;
}

const TOKEN_KEY = 'acc_token';
const USER_KEY  = 'acc_user';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly api = environment.apiUrl;
  
  // Signal para o estado do usuário
  private _user = signal<AuthResponse | null>(null);
  
  constructor(private http: HttpClient, private router: Router) {
    this.loadFromStorage();
    
    effect(() => {
      const user = this._user();
      if (user) {
        console.log('[Auth] Usuário atualizado:', { nome: user.nome, role: user.role });
      }
    });
  }

  // Getters públicos
  readonly user = this._user.asReadonly();
  readonly isLoggedIn = computed(() => !!this._user() && !!this.getToken());
  readonly isAdmin = computed(() => {
    const role = this._user()?.role;
    return role === 'admin';
  });
  readonly userRole = computed(() => this._user()?.role ?? 'user');
  readonly userName = computed(() => this._user()?.nome ?? '');
  readonly userEmail = computed(() => this._user()?.email ?? '');

  // Login - VERSÃO CORRIGIDA
  login(payload: LoginPayload): Observable<AuthResponse> {
    const url = `${this.api}/auth/login`;
    console.log('[Auth] Login iniciado para:', payload.email);
    console.log('[Auth] URL:', url);
    
    return this.http.post(url, payload).pipe(
      map((response: any) => {
        console.log('[Auth] Resposta BRUTA do servidor:', JSON.stringify(response, null, 2));
        
        // 🔧 CORREÇÃO: Extrair os dados corretamente
        let result: AuthResponse;
        
        if (response.user) {
          // Formato: { token: "...", user: { nome, email, role, userId } }
          result = {
            token: response.token,
            nome: response.user.nome,
            email: response.user.email,
            role: response.user.role,
            userId: response.user.userId
          };
        } else {
          // Formato direto: { token, nome, email, role, userId }
          result = {
            token: response.token,
            nome: response.nome,
            email: response.email,
            role: response.role,
            userId: response.userId
          };
        }
        
        console.log('[Auth] Resposta NORMALIZADA:', result);
        return result;
      }),
      tap({
        next: (normalized) => {
          if (!normalized.token || !normalized.role || !normalized.userId) {
            console.error('[Auth] Resposta inválida - campos faltando:', normalized);
            throw new Error('Resposta do servidor incompleta');
          }
          
          this.saveToStorage(normalized);
          this._user.set(normalized);
          console.log('[Auth] Login OK! Usuário:', normalized.nome, 'Role:', normalized.role);
        },
        error: (error) => {
          console.error('[Auth] Erro no login:', error);
        }
      })
    );
  }

  // Register
  register(payload: RegisterPayload): Observable<AuthResponse> {
    const url = `${this.api}/auth/register`;
    console.log('[Auth] Registro iniciado para:', payload.email);
    
    return this.http.post(url, payload).pipe(
      map((response: any) => {
        let result: AuthResponse;
        
        if (response.user) {
          result = {
            token: response.token,
            nome: response.user.nome,
            email: response.user.email,
            role: response.user.role,
            userId: response.user.userId
          };
        } else {
          result = {
            token: response.token,
            nome: response.nome,
            email: response.email,
            role: response.role,
            userId: response.userId
          };
        }
        
        return result;
      }),
      tap({
        next: (normalized) => {
          this.saveToStorage(normalized);
          this._user.set(normalized);
          console.log('[Auth] Registro OK!');
        },
        error: (error) => {
          console.error('[Auth] Erro no registro:', error);
        }
      })
    );
  }

  // Logout
  logout(): void {
    console.log('[Auth] Logout executado');
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(USER_KEY);
    this._user.set(null);
    this.router.navigate(['/login']);
  }

  getToken(): string | null {
    return localStorage.getItem(TOKEN_KEY);
  }

  private saveToStorage(response: AuthResponse): void {
    localStorage.setItem(TOKEN_KEY, response.token);
    localStorage.setItem(USER_KEY, JSON.stringify({
      token: response.token,
      nome: response.nome,
      email: response.email,
      role: response.role,
      userId: response.userId
    }));
  }

  private loadFromStorage(): void {
    try {
      const stored = localStorage.getItem(USER_KEY);
      if (stored) {
        const user = JSON.parse(stored);
        const token = this.getToken();
        if (token && user) {
          this._user.set(user);
          console.log('[Auth] Usuário carregado do storage:', user.nome, user.role);
        }
      }
    } catch (error) {
      console.error('[Auth] Erro ao carregar do storage:', error);
    }
  }
}