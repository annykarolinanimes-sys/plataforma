import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface Documento {
  id: number;
  nome: string;
  pathUrl: string;
  tipo: string;
  tamanhoBytes: number;
  tamanhoFormatado: string;
  usuarioId: number;
  envioId?: number;
  dataUpload: string;
  dataAbertura?: string;
}

@Injectable({
  providedIn: 'root',
})
export class DocumentosService {
  private http = inject(HttpClient);
  private api = environment.apiUrl;

  listar(tipo?: string, search?: string): Observable<Documento[]> {
    let params = new URLSearchParams();
    if (tipo) params.set('tipo', tipo);
    if (search) params.set('search', search);
    const url = params.toString() ? `${this.api}/documentos?${params}` : `${this.api}/documentos`;
    return this.http.get<Documento[]>(url);
  }

  baixar(id: number): Observable<Blob> {
    return this.http.get(`${this.api}/documentos/${id}/download`, { responseType: 'blob' });
  }
}

