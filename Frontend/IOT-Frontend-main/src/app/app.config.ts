import { ApplicationConfig, provideExperimentalZonelessChangeDetection } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withFetch } from '@angular/common/http';
import { routes } from './app.routes';

// Core services
import { EnvironmentService } from './core/services/environment.service';
import { ApiService } from './core/services/api.service';
import { WebSocketService } from './core/services/websocket.service';

export const appConfig: ApplicationConfig = {
  providers: [
    provideExperimentalZonelessChangeDetection(),
    provideRouter(routes),
    provideHttpClient(withFetch()),
    EnvironmentService,
    ApiService,
    WebSocketService
  ]
};
