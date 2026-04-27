import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';


export interface FornecedorModel {
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

export interface FornecedorCreateDto {
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
}

export interface FornecedorUpdateDto extends FornecedorCreateDto {
  ativo: boolean;
}

export interface PagedResult<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface ListarFornecedoresParams {
  search?: string;
  ativo?: boolean;
  page?: number;
  pageSize?: number;
}


@Injectable({ providedIn: 'root' })
export class FornecedoresCatalogoService {
  private readonly http = inject(HttpClient);
  private readonly api  = `${environment.apiUrl}/user/fornecedores-catalogo`;

  listar(params: ListarFornecedoresParams = {}): Observable<PagedResult<FornecedorModel>> {
    let httpParams = new HttpParams();
    if (params.search)              httpParams = httpParams.set('search',   params.search);
    if (params.ativo !== undefined) httpParams = httpParams.set('ativo',    String(params.ativo));
    if (params.page)                httpParams = httpParams.set('page',     String(params.page));
    if (params.pageSize)            httpParams = httpParams.set('pageSize', String(params.pageSize));

    return this.http
      .get<PagedResult<FornecedorModel>>(this.api, { params: httpParams })
      .pipe(catchError(this.handleError));
  }

  obter(id: number): Observable<FornecedorModel> {
    return this.http
      .get<FornecedorModel>(`${this.api}/${id}`)
      .pipe(catchError(this.handleError));
  }

  criar(dto: FornecedorCreateDto): Observable<FornecedorModel> {
    return this.http
      .post<FornecedorModel>(this.api, dto)
      .pipe(catchError(this.handleError));
  }

  atualizar(id: number, dto: FornecedorUpdateDto): Observable<FornecedorModel> {
    return this.http
      .put<FornecedorModel>(`${this.api}/${id}`, dto)
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
      message = 'Sem ligação ao servidor.';
    } else if (error.status === 400) {
      const body = error.error;
      if (body?.errors) {
        const msgs = Object.values(body.errors as Record<string, string[]>).flat().join(' ');
        message = msgs || body.message || 'Dados inválidos.';
      } else {
        message = body?.message || 'Pedido inválido.';
      }
    } else if (error.status === 401) {
      message = 'Sessão expirada. Por favor autentique-se novamente.';
    } else if (error.status === 404) {
      message = error.error?.message || 'Fornecedor não encontrado.';
    } else if (error.status === 409) {
      message = error.error?.message || 'Conflito: já existe um registo com estes dados.';
    } else if (error.status >= 500) {
      message = 'Erro interno do servidor. Tente novamente mais tarde.';
    }

    return throwError(() => ({ status: error.status, message }));
  }
}
