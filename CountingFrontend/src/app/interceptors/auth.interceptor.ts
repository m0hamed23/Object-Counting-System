import {
  HttpInterceptorFn,
  HttpRequest,
  HttpHandlerFn,
  HttpErrorResponse
} from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError } from 'rxjs/operators';
import { throwError } from 'rxjs';
import { Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

export const authInterceptor: HttpInterceptorFn = (
  request: HttpRequest<unknown>,
  next: HttpHandlerFn
) => {
  const authService = inject(AuthService);
  const router = inject(Router);
  const token = authService.getToken();

  // Clone the request to add the Authorization header if a token exists
  if (token) {
    request = request.clone({
      setHeaders: {
        Authorization: `Bearer ${token}`
      }
    });
  }

  return next(request).pipe(
    catchError((error: HttpErrorResponse) => {
      // Handle 401 Unauthorized errors (e.g., token expired or invalid)
      if (error.status === 401) {
        console.error('Auth interceptor caught 401 error for URL:', request.url);
        // Avoid redirect loop if the login request itself failed with 401
        if (!request.url.includes('/api/auth/login')) {
          console.log('Token expired or invalid, logging out.');
          authService.logout(); // Clear token and user state
          // Redirect to login with a query param to optionally show a message
          router.navigate(['/login'], { queryParams: { sessionExpired: 'true' } });
        } else {
           // If login failed with 401, just let the component handle the error message
           console.log('Login request failed with 401. Component will handle.');
        }
      }
      // Re-throw the error to be handled by the component/service that made the request
      return throwError(() => error);
    })
  );
};