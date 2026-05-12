
export interface StatCard {
  icon: string;
  label: string;
  value: string | number;
  color: string;
  trend?: number;
  trendDirection?: 'up' | 'down';
}

export interface AtividadeRecente {
  id: number;
  titulo: string;
  tipo: 'fatura' | 'atribuicao' | 'viagem' | 'incidente' | 'produto';
  status: 'concluido' | 'em_andamento' | 'pendente';
  data: string;
  usuario: string;
}

export interface ViagemEmCurso {
  id: number;
  numeroViagem: string;
  origem: string;
  destino: string;
  progresso: number;
}

export interface IncidentePendente {
  id: number;
  titulo: string;
  tipo: string;
  gravidade: 'critica' | 'alta' | 'media' | 'baixa';
  dataOcorrencia: string;
}

export interface FaturaRecente {
  id: number;
  numeroFatura: string;
  clienteNome: string;
  valorTotal: number;
  dataDoc: string;
  estado: string;
}

export interface DashboardStats {
  valorTotalFaturasMes: number;
  viagensAtivas: number;
  totalClientes: number;
  incidentesPendentes: number;
}

export interface PaginatedResponse<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
}