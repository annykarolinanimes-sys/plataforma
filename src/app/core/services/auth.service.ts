import { Injectable, signal, computed } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { tap } from 'rxjs/operators';
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
  senha: string;
}

export interface RegisterPayload {
  nome:         string;
  email:        string;
  senha:        string;
  departamento?: string;
  cargo?:        string;
  telefone?:     string;
}


const TOKEN_KEY = 'acc_token';
const USER_KEY  = 'acc_user';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly api = environment.apiUrl;

  private _user = signal<AuthResponse | null>(this.loadUser());

  readonly user       = this._user.asReadonly();
  readonly isLoggedIn = computed(() => !!this._user());
  readonly isAdmin    = computed(() => this._user()?.role === 'admin');
  readonly userRole   = computed(() => this._user()?.role ?? 'user');
  readonly userName   = computed(() => this._user()?.nome ?? '');
  readonly userEmail  = computed(() => this._user()?.email ?? '');

  constructor(private http: HttpClient, private router: Router) {}

  login(payload: LoginPayload): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>(`${this.api}/auth/login`, payload)
      .pipe(
        tap(res => this.persist(res))
      );
  }

  register(payload: RegisterPayload): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>(`${this.api}/auth/register`, payload)
      .pipe(
        tap(res => this.persist(res))
      );
  }

  logout(): void {
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(USER_KEY);
    this._user.set(null);
    this.router.navigate(['/login']);
  }

  getToken(): string | null {
    return localStorage.getItem(TOKEN_KEY);
  }


  private persist(res: AuthResponse): void {
    localStorage.setItem(TOKEN_KEY, res.token);
    localStorage.setItem(USER_KEY, JSON.stringify(res));
    this._user.set(res);
  }

  private loadUser(): AuthResponse | null {
    try {
      const raw = localStorage.getItem(USER_KEY);
      return raw ? (JSON.parse(raw) as AuthResponse) : null;
    } catch {
      return null;
    }
  }
}
