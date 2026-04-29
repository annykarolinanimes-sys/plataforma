import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface DocumentoGeral {
  id: number;
  numeroDocumento: string;
  tipo: string;
  nome: string;
  descricao?: string;
  dataDocumento: string;
  dataCriacao: string;
  caminhoFicheiro?: string;
  tamanhoBytes: number;
  entidadeRelacionada?: string;
  entidadeId?: number;
  entidadeNome?: string;
  tags?: string;
  categoria?: string;
  favorito: boolean;
  visualizacoes: number;
  downloads: number;
  ultimoAcesso?: string;
  observacoes?: string;
  criadoEm: string;
}

export interface PagedResult<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface EstatisticasDocumentos {
  total: number;
  porTipo: { tipo: string; total: number }[];
  totalDownloads: number;
  totalVisualizacoes: number;
  favoritos: number;
}

@Injectable({ providedIn: 'root' })
export class DocumentosService {
  private readonly http = inject(HttpClient);
  private readonly api = `${environment.apiUrl}/user/documentos-gerais`;

  listar(params: {
    tipo?: string;
    categoria?: string;
    search?: string;
    entidadeId?: number;
    entidadeRelacionada?: string;
    favorito?: boolean;
    dataInicio?: string;
    dataFim?: string;
    page?: number;
    pageSize?: number;
  } = {}): Observable<PagedResult<DocumentoGeral>> {
    let httpParams = new HttpParams();
    if (params.tipo) httpParams = httpParams.set('tipo', params.tipo);
    if (params.categoria) httpParams = httpParams.set('categoria', params.categoria);
    if (params.search) httpParams = httpParams.set('search', params.search);
    if (params.entidadeId) httpParams = httpParams.set('entidadeId', params.entidadeId);
    if (params.entidadeRelacionada) httpParams = httpParams.set('entidadeRelacionada', params.entidadeRelacionada);
    if (params.favorito !== undefined) httpParams = httpParams.set('favorito', params.favorito);
    if (params.dataInicio) httpParams = httpParams.set('dataInicio', params.dataInicio);
    if (params.dataFim) httpParams = httpParams.set('dataFim', params.dataFim);
    if (params.page) httpParams = httpParams.set('page', params.page);
    if (params.pageSize) httpParams = httpParams.set('pageSize', params.pageSize);
    return this.http.get<PagedResult<DocumentoGeral>>(this.api, { params: httpParams });
  }

  obter(id: number): Observable<DocumentoGeral> {
    return this.http.get<DocumentoGeral>(`${this.api}/${id}`);
  }

  upload(formData: FormData): Observable<DocumentoGeral> {
    return this.http.post<DocumentoGeral>(`${this.api}/upload`, formData);
  }

  atualizar(id: number, dados: Partial<DocumentoGeral>): Observable<DocumentoGeral> {
    return this.http.put<DocumentoGeral>(`${this.api}/${id}`, dados);
  }

  download(id: number): Observable<Blob> {
    return this.http.get(`${this.api}/${id}/download`, { responseType: 'blob' });
  }

  alternarFavorito(id: number): Observable<{ favorito: boolean }> {
    return this.http.post<{ favorito: boolean }>(`${this.api}/${id}/favorito`, {});
  }

  deletar(id: number): Observable<{ message: string }> {
    return this.http.delete<{ message: string }>(`${this.api}/${id}`);
  }

  getEstatisticas(): Observable<EstatisticasDocumentos> {
    return this.http.get<EstatisticasDocumentos>(`${this.api}/estatisticas`);
  }

  // Dados auxiliares para filtros
  obterClientes(): Observable<{ id: number; nome: string }[]> {
    return this.http.get<{ items: any[] }>(`${environment.apiUrl}/user/clientes-catalogo?pageSize=100`).pipe(
      map(res => res.items.map(c => ({ id: c.id, nome: c.nome })))
    );
  }

  obterFornecedores(): Observable<{ id: number; nome: string }[]> {
    return this.http.get<{ items: any[] }>(`${environment.apiUrl}/user/fornecedores-catalogo?pageSize=100`).pipe(
      map(res => res.items.map(f => ({ id: f.id, nome: f.nome })))
    );
  }

  obterFaturas(): Observable<{ id: number; numeroFatura: string }[]> {
    return this.http.get<{ items: any[] }>(`${environment.apiUrl}/user/faturas?pageSize=100`).pipe(
      map(res => res.items.map(f => ({ id: f.id, numeroFatura: f.numeroFatura })))
    );
  }
}

// Import necessário
import { map } from 'rxjs/operators';