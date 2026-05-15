import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { TipoOperacaoHistorico, EstadoDocumento, CategoriaDocumento, ContextoDocumento } from '../../features/documentos/documentos.models';

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

export interface UploadPdfResponse {
  documentoId: string;
  nomeOriginal: string;
  hashSHA256: string;
  mimeTypeDetectado: string;
  tamanhoBytes: number;
  versao: number;
  estado: string;
  correlationId: string;
  mensagem: string;
  url: string;
}

export interface UploadProgress {
  ficheiro: File;
  progresso: number;         // 0–100
  estado: 'pendente' | 'a-carregar' | 'concluido' | 'erro';
  mensagem?: string;
  documentoId?: string;
}

export interface DocumentoHistoricoItem {
  id: string;
  tipoOperacao: TipoOperacaoHistorico;
  descricao: string;
  executadoPor: string;
  ipOrigem?: string;
  estadoAnterior?: string;
  estadoPosterior?: string;
  correlationId: string;
  ocorridoEm: string;
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

  /**
   * Envia um Blob PDF para o backend (e.g. relatórios, faturas, faturas gerados localmente).
   * O backend persiste no ECM e retorna metadados (documentoId, hashSHA256, url download).
   * 
   * @param blob Blob do PDF
   * @param fileName Nome do arquivo
   * @param categoria Categoria do documento (default: "Relatorio")
   * @param contexto Contexto do documento (default: "Interno")
   * @param descricao Descrição opcional
   */
  uploadPdfBlob(
    blob: Blob,
    fileName: string,
    categoria: string = 'Relatorio',
    contexto: string = 'Interno',
    descricao?: string
  ): Observable<UploadPdfResponse> {
    const formData = new FormData();
    formData.append('ficheiro', blob, fileName);
    formData.append('categoria', categoria);
    formData.append('contexto', contexto);
    if (descricao) {
      formData.append('descricao', descricao);
    }

    return this.http.post<UploadPdfResponse>(`${this.api}/documentos/upload-stream`, formData);
  }

  /**
   * Upload de um novo documento
   */
  upload(
    ficheiro: File,
    categoria: string,
    contexto: string,
    descricao?: string
  ): Observable<UploadProgress> {
    const formData = new FormData();
    formData.append('ficheiro', ficheiro);
    formData.append('categoria', categoria);
    formData.append('contexto', contexto);
    if (descricao) {
      formData.append('descricao', descricao);
    }

    return this.http.post<UploadProgress>(`${this.api}/documentos/upload-stream`, formData);
  }

  /**
   * Criar nova versão de um documento existente
   */
  novaVersao(
    documentoId: string,
    ficheiro: File,
    descricao?: string
  ): Observable<UploadProgress> {
    const formData = new FormData();
    formData.append('documentoId', documentoId);
    formData.append('ficheiro', ficheiro);
    if (descricao) {
      formData.append('descricao', descricao);
    }

    return this.http.post<UploadProgress>(`${this.api}/documentos/versoes`, formData);
  }

  /**
   * Obter histórico de operações de um documento
   */
  obterHistorico(documentoId: string): Observable<DocumentoHistoricoItem[]> {
    return this.http.get<DocumentoHistoricoItem[]>(`${this.api}/documentos/${documentoId}/historico`);
  }
}

