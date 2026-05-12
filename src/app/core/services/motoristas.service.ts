import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';

export interface Motorista {
  id: number;
  nome: string;
  telefone: string;
  cartaConducao: string;
  transportadoraId: string;
  ativo: boolean;
  criadoEm: string;
  atualizadoEm: string;
}

export interface MotoristaCreateDto {
  nome: string;
  telefone: string;
  cartaConducao: string;
  transportadoraId: string;
}

export interface MotoristaUpdateDto {
  nome: string;
  telefone: string;
  cartaConducao: string;
  ativo: boolean;
}

export interface PagedResult<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface ListarMotoristasParams {
  search?: string;
  ativo?: boolean;
  page?: number;
  pageSize?: number;
  orderBy?: string;
  orderDir?: 'asc' | 'desc';
}

@Injectable({ providedIn: 'root' })
export class MotoristasService {
  private readonly http = inject(HttpClient);
  private readonly api = `${environment.apiUrl}/motoristas`;

  listar(params: ListarMotoristasParams = {}): Observable<PagedResult<Motorista>> {
    let p = new HttpParams();
    if (params.search) p = p.set('search', params.search);
    if (params.ativo !== undefined) p = p.set('ativo', String(params.ativo));
    if (params.page) p = p.set('page', String(params.page));
    if (params.pageSize) p = p.set('pageSize', String(params.pageSize));
    if (params.orderBy) p = p.set('orderBy', params.orderBy);
    if (params.orderDir) p = p.set('orderDir', params.orderDir);

    return this.http
      .get<PagedResult<Motorista>>(this.api, { params: p })
      .pipe(catchError(this.handleError));
  }

  obter(id: number): Observable<Motorista> {
    return this.http
      .get<Motorista>(`${this.api}/${id}`)
      .pipe(catchError(this.handleError));
  }

  criar(dto: MotoristaCreateDto): Observable<Motorista> {
    return this.http
      .post<Motorista>(this.api, dto)
      .pipe(catchError(this.handleError));
  }

  atualizar(id: number, dto: MotoristaUpdateDto): Observable<Motorista> {
    return this.http
      .put<Motorista>(`${this.api}/${id}`, dto)
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
    console.error('Erro detalhado:', error);

    let message = 'Ocorreu um erro inesperado.';

    if (error.status === 0) {
      message = 'Sem ligação ao servidor. Verifique a sua rede.';
    } else if (error.status === 400) {
      const body = error.error;
      if (body?.errors) {
        const msgs = Object.values(body.errors as Record<string, string[]>)
          .flat().join(' ');
        message = msgs || body.message || 'Dados inválidos.';
      } else if (body?.message) {
        message = body.message;
      } else {
        message = 'Dados inválidos. Verifique os campos do formulário.';
      }
    } else if (error.status === 401) {
      message = 'Sessão expirada. Por favor autentique-se novamente.';
    } else if (error.status === 403) {
      message = 'Não tem permissão para esta operação.';
    } else if (error.status === 404) {
      message = error.error?.message || 'Motorista não encontrado.';
    } else if (error.status === 409) {
      message = error.error?.message || 'Já existe um registo com estes dados.';
    } else if (error.status >= 500) {
      message = 'Erro interno do servidor. Tente novamente mais tarde.';
    }

    return throwError(() => ({ status: error.status, message, details: error.error }));
  }
}