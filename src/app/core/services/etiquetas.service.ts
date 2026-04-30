import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface EtiquetaResponse {
  id: string;
  codigo: string;
  tipo: string;
  dados: any;
  url: string;
}

@Injectable({ providedIn: 'root' })
export class EtiquetasService {
  private readonly http = inject(HttpClient);
  private readonly api = environment.apiUrl;

  obterProdutos(search?: string): Observable<{ id: number; sku: string; nome: string }[]> {
    let url = `${this.api}/user/produtos?pageSize=100`;
    if (search) url += `&search=${search}`;
    return this.http.get<{ items: any[] }>(url).pipe(
      map(res => res.items.map(p => ({ id: p.id, sku: p.sku, nome: p.nome })))
    );
  }

  obterRececoes(): Observable<{ id: number; numeroRecepcao: string; fornecedor: string }[]> {
    return this.http.get<{ items: any[] }>(`${this.api}/user/recepcao?pageSize=100`).pipe(
      map(res => res.items.map(r => ({ id: r.id, numeroRecepcao: r.numeroRecepcao, fornecedor: r.fornecedor })))
    );
  }

  obterEncomendas(): Observable<{ id: number; numeroEncomenda: string; clienteNome: string }[]> {
    // Se não tiver endpoint de encomendas, retorna array vazio
    return this.http.get<{ items: any[] }>(`${this.api}/user/encomendas?pageSize=100`).pipe(
      map(res => res.items.map(e => ({ id: e.id, numeroEncomenda: e.numeroEncomenda, clienteNome: e.clienteNome })))
    ).pipe(
      map(data => data || [])
    );
  }

  obterPaletes(): Observable<{ id: number; codigo: string; localizacao: string }[]> {
    // Se não tiver endpoint de paletes, retorna array vazio
    return this.http.get<{ items: any[] }>(`${this.api}/user/paletes?pageSize=100`).pipe(
      map(res => res.items.map(p => ({ id: p.id, codigo: p.codigo, localizacao: p.localizacao })))
    ).pipe(
      map(data => data || [])
    );
  }
}