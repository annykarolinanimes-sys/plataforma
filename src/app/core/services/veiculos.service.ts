import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface Veiculo {
  id: number;
  matricula: string;
  marca: string;
  modelo: string;
  cor?: string;
  ano?: number;
  vin?: string;
  tipoCombustivel?: string;
  cilindrada?: number;
  potencia?: number;
  lugares?: number;
  peso?: number;
  proprietarioId?: number;
  proprietario?: { id: number; nome: string; codigo: string };
  ativo: boolean;
  observacoes?: string;
  criadoEm: string;
  atualizadoEm: string;
}

@Injectable({ providedIn: 'root' })
export class VeiculosService {
  private http = inject(HttpClient);
  private api = `${environment.apiUrl}/user/veiculos`;

  listar(search?: string, ativo?: boolean): Observable<Veiculo[]> {
    let params = new URLSearchParams();
    if (search) params.set('search', search);
    if (ativo !== undefined) params.set('ativo', ativo.toString());
    const url = params.toString() ? `${this.api}?${params}` : this.api;
    return this.http.get<Veiculo[]>(url);
  }

  obter(id: number): Observable<Veiculo> {
    return this.http.get<Veiculo>(`${this.api}/${id}`);
  }

  criar(data: Veiculo): Observable<Veiculo> {
    return this.http.post<Veiculo>(this.api, data);
  }

  atualizar(id: number, data: Veiculo): Observable<Veiculo> {
    return this.http.put<Veiculo>(`${this.api}/${id}`, data);
  }

  deletar(id: number): Observable<{ message: string }> {
    return this.http.delete<{ message: string }>(`${this.api}/${id}`);
  }

  ativar(id: number): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.api}/${id}/ativar`, {});
  }
}