// src/app/core/services/invoice.service.ts
import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, finalize } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface InvoiceItem {
  id?: number;
  marca: string;
  modelo: string;
  cor: string;
  matricula: string;
  quantidade: number;
  precoUnitario: number;
  subtotal?: number;
}

export interface Invoice {
  id: number;
  numeroFatura: string;
  clienteId?: number;
  clienteNome: string;
  clienteContacto: string;
  clienteEmail?: string;
  clienteMorada?: string;
  clienteNif?: string;
  dataDoc: string;
  estado: string;
  valorTotal: number;
  observacoes?: string;
  quemExecutou?: string;
  horasTrabalho?: number;
  materialUtilizado?: string;
  criadoEm: string;
  itens: InvoiceItem[];
}

export interface CreateInvoiceRequest {
  clienteId?: number;
  clienteNome: string;
  clienteContacto: string;
  clienteEmail?: string;
  clienteMorada?: string;
  clienteNif?: string;
  dataDoc: string;
  estado: string;
  observacoes?: string;
  quemExecutou?: string;
  horasTrabalho?: number;
  materialUtilizado?: string;
  itens: InvoiceItem[];
}

@Injectable({ providedIn: 'root' })
export class InvoiceService {
  private http = inject(HttpClient);
  private api = `${environment.apiUrl}/user/faturas`;

  private invoicesSignal = signal<Invoice[]>([]);
  readonly invoices = this.invoicesSignal.asReadonly();
  private loadingSignal = signal(false);
  readonly isLoading = this.loadingSignal.asReadonly();

  listar(estado?: string, search?: string): Observable<Invoice[]> {
    this.loadingSignal.set(true);
    let params = new URLSearchParams();
    if (estado) params.set('estado', estado);
    if (search) params.set('search', search);
    
    const url = params.toString() ? `${this.api}?${params}` : this.api;
    console.log('URL da requisição:', url);
    
    return this.http.get<Invoice[]>(url).pipe(finalize(() => this.loadingSignal.set(false)));
  }

  obter(id: number): Observable<Invoice> {
    return this.http.get<Invoice>(`${this.api}/${id}`);
  }

  criar(data: CreateInvoiceRequest): Observable<Invoice> {
    return this.http.post<Invoice>(this.api, data);
  }

  atualizar(id: number, data: Partial<CreateInvoiceRequest>): Observable<Invoice> {
    return this.http.put<Invoice>(`${this.api}/${id}`, data);
  }

  deletar(id: number): Observable<{ message: string }> {
    return this.http.delete<{ message: string }>(`${this.api}/${id}`);
  }

  gerarPdf(id: number): Observable<Blob> {
    return this.http.get(`${this.api}/${id}/pdf`, { responseType: 'blob' });
  }
}