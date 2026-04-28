import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface GestaoViagem {
  id: number;
  numeroViagem: string;
  status: string;
  prioridade: string;
  dataCriacao: string;
  dataInicioPlaneada?: string;
  dataFimPlaneada?: string;
  dataInicioReal?: string;
  dataFimReal?: string;
  rotaId?: number;
  rotaNome?: string;
  veiculoId?: number;
  veiculoMatricula?: string;
  veiculoMarca?: string;
  veiculoModelo?: string;
  motoristaId?: number;
  motoristaNome?: string;
  transportadoraId?: number;
  transportadoraNome?: string;
  cargaDescricao?: string;
  cargaPeso: number;
  cargaVolume: number;
  cargaObservacoes?: string;
  distanciaTotalKm: number;
  distanciaPercorridaKm: number;
  tempoEstimadoHoras?: number;
  tempoRealHoras?: number;
  atrasoHoras?: number;
  totalEntregas: number;
  entregasConcluidas: number;
  entregasPendentes: number;
  observacoes?: string;
  criadoEm: string;
  atualizadoEm: string;
}

export interface GestaoViagemCreateDto {
  prioridade: string;
  dataInicioPlaneada?: string;
  dataFimPlaneada?: string;
  rotaId?: number;
  veiculoId?: number;
  motoristaId?: number;
  transportadoraId?: number;
  cargaDescricao?: string;
  cargaPeso: number;
  cargaVolume: number;
  cargaObservacoes?: string;
  distanciaTotalKm: number;
  tempoEstimadoHoras?: number;
  observacoes?: string;
}

export interface GestaoViagemUpdateDto {
  status?: string;
  prioridade?: string;
  dataInicioPlaneada?: string;
  dataFimPlaneada?: string;
  dataInicioReal?: string;
  dataFimReal?: string;
  rotaId?: number;
  veiculoId?: number;
  motoristaId?: number;
  transportadoraId?: number;
  cargaDescricao?: string;
  cargaPeso?: number;
  cargaVolume?: number;
  cargaObservacoes?: string;
  distanciaTotalKm?: number;
  distanciaPercorridaKm?: number;
  tempoEstimadoHoras?: number;
  observacoes?: string;
}

export interface PagedResult<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface ListarViagensParams {
  status?: string;
  search?: string;
  page?: number;
  pageSize?: number;
}

@Injectable({ providedIn: 'root' })
export class GestaoViagemService {
  private readonly http = inject(HttpClient);
  private readonly api = `${environment.apiUrl}/user/gestao-viagens`;

  listar(params: ListarViagensParams = {}): Observable<PagedResult<GestaoViagem>> {
    let httpParams = new HttpParams();
    if (params.status) httpParams = httpParams.set('status', params.status);
    if (params.search) httpParams = httpParams.set('search', params.search);
    if (params.page) httpParams = httpParams.set('page', params.page);
    if (params.pageSize) httpParams = httpParams.set('pageSize', params.pageSize);
    return this.http.get<PagedResult<GestaoViagem>>(this.api, { params: httpParams });
  }

  obter(id: number): Observable<GestaoViagem> {
    return this.http.get<GestaoViagem>(`${this.api}/${id}`);
  }

  criar(dto: GestaoViagemCreateDto): Observable<GestaoViagem> {
    return this.http.post<GestaoViagem>(this.api, dto);
  }

  atualizar(id: number, dto: GestaoViagemUpdateDto): Observable<GestaoViagem> {
    return this.http.put<GestaoViagem>(`${this.api}/${id}`, dto);
  }

  iniciar(id: number): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.api}/${id}/iniciar`, {});
  }

  concluir(id: number): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.api}/${id}/concluir`, {});
  }

  deletar(id: number): Observable<{ message: string }> {
    return this.http.delete<{ message: string }>(`${this.api}/${id}`);
  }
}