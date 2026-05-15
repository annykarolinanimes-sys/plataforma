
export enum EstadoDocumento {
  Em_Analise = 'Em_Analise',
  Ativo      = 'Ativo',
  Quarentena = 'Quarentena',
  IntegridadeCompromissada = 'IntegridadeCompromissada',
  Eliminado  = 'Eliminado',
  Arquivado  = 'Arquivado',
}

export enum CategoriaDocumento {
  Contrato             = 'Contrato',
  Fatura               = 'Fatura',
  Proposta             = 'Proposta',
  Relatorio            = 'Relatorio',
  Comprovativo         = 'Comprovativo',
  IdentificacaoPessoal = 'IdentificacaoPessoal',
  CertificadoCompliance= 'CertificadoCompliance',
  AuditoriaInterna     = 'AuditoriaInterna',
  CorrespondenciaLegal = 'CorrespondenciaLegal',
  ManualProcedimento   = 'ManualProcedimento',
  Outro                = 'Outro',
}

export enum ContextoDocumento {
  Cliente    = 'Cliente',
  Fornecedor = 'Fornecedor',
  Colaborador= 'Colaborador',
  Interno    = 'Interno',
  Regulatorio= 'Regulatorio',
  Juridico   = 'Juridico',
}

export enum TipoOperacaoHistorico {
  Upload                  = 'Upload',
  Download                = 'Download',
  Visualizacao            = 'Visualizacao',
  Validacao               = 'Validacao',
  SoftDelete              = 'SoftDelete',
  Restauro                = 'Restauro',
  ScanAntivirus           = 'ScanAntivirus',
  VerificacaoIntegridade  = 'VerificacaoIntegridade',
  TransicaoEstado         = 'TransicaoEstado',
  Quarentena              = 'Quarentena',
  Encriptacao             = 'Encriptacao',
  RetencaoLegal           = 'RetencaoLegal',
  Arquivamento            = 'Arquivamento',
  AlteracaoMetadados      = 'AlteracaoMetadados',
  AcessoNegado            = 'AcessoNegado',
}

// ─── Interfaces ───────────────────────────────────────────────────────────────

export interface DocumentoResumo {
  id: string;
  nomeOriginal: string;
  mimeTypeValidado: string;
  tamanhoBytesFormatado: string;
  estado: EstadoDocumento;
  categoria: CategoriaDocumento;
  contexto: ContextoDocumento;
  versao: number;
  isLatest: boolean;
  validado: boolean;
  integridadeVerificada: boolean;
  createdAt: string;
  createdBy: string;
}

export interface DocumentoDetalhe extends DocumentoResumo {
  tenantId: string;
  correlationId: string;
  hashSHA256: string;
  hashCalculadoEm: string;
  tamanhoBytes: number;
  extensao: string;
  entidadeAssociadaId?: string;
  descricao?: string;
  razaoEstado?: string;
  scanAntivirusRealizado: boolean;
  resultadoScanAntivirus?: string;
  encriptado: boolean;
  retencaoLegalAtiva: boolean;
  dataExpiracaoRetencao?: string;
  isDeleted: boolean;
  deletedAt?: string;
  validadoPor?: string;
  validadoEm?: string;
  comentarioRejeicao?: string;
  rejeitadoPor?: string;
  rejeitadoEm?: string;
  documentoOrigemId?: string;
  versaoAnteriorId?: string;
  ultimaVerificacaoIntegridade?: string;
  updatedBy?: string;
  updatedAt?: string;
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

export interface UploadDocumentoResponse {
  documentoId: string;
  nomeOriginal: string;
  hashSHA256: string;
  mimeTypeDetectado: string;
  tamanhoBytes: number;
  versao: number;
  estado: EstadoDocumento;
  correlationId: string;
  mensagem: string;
}

export interface PagedResult<T> {
  items: T[];
  totalItens: number;
  pagina: number;
  tamanhoPagina: number;
  totalPaginas: number;
}

export interface ListarDocumentosQuery {
  pagina?: number;
  tamanhoPagina?: number;
  estado?: EstadoDocumento;
  categoria?: CategoriaDocumento;
  contexto?: ContextoDocumento;
  entidadeAssociadaId?: string;
  apenasLatest?: boolean;
  pesquisaNome?: string;
  criadoApos?: string;
  criadoAntes?: string;
}


export interface EstadoBadge {
  label: string;
  cssClass: string;    // badge--ativo | badge--analise | etc.
  icon: string;        // Las icon class
}

export interface EstadoStats {
  totalDocumentos: number;
  ativos: number;
  emAnalise: number;
  quarentena: number;
  integridadeOk: number;
}

// ─── Upload ───────────────────────────────────────────────────────────────────

export interface UploadProgress {
  ficheiro: File;
  progresso: number;         // 0–100
  estado: 'pendente' | 'a-carregar' | 'concluido' | 'erro';
  mensagem?: string;
  documentoId?: string;
}
