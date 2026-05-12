import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, map, catchError, of } from 'rxjs';
import { environment } from '../../../environments/environment';

// Interfaces reforçadas para baterem com o componente
export interface ProdutoListResponse {
  id: number;
  sku: string;
  nome: string;
  descricao?: string;
}

export interface RecepcaoListResponse {
  id: number;
  numeroRecepcao: string;
  fornecedor: string;
  quantidadePendente: number; // Alterado para suportar a sugestão automática
  status?: string;
}

export interface EncomendaListResponse {
  id: number;
  numeroEncomenda: string;
  clienteNome: string;
}

@Injectable({ providedIn: 'root' })
export class EtiquetasService {
  private readonly http = inject(HttpClient);
  private readonly api = `${environment.apiUrl}/user`; 

  obterProdutos(search?: string): Observable<ProdutoListResponse[]> {
    let params = new HttpParams().set('pageSize', '50');
    if (search) params = params.set('search', search);

    return this.http.get<{ items: any[] }>(`${this.api}/produtos`, { params }).pipe(
      map(res => res.items.map(p => ({
        id: p.id,
        sku: p.sku,
        nome: p.nome,
        descricao: p.descricao
      }))),
      catchError(() => of([])) 
    );
  }

  obterRececoes(search?: string): Observable<RecepcaoListResponse[]> {
    let params = new HttpParams().set('pageSize', '50');
    if (search) params = params.set('search', search);

    return this.http.get<{ items: any[] }>(`${this.api}/recepcao`, { params }).pipe(
      map(res => res.items.map(r => ({
        id: r.id,
        numeroRecepcao: r.numeroRecepcao,
        fornecedor: r.fornecedor,
        quantidadePendente: r.quantidadePendente || r.itensPendentes || r.totalItens || 0,
        status: r.status
      }))),
      catchError(() => of([]))
    );
  }

  obterEncomendas(search?: string): Observable<EncomendaListResponse[]> {
    let params = new HttpParams().set('pageSize', '50');
    if (search) params = params.set('search', search);

    return this.http.get<{ items: any[] }>(`${this.api}/encomendas`, { params }).pipe(
      map(res => res.items.map(e => ({
        id: e.id,
        numeroEncomenda: e.numeroEncomenda,
        clienteNome: e.clienteNome || e.cliente?.nome || 'Cliente não identificado'
      }))),
      catchError(() => of([]))
    );
  }
}