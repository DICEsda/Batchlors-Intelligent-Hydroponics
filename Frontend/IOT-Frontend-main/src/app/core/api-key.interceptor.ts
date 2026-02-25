import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { EnvironmentService } from './services/environment.service';

/**
 * HTTP interceptor that attaches an X-API-Key header to outgoing
 * requests directed at the backend API.
 *
 * The key is read from EnvironmentService (runtime-configurable via
 * window.__env.apiKey) and defaults to 'hydro-thesis-2026' to match
 * the docker-compose default.
 *
 * Requests to other origins (CDN, third-party, etc.) are passed
 * through unchanged.
 */
export const apiKeyInterceptor: HttpInterceptorFn = (req, next) => {
  const env = inject(EnvironmentService);
  const apiKey = env.apiKey;

  if (apiKey && req.url.startsWith(env.apiUrl)) {
    const cloned = req.clone({
      setHeaders: { 'X-API-Key': apiKey },
    });
    return next(cloned);
  }

  return next(req);
};
