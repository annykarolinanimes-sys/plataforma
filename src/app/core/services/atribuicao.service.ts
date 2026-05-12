import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface AtribuicaoEntrega {
  id?: number;
  destinatario?: string;
  endereco?: string;
  contacto?: string;
  observacoes?: string;
  ordem: number;
  realizada?: boolean;
}

export interface Atribuicao {
  id: number;
  numeroAtribuicao: string;
  dataAtribuicao: string;
  status: string;
  prioridade: string;
  clienteNome?: string;
  clienteContacto?: string;
  enderecoOrigem?: string;
  enderecoDestino?: string;
  dataPrevistaInicio?: string;
  dataPrevistaFim?: string;
  observacoes?: string;
  motoristaId?: number;
  motoristaNome?: string;
  veiculoId?: number;
  veiculoMatricula?: string;
  veiculoMarca?: string;
  veiculoModelo?: string;
  transportadoraId?: number;
  transportadoraNome?: string;
  distanciaTotalKm: number;
  tempoEstimadoHoras?: number;
  totalEntregas: number;
  entregasRealizadas: number;
  entregas: AtribuicaoEntrega[];
  criadoEm: string;
  atualizadoEm: string;
}

export interface AtribuicaoCreateDto {
  clienteNome: string;
  clienteContacto?: string;
  enderecoOrigem?: string;
  enderecoDestino?: string;
  dataPrevistaInicio?: string;
  dataPrevistaFim?: string;
  prioridade: string;
  observacoes?: string;
  motoristaId?: number;
  veiculoId?: number;
  transportadoraId?: number;
  distanciaTotalKm: number;
  tempoEstimadoHoras?: number;
  entregas?: Omit<AtribuicaoEntrega, 'id' | 'realizada'>[];
}

export interface AtribuicaoUpdateDto {
  status?: string;
  prioridade?: string;
  dataPrevistaInicio?: string;
  dataPrevistaFim?: string;
  observacoes?: string;
  motoristaId?: number;
  veiculoId?: number;
  transportadoraId?: number;
  distanciaTotalKm?: number;
  tempoEstimadoHoras?: number;
  entregas?: AtribuicaoEntrega[];
}

export interface PagedResult<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface ListarAtribuicoesParams {
  status?: string;
  search?: string;
  page?: number;
  pageSize?: number;
}

@Injectable({ providedIn: 'root' })
export class AtribuicaoService {
  private readonly http = inject(HttpClient);
  private readonly api = `${environment.apiUrl}/user/atribuicoes`;

  listar(params: ListarAtribuicoesParams = {}): Observable<PagedResult<Atribuicao>> {
    let httpParams = new HttpParams();
    if (params.status) httpParams = httpParams.set('status', params.status);
    if (params.search) httpParams = httpParams.set('search', params.search);
    if (params.page) httpParams = httpParams.set('page', params.page);
    if (params.pageSize) httpParams = httpParams.set('pageSize', params.pageSize);
    return this.http.get<PagedResult<Atribuicao>>(this.api, { params: httpParams });
  }

  obter(id: number): Observable<Atribuicao> {
    return this.http.get<Atribuicao>(`${this.api}/${id}`);
  }

  criar(dto: AtribuicaoCreateDto): Observable<Atribuicao> {
    return this.http.post<Atribuicao>(this.api, dto);
  }

  atualizar(id: number, dto: AtribuicaoUpdateDto): Observable<Atribuicao> {
    return this.http.put<Atribuicao>(`${this.api}/${id}`, dto);
  }

  deletar(id: number): Observable<{ message: string }> {
    return this.http.delete<{ message: string }>(`${this.api}/${id}`);
  }

  iniciar(id: number): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.api}/${id}/iniciar`, {});
  }

  concluir(id: number): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.api}/${id}/concluir`, {});
  }
}