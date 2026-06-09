import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

export const authGuard: CanActivateFn = () => {
  const auth   = inject(AuthService);
  const router = inject(Router);
  
  console.log('[AuthGuard] Verificando autenticação:', auth.isLoggedIn());
  
  if (auth.isLoggedIn()) {
    return true;
  }
  return router.createUrlTree(['/login']);
};

export const adminGuard: CanActivateFn = () => {
  const auth   = inject(AuthService);
  const router = inject(Router);
  
  console.log('[AdminGuard] Verificando permissão de admin:', auth.isAdmin());
  
  if (auth.isAdmin()) {
    return true;
  }
  return router.createUrlTree(['/dashboard']);
};