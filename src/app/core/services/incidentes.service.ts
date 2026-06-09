import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

// ── Interfaces ────────────────────────────────────────────────────────────────

export interface IncidenteAnexo {
  id: number;
  nomeOriginal: string;
  mimeType: string;
  tamanhoBytes: number;
  tamanhoFormatado: string;
  criadoEm: string;
}

export interface Incidente {
  id: number;
  numeroIncidente: string;
  dataOcorrencia: string;
  tipo: string;
  gravidade: string;
  status: string;
  titulo: string;
  descricao?: string;
  viagemId?: number;
  viagemNumero?: string;
  veiculoId?: number;
  veiculoMatricula?: string;
  clienteId?: number;
  clienteNome?: string;
  atribuicaoId?: number;
  atribuicaoNumero?: string;
  dataResolucao?: string;
  causa?: string;
  acaoCorretiva?: string;
  responsavelResolucao?: string;
  custoAssociado?: number;
  observacoes?: string;
  totalAnexos: number;
  anexos: IncidenteAnexo[];   // ← NOVO
  criadoEm: string;
  atualizadoEm: string;
}

export interface IncidenteCreateDto {
  tipo: string;
  gravidade: string;
  titulo: string;
  descricao?: string;
  dataOcorrencia?: string;
  viagemId?: number;
  veiculoId?: number;
  clienteId?: number;
  atribuicaoId?: number;
  causa?: string;
  acaoCorretiva?: string;
  responsavelResolucao?: string;
  custoAssociado?: number;
  observacoes?: string;
}

export interface IncidenteUpdateDto {
  status?: string;
  gravidade?: string;
  descricao?: string;
  causa?: string;
  acaoCorretiva?: string;
  responsavelResolucao?: string;
  custoAssociado?: number;
  observacoes?: string;
  dataResolucao?: string;
}

export interface ResolverIncidenteDto {
  acaoCorretiva: string;
  responsavelResolucao?: string;
  custoAssociado?: number;
  observacoes?: string;
}

export interface PagedResult<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface ListarIncidentesParams {
  tipo?: string;
  status?: string;
  gravidade?: string;
  viagemId?: number;
  veiculoId?: number;
  clienteId?: number;
  search?: string;
  page?: number;
  pageSize?: number;
}

// ── Service ───────────────────────────────────────────────────────────────────

@Injectable({ providedIn: 'root' })
export class IncidentesService {
  private readonly http = inject(HttpClient);
  private readonly api = `${environment.apiUrl}/user/incidentes`;

  listar(params: ListarIncidentesParams = {}): Observable<PagedResult<Incidente>> {
    let httpParams = new HttpParams();
    if (params.tipo)      httpParams = httpParams.set('tipo',      params.tipo);
    if (params.status)    httpParams = httpParams.set('status',    params.status);
    if (params.gravidade) httpParams = httpParams.set('gravidade', params.gravidade);
    if (params.viagemId)  httpParams = httpParams.set('viagemId',  params.viagemId);
    if (params.veiculoId) httpParams = httpParams.set('veiculoId', params.veiculoId);
    if (params.clienteId) httpParams = httpParams.set('clienteId', params.clienteId);
    if (params.search)    httpParams = httpParams.set('search',    params.search);
    if (params.page)      httpParams = httpParams.set('page',      params.page);
    if (params.pageSize)  httpParams = httpParams.set('pageSize',  params.pageSize);
    return this.http.get<PagedResult<Incidente>>(this.api, { params: httpParams });
  }

  obter(id: number): Observable<Incidente> {
    return this.http.get<Incidente>(`${this.api}/${id}`);
  }

  obterPorViagem(viagemId: number): Observable<Incidente[]> {
    return this.http.get<Incidente[]>(`${this.api}/por-viagem/${viagemId}`);
  }

  criar(dto: IncidenteCreateDto): Observable<Incidente> {
    return this.http.post<Incidente>(this.api, dto);
  }

  atualizar(id: number, dto: IncidenteUpdateDto): Observable<Incidente> {
    return this.http.put<Incidente>(`${this.api}/${id}`, dto);
  }

  resolver(id: number, dto: ResolverIncidenteDto): Observable<{ message: string; incidenteId: number }> {
    return this.http.post<{ message: string; incidenteId: number }>(`${this.api}/${id}/resolver`, dto);
  }

  fechar(id: number): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.api}/${id}/fechar`, {});
  }

  deletar(id: number): Observable<{ message: string }> {
    return this.http.delete<{ message: string }>(`${this.api}/${id}`);
  }


  uploadAnexo(incidenteId: number, ficheiro: File): Observable<IncidenteAnexo> {
    const formData = new FormData();
    formData.append('ficheiro', ficheiro);
    return this.http.post<IncidenteAnexo>(`${this.api}/${incidenteId}/anexos`, formData);
  }

  downloadAnexo(incidenteId: number, anexoId: number): Observable<Blob> {
    return this.http.get(`${this.api}/${incidenteId}/anexos/${anexoId}`, {
      responseType: 'blob'
    });
  }

  removerAnexo(incidenteId: number, anexoId: number): Observable<{ message: string }> {
    return this.http.delete<{ message: string }>(
      `${this.api}/${incidenteId}/anexos/${anexoId}`
    );
  }
}