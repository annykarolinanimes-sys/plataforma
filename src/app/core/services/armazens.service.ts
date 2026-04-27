import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface Armazem {
  id: number;
  codigo: string;
  nome: string;
  tipo?: string;
  morada?: string;
  localidade?: string;
  codigoPostal?: string;
  pais?: string;
  telefone?: string;
  email?: string;
  responsavelNome?: string;
  responsavelTelefone?: string;
  observacoes?: string;
  ativo: boolean;
  criadoEm: string;
  atualizadoEm: string;
}

@Injectable({ providedIn: 'root' })
export class ArmazensService {
  private http = inject(HttpClient);
  private api = `${environment.apiUrl}/user/armazens`;

  listar(search?: string, ativo?: boolean): Observable<Armazem[]> {
    let params = new URLSearchParams();
    if (search) params.set('search', search);
    if (ativo !== undefined) params.set('ativo', ativo.toString());
    const url = params.toString() ? `${this.api}?${params}` : this.api;
    return this.http.get<Armazem[]>(url);
  }

  obter(id: number): Observable<Armazem> {
    return this.http.get<Armazem>(`${this.api}/${id}`);
  }

  criar(data: Armazem): Observable<Armazem> {
    return this.http.post<Armazem>(this.api, data);
  }

  atualizar(id: number, data: Armazem): Observable<Armazem> {
    return this.http.put<Armazem>(`${this.api}/${id}`, data);
  }

  deletar(id: number): Observable<{ message: string }> {
    return this.http.delete<{ message: string }>(`${this.api}/${id}`);
  }

  ativar(id: number): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.api}/${id}/ativar`, {});
  }
}