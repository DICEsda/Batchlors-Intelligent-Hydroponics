import { HttpInterceptorFn, HttpResponse } from '@angular/common/http';
import { map } from 'rxjs';

/**
 * Converts snake_case object keys to camelCase recursively.
 *
 * The backend REST API serializes JSON with SnakeCaseLower
 * (see Program.cs line 26). This interceptor transparently
 * converts responses so the Angular app can use idiomatic
 * camelCase everywhere.
 */
function snakeToCamel(str: string): string {
  return str.replace(/_([a-z0-9])/g, (_, char) => char.toUpperCase());
}

function camelToSnake(str: string): string {
  return str.replace(/[A-Z]/g, letter => `_${letter.toLowerCase()}`);
}

function isPlainObject(value: unknown): value is Record<string, unknown> {
  return value !== null && typeof value === 'object' && !Array.isArray(value) && !(value instanceof Date);
}

function transformKeys(obj: unknown, keyFn: (key: string) => string): unknown {
  if (Array.isArray(obj)) {
    return obj.map(item => transformKeys(item, keyFn));
  }
  if (isPlainObject(obj)) {
    const result: Record<string, unknown> = {};
    for (const [key, value] of Object.entries(obj)) {
      // Preserve keys that start with _ (like _id from MongoDB)
      const newKey = key.startsWith('_') ? key : keyFn(key);
      result[newKey] = transformKeys(value, keyFn);
    }
    return result;
  }
  return obj;
}

/**
 * HTTP interceptor that converts:
 * - Response body keys: snake_case -> camelCase
 * - Request body keys:  camelCase -> snake_case
 *
 * Preserves keys starting with _ (e.g. _id).
 */
export const snakeCaseInterceptor: HttpInterceptorFn = (req, next) => {
  // Transform request body: camelCase -> snake_case
  let transformedReq = req;
  if (req.body && (req.method === 'POST' || req.method === 'PUT' || req.method === 'PATCH')) {
    const transformedBody = transformKeys(req.body, camelToSnake);
    transformedReq = req.clone({ body: transformedBody });
  }

  return next(transformedReq).pipe(
    map(event => {
      // Transform response body: snake_case -> camelCase
      if (event instanceof HttpResponse && event.body) {
        const transformedBody = transformKeys(event.body, snakeToCamel);
        return event.clone({ body: transformedBody });
      }
      return event;
    })
  );
};
