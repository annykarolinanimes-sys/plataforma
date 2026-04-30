import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';


export interface ClienteModel {
  id: number;
  codigo: string;
  nome: string;
  contribuinte?: string;
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

export interface ClienteCreateDto {
  codigo?: string;  // Agora é opcional - será gerado pelo backend
  nome: string;
  contribuinte?: string;
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

export interface ClienteUpdateDto extends ClienteCreateDto {
  ativo: boolean;
}

export interface PagedResult<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface ListarClientesParams {
  search?: string;
  ativo?: boolean;
  page?: number;
  pageSize?: number;
}


@Injectable({ providedIn: 'root' })
export class ClientesCatalogoService {
  private readonly http = inject(HttpClient);
  private readonly api  = `${environment.apiUrl}/user/clientes-catalogo`;

  listar(params: ListarClientesParams = {}): Observable<PagedResult<ClienteModel>> {
    let httpParams = new HttpParams();
    if (params.search)              httpParams = httpParams.set('search',   params.search);
    if (params.ativo !== undefined) httpParams = httpParams.set('ativo',    String(params.ativo));
    if (params.page)                httpParams = httpParams.set('page',     String(params.page));
    if (params.pageSize)            httpParams = httpParams.set('pageSize', String(params.pageSize));

    return this.http
      .get<PagedResult<ClienteModel>>(this.api, { params: httpParams })
      .pipe(catchError(this.handleError));
  }

  obter(id: number): Observable<ClienteModel> {
    return this.http
      .get<ClienteModel>(`${this.api}/${id}`)
      .pipe(catchError(this.handleError));
  }

  criar(dto: ClienteCreateDto): Observable<ClienteModel> {
    return this.http
      .post<ClienteModel>(this.api, dto)
      .pipe(catchError(this.handleError));
  }

  atualizar(id: number, dto: ClienteUpdateDto): Observable<ClienteModel> {
    return this.http
      .put<ClienteModel>(`${this.api}/${id}`, dto)
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

  obterProximoCodigo(): Observable<{ codigo: string }> {
  return this.http.get<{ codigo: string }>(`${this.api}/proximo-codigo`);
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
      message = error.error?.message || 'Cliente não encontrado.';
    } else if (error.status === 409) {
      message = error.error?.message || 'Conflito: já existe um registo com estes dados.';
    } else if (error.status >= 500) {
      message = 'Erro interno do servidor. Tente novamente mais tarde.';
    }

    return throwError(() => ({ status: error.status, message }));
  }
}
