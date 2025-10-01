import { ApplicationConfig, importProvidersFrom } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideAnimations } from '@angular/platform-browser/animations';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';

import { routes } from './app.routes';
import { authInterceptor } from './core/interceptors/auth.interceptor';
import { SignalrService } from './core/services/signalr.service'; // Import the service

export const appConfig: ApplicationConfig = {
  providers: [
    provideRouter(routes),
    provideHttpClient(withInterceptors([authInterceptor])),
    provideAnimations(),
    // These are needed for template-driven and reactive forms across the app
    importProvidersFrom(FormsModule, ReactiveFormsModule),
    // FIXED: Provide SignalrService here to make it a true singleton
    SignalrService 
  ]
};