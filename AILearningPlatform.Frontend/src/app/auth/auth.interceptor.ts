import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { environment } from '../../environments/environment';
import { AuthService } from './auth.service';

/**
 * Hängt das Anmelde-Token an Aufrufe der eigenen API und meldet ab, wenn der Server 401 sagt
 * (Token abgelaufen, manipuliert oder Konto gelöscht).
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);

  // Das Token geht nur an die eigene API, nie an fremde Hosts.
  const isOwnApi = environment.apiUrl ? req.url.startsWith(environment.apiUrl) : req.url.startsWith('/api');
  const isLoginOrRegister = /\/api\/auth\/(login|register)$/.test(req.url);

  const token = auth.token;
  const authorized = isOwnApi && !isLoginOrRegister && token
    ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
    : req;

  return next(authorized).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse && error.status === 401 && isOwnApi && !isLoginOrRegister) {
        auth.logout();
      }
      return throwError(() => error);
    })
  );
};
