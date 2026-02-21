import { ApplicationConfig, provideExperimentalZonelessChangeDetection, APP_INITIALIZER, inject } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import { snakeCaseInterceptor } from './core/snake-case.interceptor';
import { provideAnimations } from '@angular/platform-browser/animations';
import { routes } from './app.routes';

// Core services
import { EnvironmentService } from './core/services/environment.service';
import { ApiService } from './core/services/api.service';
import { WebSocketService } from './core/services/websocket.service';
import { ThemeService } from './core/services/theme.service';
import { ToastService } from './core/services/toast.service';
import { NotificationListenerService } from './core/services/notification-listener.service';

// Initialize theme on app startup
function initializeApp() {
  return () => {
    // Initialize theme
    inject(ThemeService);
    
    // Initialize toast service
    inject(ToastService);
    
    // Initialize notification listener (connects WebSocket and listens for events)
    const notificationListener = inject(NotificationListenerService);
    notificationListener.initialize();
  };
}

export const appConfig: ApplicationConfig = {
  providers: [
    provideExperimentalZonelessChangeDetection(),
    provideRouter(routes),
    provideHttpClient(withFetch(), withInterceptors([snakeCaseInterceptor])),
    provideAnimations(),
    EnvironmentService,
    ApiService,
    WebSocketService,
    ThemeService,
    ToastService,
    NotificationListenerService,
    {
      provide: APP_INITIALIZER,
      useFactory: initializeApp,
      multi: true
    }
  ]
};
