import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface FechoViagem {
  id: number;
  numeroFecho: string;
  atribuicaoId: number;
  atribuicaoNumero?: string;
  clienteNome?: string;
  dataFecho: string;
  status: string;
  dataInicioReal?: string;
  dataFimReal?: string;
  tempoTotalReal?: string;
  tempoPlaneado?: string;
  diferencaTempo?: string;
  combustivelLitros?: number;
  combustivelCusto?: number;
  portagensCusto?: number;
  outrosCustos?: number;
  custosExtrasDescricao?: string;
  custoTotal: number;
  quilometrosInicio?: number;
  quilometrosFim?: number;
  quilometrosPercorridos?: number;
  totalEntregas: number;
  entregasRealizadas: number;
  entregasNaoRealizadas: number;
  entregasPendentesObs?: string;
  temIncidentes: boolean;
  incidentesDescricao?: string;
  faturado: boolean;
  observacoes?: string;
  criadoEm: string;
  atualizadoEm: string;
}

export interface FechoViagemCreateDto {
  atribuicaoId: number;
  dataInicioReal?: string;
  dataFimReal?: string;
  combustivelLitros?: number;
  combustivelCusto?: number;
  portagensCusto?: number;
  outrosCustos?: number;
  custosExtrasDescricao?: string;
  quilometrosInicio?: number;
  quilometrosFim?: number;
  entregasNaoRealizadasIds?: number[];
  entregasPendentesObs?: string;
  temIncidentes: boolean;
  incidentesDescricao?: string;
  observacoes?: string;
}

export interface FechoViagemUpdateDto {
  status?: string;
  dataInicioReal?: string;
  dataFimReal?: string;
  combustivelLitros?: number;
  combustivelCusto?: number;
  portagensCusto?: number;
  outrosCustos?: number;
  custosExtrasDescricao?: string;
  quilometrosInicio?: number;
  quilometrosFim?: number;
  entregasNaoRealizadasIds?: number[];
  entregasPendentesObs?: string;
  temIncidentes?: boolean;
  incidentesDescricao?: string;
  observacoes?: string;
  faturado?: boolean;
}

export interface PagedResult<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface ListarFechosParams {
  status?: string;
  search?: string;
  page?: number;
  pageSize?: number;
}

@Injectable({ providedIn: 'root' })
export class FechoViagemService {
  private readonly http = inject(HttpClient);
  private readonly api = `${environment.apiUrl}/user/fechos-viagem`;

  listar(params: ListarFechosParams = {}): Observable<PagedResult<FechoViagem>> {
    let httpParams = new HttpParams();
    if (params.status) httpParams = httpParams.set('status', params.status);
    if (params.search) httpParams = httpParams.set('search', params.search);
    if (params.page) httpParams = httpParams.set('page', params.page);
    if (params.pageSize) httpParams = httpParams.set('pageSize', params.pageSize);
    return this.http.get<PagedResult<FechoViagem>>(this.api, { params: httpParams });
  }

  obter(id: number): Observable<FechoViagem> {
    return this.http.get<FechoViagem>(`${this.api}/${id}`);
  }

  obterPorAtribuicao(atribuicaoId: number): Observable<FechoViagem> {
    return this.http.get<FechoViagem>(`${this.api}/por-atribuicao/${atribuicaoId}`);
  }

  criar(dto: FechoViagemCreateDto): Observable<FechoViagem> {
    return this.http.post<FechoViagem>(this.api, dto);
  }

  atualizar(id: number, dto: FechoViagemUpdateDto): Observable<FechoViagem> {
    return this.http.put<FechoViagem>(`${this.api}/${id}`, dto);
  }

  processar(id: number): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.api}/${id}/processar`, {});
  }

  deletar(id: number): Observable<{ message: string }> {
    return this.http.delete<{ message: string }>(`${this.api}/${id}`);
  }
}