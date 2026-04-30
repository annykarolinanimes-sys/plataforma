import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface GuiaItem {
  id?: number;
  produtoId: number;
  produtoSku?: string;
  produtoNome?: string;
  quantidade: number;
  pesoUnitario?: number;
  pesoTotal?: number;
  volumeUnitario?: number;
  volumeTotal?: number;
  lote?: string;
  observacoes?: string;
}

export interface Guia {
  id: number;
  numeroGuia: string;
  tipo: string;
  status: string;
  dataEmissao: string;
  atribuicaoId?: number;
  atribuicaoNumero?: string;
  clienteId?: number;
  clienteNome?: string;
  clienteNif?: string;
  clienteMorada?: string;
  clienteContacto?: string;
  transportadoraId?: number;
  transportadoraNome?: string;
  transportadoraNif?: string;
  enderecoOrigem?: string;
  enderecoDestino?: string;
  totalItens: number;
  pesoTotalKg: number;
  volumeTotalM3: number;
  totalVolumes: number;
  dataPrevistaEntrega?: string;
  dataEntregaReal?: string;
  observacoes?: string;
  instrucoesEspeciais?: string;
  criadoEm: string;
  atualizadoEm: string;
  itens: GuiaItem[];
}

export interface GuiaCreateDto {
  tipo: string;
  atribuicaoId?: number;
  clienteId?: number;
  transportadoraId?: number;
  enderecoOrigem?: string;
  enderecoDestino?: string;
  dataPrevistaEntrega?: string;
  observacoes?: string;
  instrucoesEspeciais?: string;
  itens: Omit<GuiaItem, 'id' | 'produtoSku' | 'produtoNome' | 'pesoTotal' | 'volumeTotal'>[];
}

export interface GuiaUpdateDto {
  status?: string;
  dataPrevistaEntrega?: string;
  dataEntregaReal?: string;
  observacoes?: string;
  instrucoesEspeciais?: string;
  itens?: GuiaItem[];
}

export interface PagedResult<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface ListarGuiasParams {
  tipo?: string;
  status?: string;
  search?: string;
  page?: number;
  pageSize?: number;
}

@Injectable({ providedIn: 'root' })
export class GuiasService {
  private readonly http = inject(HttpClient);
  private readonly api = `${environment.apiUrl}/user/guias`;

  listar(params: ListarGuiasParams = {}): Observable<PagedResult<Guia>> {
    let httpParams = new HttpParams();
    if (params.tipo) httpParams = httpParams.set('tipo', params.tipo);
    if (params.status) httpParams = httpParams.set('status', params.status);
    if (params.search) httpParams = httpParams.set('search', params.search);
    if (params.page) httpParams = httpParams.set('page', params.page);
    if (params.pageSize) httpParams = httpParams.set('pageSize', params.pageSize);
    return this.http.get<PagedResult<Guia>>(this.api, { params: httpParams });
  }

  obter(id: number): Observable<Guia> {
    return this.http.get<Guia>(`${this.api}/${id}`);
  }

  criar(dto: GuiaCreateDto): Observable<Guia> {
    return this.http.post<Guia>(this.api, dto);
  }

  atualizar(id: number, dto: GuiaUpdateDto): Observable<Guia> {
    return this.http.put<Guia>(`${this.api}/${id}`, dto);
  }

  imprimir(id: number): Observable<Blob> {
    return this.http.post(`${this.api}/${id}/imprimir`, {}, { responseType: 'blob' });
  }

  deletar(id: number): Observable<{ message: string }> {
    return this.http.delete<{ message: string }>(`${this.api}/${id}`);
  }

  // Dados auxiliares
  obterProdutos(): Observable<{ id: number; sku: string; nome: string; pesoUnitario: number; volumeUnitario: number }[]> {
    return this.http.get<{ items: any[] }>(`${environment.apiUrl}/user/produtos?pageSize=100`).pipe(
      map(res => res.items.map(p => ({
        id: p.id,
        sku: p.sku,
        nome: p.nome,
        pesoUnitario: p.pesoUnitario || 0,
        volumeUnitario: p.volumeUnitario || 0
      })))
    );
  }

  obterClientes(): Observable<{ id: number; nome: string; contribuinte: string; morada: string; telefone: string }[]> {
    return this.http.get<{ items: any[] }>(`${environment.apiUrl}/user/clientes-catalogo?pageSize=100`).pipe(
      map(res => res.items.map(c => ({ id: c.id, nome: c.nome, contribuinte: c.contribuinte || '', morada: c.morada || '', telefone: c.telefone || '' })))
    );
  }

  obterTransportadoras(): Observable<{ id: number; nome: string; nif: string }[]> {
    return this.http.get<{ items: any[] }>(`${environment.apiUrl}/user/transportadoras-catalogo?pageSize=100`).pipe(
      map(res => res.items.map(t => ({ id: t.id, nome: t.nome, nif: t.nif || '' })))
    );
  }

  obterAtribuicoes(): Observable<{ id: number; numeroAtribuicao: string; clienteNome: string; enderecoOrigem: string; enderecoDestino: string }[]> {
    return this.http.get<{ items: any[] }>(`${environment.apiUrl}/user/atribuicoes?status=Concluida&pageSize=100`).pipe(
      map(res => res.items.map(a => ({
        id: a.id,
        numeroAtribuicao: a.numeroAtribuicao,
        clienteNome: a.clienteNome || '',
        enderecoOrigem: a.enderecoOrigem || '',
        enderecoDestino: a.enderecoDestino || ''
      })))
    );
  }
}