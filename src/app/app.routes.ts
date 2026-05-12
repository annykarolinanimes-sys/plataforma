import { Routes } from '@angular/router';
import { authGuard, adminGuard } from './core/guards/auth.guard';
import { MainLayoutComponent } from './layout/main-layout.component/main-layout.component';

export const routes: Routes = [
  {
    path: 'login',
    loadComponent: () =>
      import('./features/login.component/login.component').then(m => m.LoginComponent),
  },
  {
    path: '',
    component: MainLayoutComponent,
    canActivate: [authGuard],
    children: [
      { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
      { path: 'dashboard', loadComponent: () => import('./features/dashboard.component/dashboard.component').then(m => m.DashboardComponent) },
      
      { path: 'recepcao', loadComponent: () => import('./features/recepcao.component/recepcao.component').then(m => m.RecepcaoComponent) },
      { path: 'atribuicoes', loadComponent: () => import('./features/atribuicao.component/atribuicao.component').then(m => m.AtribuicaoComponent) },
      { path: 'gestao-viagens', loadComponent: () => import('./features/gestao-viagens.component/gestao-viagens.component').then(m => m.GestaoViagensComponent) },
      { path: 'incidentes', loadComponent: () => import('./features/incidentes.component/incidentes.component').then(m => m.IncidentesComponent) },
      
      { path: 'catalogo/produtos', loadComponent: () => import('./features/produtos.component/produtos.component').then(m => m.ProdutosComponent) },
      { path: 'catalogo/clientes', loadComponent: () => import('./features/clientes-catalogo.component/clientes-catalogo.component').then(m => m.ClientesCatalogoComponent) },
      { path: 'catalogo/fornecedores', loadComponent: () => import('./features/fornecedores-catalogo.component/fornecedores-catalogo.component').then(m => m.FornecedoresCatalogoComponent) },
      { path: 'catalogo/veiculos', loadComponent: () => import('./features/veiculos.component/veiculos.component').then(m => m.VeiculosComponent) },
      { path: 'catalogo/armazem', loadComponent: () => import('./features/armazens.component/armazens.component').then(m => m.ArmazensComponent) },
      { path: 'catalogo/transportadoras', loadComponent: () => import('./features/transportadoras-catalogo.component/transportadoras-catalogo.component').then(m => m.TransportadorasComponent) },
      { path: 'catalogo/motoristas', loadComponent: () => import('./features/motoristas.component/motoristas.component').then(m => m.MotoristasComponent) },
      { path: 'impressao', redirectTo: 'impressao/documentos', pathMatch: 'full' },
      { path: 'impressao/etiquetas', loadComponent: () => import('./features/etiquetas.component/etiquetas.component').then(m => m.EtiquetasComponent) },
      { path: 'impressao/guias', loadComponent: () => import('./features/guias.component/guias.component').then(m => m.GuiasComponent) },
      { path: 'impressao/documentos', loadComponent: () => import('./features/documentos/documentos').then(m => m.DocumentosComponent) },
      
      { path: 'faturas', loadComponent: () => import('./features/faturas.component/faturas.component').then(m => m.FaturasComponent) },
      { path: 'definicao', loadComponent: () => import('./features/definicao.component/definicao.component').then(m => m.DefinicaoComponent) },
      { path: 'admin', redirectTo: 'admin/atividades', pathMatch: 'full' },
      { path: 'admin/atividades', loadComponent: () => import('./features/admin.component/admin.component').then(m => m.AdminComponent), canActivate: [authGuard, adminGuard] },
      { path: 'admin/audit', loadComponent: () => import('./features/admin.component/admin.component').then(m => m.AdminComponent), canActivate: [authGuard, adminGuard] },
      { path: 'admin/users', loadComponent: () => import('./features/admin.component/admin.component').then(m => m.AdminComponent), canActivate: [authGuard, adminGuard] },
    ],
  },
  { path: '**', redirectTo: 'login' },
];