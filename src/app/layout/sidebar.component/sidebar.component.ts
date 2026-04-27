import { Component, input, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink, RouterLinkActive, Router } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-sidebar',
  standalone: true,
  imports: [CommonModule, RouterLink, RouterLinkActive],
  templateUrl: './sidebar.component.html',
  styleUrls: ['./sidebar.component.css']
})
export class SidebarComponent {
  collapsed = input<boolean>(false);
  auth = inject(AuthService);
  private router = inject(Router);

  processosMenuOpen = signal(false);
  catalogoMenuOpen  = signal(false);
  impressaoMenuOpen = signal(false);
  
  get isCollapsed(): boolean {
      return this.collapsed();
  }
  readonly navItems = [
    { icon: 'la-tachometer-alt', label: 'Dashboards', route: '/dashboard' },
    { icon: 'la-chalkboard', label: 'Processos', route: null, hasSubmenu: true, submenuKey: 'processos' },
    { icon: 'la-book', label: 'Catálogo', route: null, hasSubmenu: true, submenuKey: 'catalogo' },
    { icon: 'la-print', label: 'Impressão', route: null, hasSubmenu: true, submenuKey: 'impressao' },
    { icon: 'la-file-invoice', label: 'Faturas', route: '/faturas' },
    { icon: 'la-cog', label: 'Definições', route: '/definicao' },
  ];

  processosSubmenu = [
    { icon: 'la-box', label: 'Recepção', route: '/recepcao' },
    { icon: 'la-exchange-alt', label: 'Atribuições', route: '/processos', queryParams: { processo: 'atribuicao' } },
    { icon: 'la-route', label: 'Fecho de Viagem', route: '/processos', queryParams: { processo: 'fechoviagem' } },
    { icon: 'la-calculator', label: 'Gestão de Viagens', route: '/processos', queryParams: { processo: 'gestaoviagens' } },
    { icon: 'la-chart-line', label: 'Roteirização', route: '/processos', queryParams: { processo: 'roteirizacao' } },
    { icon: 'la-exclamation-triangle', label: 'Incidentes', route: '/processos', queryParams: { processo: 'incidentes' } },
  ];

  catalogoSubmenu = [
    { icon: 'la-boxes', label: 'Produtos', route: '/catalogo/produtos' },
    { icon: 'la-users', label: 'Clientes', route: '/catalogo/clientes' },
    { icon: 'la-truck', label: 'Fornecedores', route: '/catalogo/fornecedores' },
    { icon: 'la-tachometer-alt', label: 'Veículos', route: '/catalogo/veiculos' },
    { icon: 'la-warehouse', label: 'Armazém', route: '/catalogo/armazem' },
    { icon: 'la-shipping-fast', label: 'Transportadoras', route: '/catalogo/transportadoras' },
    { icon: 'la-route', label: 'Rotas', route: '/catalogo/rotas' }
  ];

  impressaoSubmenu = [
    { icon: 'la-tag', label: 'Etiquetas', route: '/impressao/etiquetas' },
    { icon: 'la-file-alt', label: 'Guias', route: '/impressao/guias' },
    { icon: 'la-file-pdf', label: 'Documentos', route: '/impressao/documentos' }
  ];

  toggleProcessosMenu(): void {
    this.processosMenuOpen.update(v => !v);
    if (this.processosMenuOpen()) {
      this.catalogoMenuOpen.set(false);
      this.impressaoMenuOpen.set(false);
    }
  }

  toggleCatalogoMenu(): void {
    this.catalogoMenuOpen.update(v => !v);
    if (this.catalogoMenuOpen()) {
      this.processosMenuOpen.set(false);
      this.impressaoMenuOpen.set(false);
    }
  }

  toggleImpressaoMenu(): void {
    this.impressaoMenuOpen.update(v => !v);
    if (this.impressaoMenuOpen()) {
      this.processosMenuOpen.set(false);
      this.catalogoMenuOpen.set(false);
    }
  }

  irParaProcesso(processo: string): void {
    if (processo === 'entradas') {
      this.router.navigate(['/entradas']);
    } else {
      this.router.navigate(['/processos'], { queryParams: { processo } });
    }
  }

  navegarPara(route: string, queryParams?: any): void {
    if (queryParams) {
      this.router.navigate([route], { queryParams });
    } else {
      this.router.navigate([route]);
    }
  }
}