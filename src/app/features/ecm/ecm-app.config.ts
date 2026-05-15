import { ApplicationConfig, provideZoneChangeDetection } from '@angular/core';
import { provideRouter, withComponentInputBinding, withViewTransitions } from '@angular/router';
import {
  provideHttpClient,
  withInterceptors,
  withFetch,
} from '@angular/common/http';
import { provideAnimations } from '@angular/platform-browser/animations';
import { DatePipe, DecimalPipe } from '@angular/common';

import { ECM_ROUTES } from './ecm-documentos.routes';
import { ecmAuthInterceptor } from '../ecm/ecm-auth.interceptor';



export const ecmAppConfig: ApplicationConfig = {
  providers: [
    provideZoneChangeDetection({ eventCoalescing: true }),

    provideRouter(
      ECM_ROUTES,
      withComponentInputBinding(),   
      withViewTransitions(),         
    ),

    provideHttpClient(
      withFetch(),                              
      withInterceptors([ecmAuthInterceptor]),   
    ),

    provideAnimations(),

    DatePipe,
    DecimalPipe,
  ],
};

