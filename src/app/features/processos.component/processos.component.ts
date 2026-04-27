// processos.component.ts
import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';

@Component({
  selector: 'app-processos',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="processos-container">
      <div class="page-header">
        <h1><i class="las la-chalkboard"></i> {{ titulo }}</h1>
        <p>Gestão de {{ titulo }}</p>
      </div>
      <div class="processos-content">
        <p>Conteúdo do processo: <strong>{{ processo }}</strong></p>
        <p>Aqui será integrada a funcionalidade específica.</p>
      </div>
    </div>
  `,
  styles: [`
    .processos-container { padding: 24px; max-width: 1400px; margin: 0 auto; }
    .page-header { margin-bottom: 24px; padding: 20px 24px; background: white; border-radius: 20px; }
    .processos-content { background: white; border-radius: 20px; padding: 24px; min-height: 400px; }
  `]
})
export class ProcessosComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  
  processo = '';
  titulo = '';

  ngOnInit(): void {
    this.route.queryParams.subscribe(params => {
      this.processo = params['processo'] || 'processos';
      this.titulo = this.getTitulo(this.processo);
    });
  }

  private getTitulo(processo: string): string {
    const titulos: Record<string, string> = {
      'entradas': 'Entradas',
      'crossdock': 'Cross Dock',
      'putaway': 'Put Away',
      'paletes': 'Paletes e Contentores',
      'picking': 'Picking',
      'reabastecimento': 'Reabastecimento',
      'auditoria': 'Auditoria',
      'inventarios': 'Inventários',
      'embarque': 'Embarque',
      'manufactura': 'Manufactura',
      'incidentes': 'Controle de Incidentes',
      'ativos': 'Controle de Ativos'
    };
    return titulos[processo] || 'Processos Logísticos';
  }
}