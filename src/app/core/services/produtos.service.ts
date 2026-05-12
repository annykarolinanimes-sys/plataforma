import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';


export interface ProdutoModel {
  id: number;
  sku: string;
  nome: string;
  descricao?: string;
  categoria?: string;
  fornecedorId?: number;
  fornecedorCodigo?: string;
  fornecedor?: { id: number; nome: string };
  fornecedorNome?: string;
  precoCompra: number;
  precoVenda: number;
  iva: number;
  stockAtual: number;
  stockMinimo: number;
  unidadeMedida: string;
  localizacao?: string;
  loteObrigatorio: boolean;
  validadeObrigatoria: boolean;
  ativo: boolean;
  criadoPor?: number;
  criadoEm: string;
  atualizadoEm: string;
}

export interface ProdutoCreateDto {
  nome: string;
  descricao?: string;
  categoria?: string;
  fornecedorId?: number;
  fornecedorCodigo?: string;
  precoCompra: number;
  precoVenda: number;
  iva: number;
  stockInicial: number;  
  stockMinimo: number;
  unidadeMedida: string;
  localizacao?: string;
  loteObrigatorio: boolean;
  validadeObrigatoria: boolean;
}

export interface ProdutoUpdateDto {
  nome: string;
  descricao?: string;
  categoria?: string;
  fornecedorId?: number;
  fornecedorCodigo?: string;
  precoCompra: number;
  precoVenda: number;
  iva: number;
  stockMinimo: number;
  unidadeMedida: string;
  localizacao?: string;
  loteObrigatorio: boolean;
  validadeObrigatoria: boolean;
  ativo: boolean;
}

export interface PagedResult<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface ListarParams {
  search?: string;
  categoria?: string;
  ativo?: boolean;
  page?: number;
  pageSize?: number;
}



@Injectable({ providedIn: 'root' })
export class ProdutosService {
  private readonly http = inject(HttpClient);
  private readonly api  = `${environment.apiUrl}/user/produtos`;


  listar(params: ListarParams = {}): Observable<PagedResult<ProdutoModel>> {
    let httpParams = new HttpParams();
    if (params.search)              httpParams = httpParams.set('search',    params.search);
    if (params.categoria)           httpParams = httpParams.set('categoria', params.categoria);
    if (params.ativo !== undefined) httpParams = httpParams.set('ativo',     String(params.ativo));
    if (params.page)                httpParams = httpParams.set('page',      String(params.page));
    if (params.pageSize)            httpParams = httpParams.set('pageSize',  String(params.pageSize));

    return this.http
      .get<PagedResult<ProdutoModel>>(this.api, { params: httpParams })
      .pipe(catchError(this.handleError));
  }

  obter(id: number): Observable<ProdutoModel> {
    return this.http
      .get<ProdutoModel>(`${this.api}/${id}`)
      .pipe(catchError(this.handleError));
  }

  criar(dto: ProdutoCreateDto): Observable<ProdutoModel> {
    return this.http
      .post<ProdutoModel>(this.api, dto)
      .pipe(catchError(this.handleError));
  }

  atualizar(id: number, dto: ProdutoUpdateDto): Observable<ProdutoModel> {
    return this.http
      .put<ProdutoModel>(`${this.api}/${id}`, dto)
      .pipe(catchError(this.handleError));
  }

  deletar(id: number): Observable<{ message: string }> {
    return this.http
      .delete<{ message: string }>(`${this.api}/${id}`)
      .pipe(catchError(this.handleError));
  }

  ativar(id: number): Observable<{ message: string }> {
    return this.http
      .post<{ message: string }>(`${this.api}/${id}/ativar`, {})
      .pipe(catchError(this.handleError));
  }

  private handleError(error: HttpErrorResponse): Observable<never> {
    let message = 'Ocorreu um erro inesperado.';

    if (error.status === 0) {
      message = 'Sem ligação ao servidor. Verifique a sua ligação à internet.';
    } else if (error.status === 400) {
      const body = error.error;
      if (body?.errors) {
        const msgs = Object.values(body.errors as Record<string, string[]>)
          .flat()
          .join(' ');
        message = msgs || body.message || 'Dados inválidos.';
      } else {
        message = body?.message || 'Pedido inválido.';
      }
    } else if (error.status === 401) {
      message = 'Sessão expirada. Por favor autentique-se novamente.';
    } else if (error.status === 403) {
      message = 'Não tem permissão para esta operação.';
    } else if (error.status === 404) {
      message = error.error?.message || 'Recurso não encontrado.';
    } else if (error.status === 409) {
      message = error.error?.message || 'Conflito: já existe um registo com estes dados.';
    } else if (error.status >= 500) {
      message = 'Erro interno do servidor. Tente novamente mais tarde.';
    }

    return throwError(() => ({ status: error.status, message }));
  }
}
