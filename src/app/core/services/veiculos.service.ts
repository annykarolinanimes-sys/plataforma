import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';

export interface Veiculo {
  id: number;
  matricula: string;
  marca: string;
  modelo: string;
  cor?: string;
  ano?: number;
  vin?: string;
  tipoCombustivel?: string;
  cilindrada?: number;
  potencia?: number;
  lugares?: number;
  peso?: number;
  proprietarioId?: number;
  proprietario?: { id: number; nome: string; codigo: string };
  ativo: boolean;
  observacoes?: string;
  criadoEm: string;
  atualizadoEm: string;
}

export interface PagedResult<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface ListarVeiculosParams {
  search?: string;
  combustivel?: string;
  ativo?: boolean;
  page?: number;
  pageSize?: number;
}

@Injectable({ providedIn: 'root' })
export class VeiculosService {
  private readonly http = inject(HttpClient);
  private readonly api  = `${environment.apiUrl}/user/veiculos`;

  listar(params: ListarVeiculosParams = {}): Observable<PagedResult<Veiculo>> {
    let httpParams = new HttpParams();
    if (params.search)              httpParams = httpParams.set('search',      params.search);
    if (params.combustivel)         httpParams = httpParams.set('combustivel', params.combustivel);
    if (params.ativo !== undefined) httpParams = httpParams.set('ativo',       String(params.ativo));
    if (params.page)                httpParams = httpParams.set('page',        String(params.page));
    if (params.pageSize)            httpParams = httpParams.set('pageSize',    String(params.pageSize));

    return this.http
      .get<PagedResult<Veiculo>>(this.api, { params: httpParams })
      .pipe(catchError(this.handleError));
  }

  obter(id: number): Observable<Veiculo> {
    return this.http.get<Veiculo>(`${this.api}/${id}`).pipe(catchError(this.handleError));
  }

  criar(data: Veiculo): Observable<Veiculo> {
    return this.http.post<Veiculo>(this.api, data).pipe(catchError(this.handleError));
  }

  atualizar(id: number, data: Veiculo): Observable<Veiculo> {
    return this.http.put<Veiculo>(`${this.api}/${id}`, data).pipe(catchError(this.handleError));
  }

  deletar(id: number): Observable<{ message: string }> {
    return this.http.delete<{ message: string }>(`${this.api}/${id}`).pipe(catchError(this.handleError));
  }

  ativar(id: number): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.api}/${id}/ativar`, {}).pipe(catchError(this.handleError));
  }

  private handleError(error: HttpErrorResponse): Observable<never> {
    let message = 'Ocorreu um erro inesperado.';
    if (error.status === 0)        message = 'Sem ligação ao servidor.';
    else if (error.status === 409) message = error.error?.message ?? 'Matrícula já existe.';
    else if (error.status === 404) message = error.error?.message ?? 'Veículo não encontrado.';
    else if (error.status >= 500)  message = 'Erro interno do servidor.';
    else                           message = error.error?.message ?? message;
    return throwError(() => ({ status: error.status, message }));
  }
}