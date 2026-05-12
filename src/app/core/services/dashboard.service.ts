// src/app/features/dashboard/dashboard.service.ts

import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, catchError, of, forkJoin } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import {DashboardStats, AtividadeRecente,ViagemEmCurso,IncidentePendente,FaturaRecente,PaginatedResponse} from '../../features/dashboard.component/dashboard.model';

@Injectable({
  providedIn: 'root'
})
export class DashboardService {
  private http = inject(HttpClient);
  private apiUrl = `${environment.apiUrl}/user`;

  /**
   * Obtém as estatísticas gerais do dashboard
   */
  getDashboardStats(): Observable<DashboardStats> {
    return this.http.get<DashboardStats>(`${this.apiUrl}/dashboard/stats`).pipe(
      catchError(error => {
        console.error('Erro ao carregar estatísticas:', error);
        return of({
          valorTotalFaturasMes: 0,
          viagensAtivas: 0,
          totalClientes: 0,
          incidentesPendentes: 0
        });
      })
    );
  }

  /**
   * Obtém atividades recentes com limite configurável
   */
  getAtividadesRecentes(limite: number = 5): Observable<AtividadeRecente[]> {
    const params = new HttpParams().set('limite', limite.toString());
    
    return this.http.get<AtividadeRecente[]>(`${this.apiUrl}/dashboard/atividades-recentes`, { params }).pipe(
      catchError(error => {
        console.error('Erro ao carregar atividades recentes:', error);
        // Fallback para dados mock em desenvolvimento
        return of(this.getMockAtividades());
      })
    );
  }

  /**
   * Obtém viagens em curso com paginação
   */
  getViagensEmCurso(page: number = 1, pageSize: number = 5): Observable<PaginatedResponse<ViagemEmCurso>> {
    const params = new HttpParams()
      .set('status', 'EmCurso')
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());

    return this.http.get<PaginatedResponse<ViagemEmCurso>>(`${this.apiUrl}/gestao-viagens`, { params }).pipe(
      catchError(error => {
        console.error('Erro ao carregar viagens:', error);
        return of({ items: [], total: 0, page: 1, pageSize: 5 });
      })
    );
  }

  /**
   * Obtém incidentes pendentes com paginação
   */
  getIncidentesPendentes(page: number = 1, pageSize: number = 5): Observable<PaginatedResponse<IncidentePendente>> {
    const params = new HttpParams()
      .set('status', 'Aberto')
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());

    return this.http.get<PaginatedResponse<IncidentePendente>>(`${this.apiUrl}/incidentes`, { params }).pipe(
      catchError(error => {
        console.error('Erro ao carregar incidentes:', error);
        return of({ items: [], total: 0, page: 1, pageSize: 5 });
      })
    );
  }

  /**
   * Obtém faturas recentes
   */
  getFaturasRecentes(pageSize: number = 5): Observable<FaturaRecente[]> {
    const params = new HttpParams().set('pageSize', pageSize.toString());
    
    return this.http.get<FaturaRecente[]>(`${this.apiUrl}/faturas`, { params }).pipe(
      catchError(error => {
        console.error('Erro ao carregar faturas:', error);
        return of([]);
      })
    );
  }

  /**
   * Calcula o total faturado no mês atual
   */
  calcularTotalFaturasMes(faturas: FaturaRecente[]): number {
    const mesAtual = new Date().getMonth();
    const anoAtual = new Date().getFullYear();
    
    return faturas
      .filter(f => {
        const dataDoc = new Date(f.dataDoc);
        return dataDoc.getMonth() === mesAtual && 
               dataDoc.getFullYear() === anoAtual && 
               f.estado === 'Paga';
      })
      .reduce((sum, f) => sum + f.valorTotal, 0);
  }

  /**
   * Carrega todos os dados do dashboard em paralelo
   */
  carregarDadosDashboard() {
    return forkJoin({
      stats: this.getDashboardStats(),
      atividades: this.getAtividadesRecentes(5),
      viagens: this.getViagensEmCurso(1, 5),
      incidentes: this.getIncidentesPendentes(1, 5),
      faturas: this.getFaturasRecentes(5)
    });
  }

  /**
   * Dados mock para desenvolvimento
   */
  private getMockAtividades(): AtividadeRecente[] {
    return [
      {
        id: 1,
        titulo: 'Criação de fatura #FT/2025/0001',
        tipo: 'fatura',
        status: 'concluido',
        data: new Date(Date.now() - 25 * 60000).toISOString(),
        usuario: 'Admin'
      },
      {
        id: 2,
        titulo: 'Atribuição de viagem #ATRIB/2025/02/0001',
        tipo: 'atribuicao',
        status: 'em_andamento',
        data: new Date(Date.now() - 60 * 60000).toISOString(),
        usuario: 'João Silva'
      },
      {
        id: 3,
        titulo: 'Início de viagem #V/2025/02/0001',
        tipo: 'viagem',
        status: 'concluido',
        data: new Date(Date.now() - 120 * 60000).toISOString(),
        usuario: 'Carlos Santos'
      },
      {
        id: 4,
        titulo: 'Reporte de incidente #INC/2025/0001',
        tipo: 'incidente',
        status: 'pendente',
        data: new Date(Date.now() - 180 * 60000).toISOString(),
        usuario: 'Maria Oliveira'
      },
      {
        id: 5,
        titulo: 'Cadastro de novo produto #PROD/2025/0001',
        tipo: 'produto',
        status: 'concluido',
        data: new Date(Date.now() - 240 * 60000).toISOString(),
        usuario: 'Admin'
      }
    ];
  }
}