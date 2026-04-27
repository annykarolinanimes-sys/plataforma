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
      { path: 'dashboard',  loadComponent: () => import('./features/dashboard.component/dashboard.component').then(m => m.DashboardComponent),  title: 'Dashboard — Accusoft'  },
      { path: 'processos',  loadComponent: () => import('./features/processos.component/processos.component').then(m => m.ProcessosComponent), title: 'Processos — Accusoft' },
      { path: 'catalogo/produtos', loadComponent: () => import('./features/produtos.component/produtos.component').then(m => m.ProdutosComponent) },
      { path: 'catalogo/clientes', loadComponent: () => import('./features/clientes-catalogo.component/clientes-catalogo.component').then(m => m.ClientesCatalogoComponent) },
      { path: 'catalogo/veiculos', loadComponent: () => import('./features/veiculos.component/veiculos.component').then(m => m.VeiculosComponent) },
      { path: 'catalogo/fornecedores', loadComponent: () => import('./features/fornecedores-catalogo.component/fornecedores-catalogo.component').then(m => m.FornecedoresCatalogoComponent) },
      { path: 'catalogo/armazem', loadComponent: () => import('./features/armazens.component/armazens.component').then(m => m.ArmazensComponent) },
      { path: 'catalogo/transportadoras', loadComponent: () => import('./features/transportadoras-catalogo.component/transportadoras-catalogo.component').then(m => m.TransportadorasCatalogoComponent) },
      { path: 'definicao',  loadComponent: () => import('./features/definicao.component/definicao.component').then(m => m.DefinicaoComponent),    title: 'Definições — Accusoft' },
      { path: 'faturas',    loadComponent: () => import('./features/faturas.component/faturas.component').then(m => m.FaturasComponent),          title: 'Faturas — Accusoft '},
      { path: 'admin',      canActivate: [adminGuard], loadComponent: () => import('./features/admin.component/admin.component').then(m => m.AdminComponent), title: 'Admin — Accusoft' },
    ],
  },
  { path: '**', redirectTo: 'login' },
];


