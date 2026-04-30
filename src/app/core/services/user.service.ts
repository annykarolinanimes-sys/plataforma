import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface UserProfile {
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

export interface UpdateProfileRequest {
  nome: string;
  departamento?: string | null;
  cargo?: string | null;
  telefone?: string | null;
}

export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
}

export interface Alerta {
  id: number;
  tipo: string;
  mensagem: string;
  detalhe?: string;
  lido: boolean;
  data: string;
}

@Injectable({ providedIn: 'root' })
export class UserService {
  private http = inject(HttpClient);
  private api = environment.apiUrl;

  getMe(): Observable<UserProfile> {
    return this.http.get<UserProfile>(`${this.api}/user/me`);
  }

  updateMe(data: UpdateProfileRequest): Observable<UserProfile> {
    return this.http.put<UserProfile>(`${this.api}/user/me`, data);
  }

  changePassword(data: ChangePasswordRequest): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.api}/user/change-password`, data);
  }

  uploadAvatar(file: File): Observable<{ avatarUrl: string }> {
    const formData = new FormData();
    formData.append('avatar', file);
    return this.http.post<{ avatarUrl: string }>(`${this.api}/user/avatar`, formData);
  }

  getAlertas(lido?: boolean, tipo?: string): Observable<Alerta[]> {
    let params = new URLSearchParams();
    if (lido !== undefined) params.set('lido', lido.toString());
    if (tipo) params.set('tipo', tipo);
    const url = params.toString() ? `${this.api}/user/alertas?${params}` : `${this.api}/user/alertas`;
    return this.http.get<Alerta[]>(url);
  }

  marcarAlertasLidos(ids: number[]): Observable<void> {
    return this.http.patch<void>(`${this.api}/user/alertas/lidos`, { ids });
  }

  marcarTodosAlertasLidos(tipo?: string): Observable<void> {
    let url = `${this.api}/user/alertas/todos-lidos`;
    if (tipo) url += `?tipo=${tipo}`;
    return this.http.patch<void>(url, {});
  }
}