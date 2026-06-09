

import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, finalize, map } from 'rxjs';
import { environment } from '../../../environments/environment';

// ── Interfaces ────────────────────────────────────────────────────────────────

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

export interface FaturaAnexo {
  id: number;
  nomeOriginal: string;
  mimeType: string;
  tamanhoBytes: number;
  tamanhoFormatado: string;
  criadoEm: string;
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
  anexos: FaturaAnexo[];   
}

export interface InvoiceListResult {
  total: number;
  page: number;
  pageSize: number;
  data: Invoice[];
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

// ── Service ───────────────────────────────────────────────────────────────────

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
    if (search)  params.set('search', search);

    const url = params.toString() ? `${this.api}?${params}` : this.api;

    return this.http
      .get<InvoiceListResult>(url)
      .pipe(
        map(result => result.data ?? []),
        finalize(() => this.loadingSignal.set(false))
      );
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

  // ── Métodos de Anexos ─────────────────────────────────────────────────────

  /** Faz upload de um ficheiro e associa-o à fatura. */
  uploadAnexo(faturaId: number, ficheiro: File): Observable<FaturaAnexo> {
    const formData = new FormData();
    formData.append('ficheiro', ficheiro);
    return this.http.post<FaturaAnexo>(`${this.api}/${faturaId}/anexos`, formData);
  }

  /** Descarrega um anexo como Blob. */
  downloadAnexo(faturaId: number, anexoId: number): Observable<Blob> {
    return this.http.get(`${this.api}/${faturaId}/anexos/${anexoId}`, {
      responseType: 'blob'
    });
  }

  /** Remove um anexo da fatura. */
  removerAnexo(faturaId: number, anexoId: number): Observable<{ message: string }> {
    return this.http.delete<{ message: string }>(
      `${this.api}/${faturaId}/anexos/${anexoId}`
    );
  }
}