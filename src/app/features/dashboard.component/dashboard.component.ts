
import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink, Router } from '@angular/router';
import { DashboardService } from '../../core/services/dashboard.service';
import { AuthService } from '../../core/services/auth.service';
import {
  StatCard,
  AtividadeRecente,
  ViagemEmCurso,
  IncidentePendente,
  FaturaRecente
} from './dashboard.model';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './dashboard.component.html',
  styleUrls: ['./dashboard.component.css']
})
export class DashboardComponent implements OnInit {
  private dashboardService = inject(DashboardService);
  private router = inject(Router);
  readonly auth = inject(AuthService);

  // Signals
  statCards = signal<StatCard[]>([
    { 
      icon: 'la-chart-line', 
      label: 'Faturação Mensal', 
      value: '€ 0', 
      color: 'blue', 
      trend: 12, 
      trendDirection: 'up' 
    },
    { 
      icon: 'la-truck', 
      label: 'Viagens Ativas', 
      value: 0, 
      color: 'orange' 
    },
    { 
      icon: 'la-users', 
      label: 'Clientes Ativos', 
      value: 0, 
      color: 'green' 
    },
    { 
      icon: 'la-exclamation-triangle', 
      label: 'Incidentes', 
      value: 0, 
      color: 'red', 
      trend: 5, 
      trendDirection: 'down' 
    },
  ]);

  isLoadingAtividades = signal(false);
  isLoadingViagens = signal(false);
  isLoadingIncidentes = signal(false);
  isLoadingFaturas = signal(false);

  atividadesRecentes = signal<AtividadeRecente[]>([]);
  viagensEmCurso = signal<ViagemEmCurso[]>([]);
  incidentesPendentes = signal<IncidentePendente[]>([]);
  faturasRecentes = signal<FaturaRecente[]>([]);
  faturasMes = signal<number>(0);

  ngOnInit(): void {
    this.carregarDashboard();
  }

  carregarDashboard(): void {
    this.carregarEstatisticas();
    this.carregarAtividadesRecentes();
    this.carregarViagensEmCurso();
    this.carregarIncidentesPendentes();
    this.carregarFaturasRecentes();
  }

  private carregarEstatisticas(): void {
    this.dashboardService.getDashboardStats().subscribe({
      next: (data) => {
        this.statCards.update(cards => 
          cards.map(card => {
            if (card.label === 'Faturação Mensal') {
              return { ...card, value: `€ ${data.valorTotalFaturasMes?.toFixed(2) || '0.00'}` };
            }
            if (card.label === 'Viagens Ativas') {
              return { ...card, value: data.viagensAtivas || 0 };
            }
            if (card.label === 'Clientes Ativos') {
              return { ...card, value: data.totalClientes || 0 };
            }
            if (card.label === 'Incidentes') {
              return { ...card, value: data.incidentesPendentes || 0 };
            }
            return card;
          })
        );
      }
    });
  }

  private carregarAtividadesRecentes(): void {
    this.isLoadingAtividades.set(true);
    
    this.dashboardService.getAtividadesRecentes(5).subscribe({
      next: (data) => {
        this.atividadesRecentes.set(data);
        this.isLoadingAtividades.set(false);
      },
      error: () => {
        this.isLoadingAtividades.set(false);
      }
    });
  }

  private carregarViagensEmCurso(): void {
    this.isLoadingViagens.set(true);
    
    this.dashboardService.getViagensEmCurso(1, 5).subscribe({
      next: (data) => {
        this.viagensEmCurso.set(data.items || []);
        this.isLoadingViagens.set(false);
      },
      error: () => {
        this.isLoadingViagens.set(false);
      }
    });
  }

  private carregarIncidentesPendentes(): void {
    this.isLoadingIncidentes.set(true);
    
    this.dashboardService.getIncidentesPendentes(1, 5).subscribe({
      next: (data) => {
        this.incidentesPendentes.set(data.items || []);
        this.isLoadingIncidentes.set(false);
      },
      error: () => {
        this.isLoadingIncidentes.set(false);
      }
    });
  }

  private carregarFaturasRecentes(): void {
    this.isLoadingFaturas.set(true);
    
    this.dashboardService.getFaturasRecentes(5).subscribe({
      next: (data) => {
        this.faturasRecentes.set(data);
        this.faturasMes.set(this.dashboardService.calcularTotalFaturasMes(data));
        this.isLoadingFaturas.set(false);
      },
      error: () => {
        this.isLoadingFaturas.set(false);
      }
    });
  }

  getActivityIcon(tipo: string): string {
    const icons: Record<string, string> = {
      'fatura': 'la-file-invoice',
      'atribuicao': 'la-tasks',
      'viagem': 'la-truck',
      'incidente': 'la-exclamation-triangle',
      'produto': 'la-box'
    };
    return icons[tipo] || 'la-history';
  }

  getActivityIconClass(tipo: string): string {
    const classes: Record<string, string> = {
      'fatura': 'icon-blue',
      'atribuicao': 'icon-orange',
      'viagem': 'icon-green',
      'incidente': 'icon-red'
    };
    return classes[tipo] || 'icon-gray';
  }

  formatRelativeTime(dateStr: string): string {
    const date = new Date(dateStr);
    const now = new Date();
    const diffMs = now.getTime() - date.getTime();
    const diffMins = Math.floor(diffMs / 60000);
    const diffHours = Math.floor(diffMs / 3600000);
    const diffDays = Math.floor(diffMs / 86400000);

    if (diffMins < 1) return 'agora';
    if (diffMins < 60) return `${diffMins} min`;
    if (diffHours < 24) return `${diffHours} h`;
    if (diffDays < 7) return `${diffDays} d`;
    return date.toLocaleDateString('pt-PT');
  }

  formatDate(dateStr: string): string {
    return new Date(dateStr).toLocaleDateString('pt-PT');
  }

  goToViagem(id: number): void {
    this.router.navigate(['/gestao-viagens'], { queryParams: { id } });
  }

  goToFatura(id: number): void {
    this.router.navigate(['/faturas'], { queryParams: { id } });
  }
}