import {
  HttpInterceptorFn, HttpRequest, HttpHandlerFn,
  HttpEvent, HttpErrorResponse,
} from '@angular/common/http';
import { inject } from '@angular/core';
import { Observable, throwError, BehaviorSubject, filter, take, switchMap, catchError } from 'rxjs';
import { Router } from '@angular/router';

export const AUTH_SERVICE_TOKEN = Symbol('AUTH_SERVICE');


let isRefreshing = false;
const refreshSubject$ = new BehaviorSubject<string | null>(null);

export const ecmAuthInterceptor: HttpInterceptorFn = (
  req: HttpRequest<unknown>,
  next: HttpHandlerFn,
): Observable<HttpEvent<unknown>> => {

  const router = inject(Router);

  if (!req.url.includes('/api/v1/documentos')) {
    return next(req);
  }

  const token = obterToken();
  const reqComJwt = token ? injetarToken(req, token) : req;

  return next(reqComJwt).pipe(
    catchError((erro: HttpErrorResponse) => {

      if (erro.status === 401) {
        if (!isRefreshing) {
          isRefreshing = true;
          refreshSubject$.next(null);

          return renovarToken().pipe(
            switchMap(novoToken => {
              isRefreshing = false;
              refreshSubject$.next(novoToken);
              return next(injetarToken(req, novoToken));
            }),
            catchError(erroRenovacao => {
              isRefreshing = false;
              limparSessao();
              router.navigate(['/login'], { queryParams: { reason: 'session_expired' } });
              return throwError(() => erroRenovacao);
            }),
          );
        }

        return refreshSubject$.pipe(
          filter(token => token !== null),
          take(1),
          switchMap(token => next(injetarToken(req, token!))),
        );
      }

      if (erro.status === 403) {
        console.warn(
          '[ECM] Acesso negado.',
          `Endpoint: ${req.url}`,
          `CorrelationId: ${erro.headers.get('X-Correlation-Id') ?? 'N/A'}`,
        );
      }

      return throwError(() => erro);
    }),
  );
};


function obterToken(): string | null {
  return localStorage.getItem('acc_token')
    ?? localStorage.getItem('access_token');
}

function injetarToken(req: HttpRequest<unknown>, token: string): HttpRequest<unknown> {
  return req.clone({
    setHeaders: { Authorization: `Bearer ${token}` },
  });
}

function renovarToken(): Observable<string> {
  const refreshToken = localStorage.getItem('refresh_token');
  if (!refreshToken) return throwError(() => new Error('Sem refresh token'));

  return throwError(() => new Error('renovarToken: implementar no projeto host'));
}

function limparSessao(): void {
  localStorage.removeItem('acc_token');
  localStorage.removeItem('access_token');
  localStorage.removeItem('refresh_token');
}
