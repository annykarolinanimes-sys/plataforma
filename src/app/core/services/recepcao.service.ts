import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface RecepcaoItem {
  id?: number;
  sku: string;
  produtoNome: string;
  quantidadeEsperada: number;
  quantidadeRecebida: number;
  quantidadeRejeitada: number;
  lote?: string;
  validade?: string;
  localizacao?: string;
  observacoes?: string;
  conformidade?: boolean;
}

export interface Recepcao {
  id: number;
  numeroRecepcao: string;
  fornecedor: string;
  tipoEntrada: string;
  dataRecepcao: string;
  status: string;
  prioridade: string;
  documentoReferencia?: string;
  totalItens: number;
  totalUnidades: number;
  criadoEm: string;
  atualizadoEm: string;
  itens: RecepcaoItem[];
}

export interface RecepcaoCreateDto {
  fornecedor: string;
  tipoEntrada: string;
  prioridade: string;
  documentoReferencia?: string;
  itens: Omit<RecepcaoItem, 'id' | 'conformidade'>[];
}

export interface RecepcaoUpdateDto {
  fornecedor?: string;
  status?: string;
  prioridade?: string;
  documentoReferencia?: string;
  itens?: RecepcaoItem[];
}

export interface PagedResult<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface ListarRecepcoesParams {
  status?: string;
  search?: string;
  page?: number;
  pageSize?: number;
}

@Injectable({ providedIn: 'root' })
export class RecepcaoService {
  private readonly http = inject(HttpClient);
  private readonly api = `${environment.apiUrl}/user/recepcao`;

  listar(params: ListarRecepcoesParams = {}): Observable<PagedResult<Recepcao>> {
    let httpParams = new HttpParams();
    if (params.status) httpParams = httpParams.set('status', params.status);
    if (params.search) httpParams = httpParams.set('search', params.search);
    if (params.page) httpParams = httpParams.set('page', params.page);
    if (params.pageSize) httpParams = httpParams.set('pageSize', params.pageSize);

    return this.http.get<PagedResult<Recepcao>>(this.api, { params: httpParams });
  }

  obter(id: number): Observable<Recepcao> {
    return this.http.get<Recepcao>(`${this.api}/${id}`);
  }

  criar(dto: RecepcaoCreateDto): Observable<Recepcao> {
    return this.http.post<Recepcao>(this.api, dto);
  }

  atualizar(id: number, dto: RecepcaoUpdateDto): Observable<Recepcao> {
    return this.http.put<Recepcao>(`${this.api}/${id}`, dto);
  }

  deletar(id: number): Observable<{ message: string }> {
    return this.http.delete<{ message: string }>(`${this.api}/${id}`);
  }

  concluir(id: number): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.api}/${id}/concluir`, {});
  }
}