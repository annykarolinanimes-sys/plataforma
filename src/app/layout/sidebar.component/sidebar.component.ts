import { Component, input, inject, signal, computed } from '@angular/core';
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
  adminMenuOpen     = signal(false);
  
  showAdminSection = computed(() => {
    const isAdmin = this.auth.isAdmin();
    console.log('[Sidebar] showAdminSection computed:', isAdmin);
    return isAdmin;
  });
  
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
    { icon: 'la-exchange-alt', label: 'Atribuições', route: '/atribuicoes' },
    { icon: 'la-calculator', label: 'Gestão de Viagens', route: '/gestao-viagens' },
    { icon: 'la-exclamation-triangle', label: 'Incidentes', route: '/incidentes' },
  ];

  catalogoSubmenu = [
    { icon: 'la-boxes', label: 'Produtos', route: '/catalogo/produtos' },
    { icon: 'la-users', label: 'Clientes', route: '/catalogo/clientes' },
    { icon: 'la-truck', label: 'Fornecedores', route: '/catalogo/fornecedores' },
    { icon: 'la-tachometer-alt', label: 'Veículos', route: '/catalogo/veiculos' },
    { icon: 'la-warehouse', label: 'Armazém', route: '/catalogo/armazem' },
    { icon: 'la-shipping-fast', label: 'Transportadoras', route: '/catalogo/transportadoras' },
    { icon: 'la-user-tie', label: 'Motoristas', route: '/catalogo/motoristas' },
  ];

  impressaoSubmenu = [
    { icon: 'la-tag', label: 'Etiquetas', route: '/impressao/etiquetas' },
    { icon: 'la-file-alt', label: 'Guias', route: '/impressao/guias' },
    { icon: 'la-file-pdf', label: 'Documentos', route: '/impressao/documentos' }
  ];

  adminSubmenu = [
    { icon: 'la-chart-line', label: 'Atividades', route: '/admin/atividades' },
    { icon: 'la-clipboard-list', label: 'Logs de Auditoria', route: '/admin/audit' },
    { icon: 'la-users', label: 'Utilizadores & Sessões', route: '/admin/users' }
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
      this.adminMenuOpen.set(false);
    }
  }

  toggleAdminMenu(): void {
    this.adminMenuOpen.update(v => !v);
    if (this.adminMenuOpen()) {
      this.processosMenuOpen.set(false);
      this.catalogoMenuOpen.set(false);
      this.impressaoMenuOpen.set(false);
    }
  }
}