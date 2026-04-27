import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface TransportadoraCatalogo {
  id: number;
  codigo: string;
  nome: string;
  nif?: string;
  telefone?: string;
  email?: string;
  morada?: string;
  localidade?: string;
  codigoPostal?: string;
  pais?: string;
  contactoNome?: string;
  contactoTelefone?: string;
  observacoes?: string;
  ativo: boolean;
  criadoEm: string;
  atualizadoEm: string;
}

@Injectable({ providedIn: 'root' })
export class TransportadorasCatalogoService {
  private http = inject(HttpClient);
  private api = `${environment.apiUrl}/user/transportadoras-catalogo`;

  listar(search?: string, ativo?: boolean): Observable<TransportadoraCatalogo[]> {
    let params = new URLSearchParams();
    if (search) params.set('search', search);
    if (ativo !== undefined) params.set('ativo', ativo.toString());
    const url = params.toString() ? `${this.api}?${params}` : this.api;
    return this.http.get<TransportadoraCatalogo[]>(url);
  }

  obter(id: number): Observable<TransportadoraCatalogo> {
    return this.http.get<TransportadoraCatalogo>(`${this.api}/${id}`);
  }

  criar(data: TransportadoraCatalogo): Observable<TransportadoraCatalogo> {
    return this.http.post<TransportadoraCatalogo>(this.api, data);
  }

  atualizar(id: number, data: TransportadoraCatalogo): Observable<TransportadoraCatalogo> {
    return this.http.put<TransportadoraCatalogo>(`${this.api}/${id}`, data);
  }

  deletar(id: number): Observable<{ message: string }> {
    return this.http.delete<{ message: string }>(`${this.api}/${id}`);
  }

  ativar(id: number): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.api}/${id}/ativar`, {});
  }
}