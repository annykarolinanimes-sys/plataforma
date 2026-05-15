import { Routes } from '@angular/router';
import { inject } from '@angular/core';
import { Router } from '@angular/router';


const adminGuard = () => {
  const router = inject(Router);
  const storedUser = localStorage.getItem('acc_user');
  if (storedUser) {
    const user = JSON.parse(storedUser);
    if (user.role === 'admin') return true;
  }

  router.navigate(['/acesso-negado'], { queryParams: { from: 'ecm-admin' } });
  return false;
};


const authGuard = () => {
  const router = inject(Router);
  const token = localStorage.getItem('acc_token');

  if (token) return true;

  router.navigate(['/login'], { queryParams: { returnUrl: '/impressao/documentos' } });
  return false;
};


export const ECM_ROUTES: Routes = [
  {
    path: '',
    canActivate: [authGuard],
    children: [
      {
        path: '',
        loadComponent: () =>
          import('../documentos/documentos').then(
            m => m.DocumentosComponent,
          ),
        title: 'Gestão Documental — ECM',
        data: { breadcrumb: 'Documentos', icon: 'la-folder-open' },
      },
      {
        path: ':id',
        loadComponent: () =>
          import('../documentos/documentos').then(
            m => m.DocumentosComponent,
          ),
        title: 'Documento — ECM',
        data: { breadcrumb: 'Detalhe', icon: 'la-file-alt' },
      },
    ],
  },
];

