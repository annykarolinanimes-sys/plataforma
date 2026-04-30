import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface RotaCatalogo {
  id: number;
  codigo: string;
  nome: string;
  descricao?: string;
  origem?: string;
  destino?: string;
  distanciaKm?: number;
  tempoEstimadoMin?: number;
  transportadoraId?: number;
  transportadora?: { id: number; nome: string };
  ativo: boolean;
  criadoEm: string;
  atualizadoEm: string;
}

@Injectable({ providedIn: 'root' })
export class RotasCatalogoService {
  private http = inject(HttpClient);
  private api = `${environment.apiUrl}/user/rotas-catalogo`;

  listar(search?: string, ativo?: boolean): Observable<RotaCatalogo[]> {
    let params = new URLSearchParams();
    if (search) params.set('search', search);
    if (ativo !== undefined) params.set('ativo', ativo.toString());
    const url = params.toString() ? `${this.api}?${params}` : this.api;
    return this.http.get<RotaCatalogo[]>(url);
  }
  obter(id: number): Observable<RotaCatalogo> { return this.http.get<RotaCatalogo>(`${this.api}/${id}`); }
  criar(data: RotaCatalogo): Observable<RotaCatalogo> { return this.http.post<RotaCatalogo>(this.api, data); }
  atualizar(id: number, data: RotaCatalogo): Observable<RotaCatalogo> { return this.http.put<RotaCatalogo>(`${this.api}/${id}`, data); }
  deletar(id: number): Observable<{ message: string }> { return this.http.delete<{ message: string }>(`${this.api}/${id}`); }
  ativar(id: number): Observable<{ message: string }> { return this.http.post<{ message: string }>(`${this.api}/${id}/ativar`, {}); }
}