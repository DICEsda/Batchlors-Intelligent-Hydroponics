import { TestBed } from '@angular/core/testing';
import { provideExperimentalZonelessChangeDetection } from '@angular/core';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';

import { snakeCaseInterceptor } from './snake-case.interceptor';

describe('snakeCaseInterceptor', () => {
  let http: HttpClient;
  let httpTesting: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideExperimentalZonelessChangeDetection(),
        provideHttpClient(withInterceptors([snakeCaseInterceptor])),
        provideHttpClientTesting(),
      ],
    });

    http = TestBed.inject(HttpClient);
    httpTesting = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTesting.verify();
  });

  // ---------------------------------------------------------------------------
  // 1. Response body transformation  (snake_case → camelCase)
  // ---------------------------------------------------------------------------

  it('should convert flat snake_case response keys to camelCase', () => {
    http.get('/api/test').subscribe(body => {
      expect(body).toEqual({ userName: 'test' });
    });

    httpTesting.expectOne('/api/test').flush({ user_name: 'test' });
  });

  it('should convert nested snake_case response keys to camelCase', () => {
    http.get('/api/test').subscribe(body => {
      expect(body).toEqual({ userProfile: { firstName: 'a' } });
    });

    httpTesting.expectOne('/api/test').flush({
      user_profile: { first_name: 'a' },
    });
  });

  it('should convert arrays of objects in response', () => {
    http.get('/api/test').subscribe(body => {
      expect(body).toEqual([{ itemId: 1 }]);
    });

    httpTesting.expectOne('/api/test').flush([{ item_id: 1 }]);
  });

  it('should preserve _id key in response (not transform underscore-prefixed keys)', () => {
    http.get('/api/test').subscribe(body => {
      expect(body).toEqual({ _id: '123', userName: 'a' });
    });

    httpTesting.expectOne('/api/test').flush({ _id: '123', user_name: 'a' });
  });

  it('should convert deeply nested response keys (3+ levels)', () => {
    http.get('/api/test').subscribe(body => {
      expect(body).toEqual({
        levelOne: {
          levelTwo: {
            levelThree: { deepValue: 42 },
          },
        },
      });
    });

    httpTesting.expectOne('/api/test').flush({
      level_one: {
        level_two: {
          level_three: { deep_value: 42 },
        },
      },
    });
  });

  it('should handle mixed arrays containing objects with nested arrays', () => {
    http.get('/api/test').subscribe(body => {
      expect(body).toEqual([
        {
          itemName: 'x',
          subItems: [{ subItemId: 1 }, { subItemId: 2 }],
        },
      ]);
    });

    httpTesting.expectOne('/api/test').flush([
      {
        item_name: 'x',
        sub_items: [{ sub_item_id: 1 }, { sub_item_id: 2 }],
      },
    ]);
  });

  it('should preserve null values in response body', () => {
    http.get('/api/test').subscribe(body => {
      expect(body).toEqual({ userName: null });
    });

    httpTesting.expectOne('/api/test').flush({ user_name: null });
  });

  it('should preserve Date-like string values in response (not transform values)', () => {
    http.get('/api/test').subscribe(body => {
      expect(body).toEqual({
        createdAt: '2025-01-01T00:00:00Z',
      });
    });

    httpTesting.expectOne('/api/test').flush({
      created_at: '2025-01-01T00:00:00Z',
    });
  });

  it('should return empty object unchanged for empty response body', () => {
    http.get('/api/test').subscribe(body => {
      expect(body).toEqual({});
    });

    httpTesting.expectOne('/api/test').flush({});
  });

  it('should return primitive string response unchanged', () => {
    http.get('/api/test', { responseType: 'text' }).subscribe(body => {
      expect(body).toBe('hello');
    });

    httpTesting.expectOne('/api/test').flush('hello');
  });

  it('should return primitive number response unchanged', () => {
    http.get<number>('/api/test').subscribe(body => {
      expect(body).toBe(42);
    });

    httpTesting.expectOne('/api/test').flush(42);
  });

  it('should handle response with multiple underscore-prefixed keys', () => {
    http.get('/api/test').subscribe(body => {
      expect(body).toEqual({ _id: '1', _rev: '2', fieldName: 'val' });
    });

    httpTesting.expectOne('/api/test').flush({
      _id: '1',
      _rev: '2',
      field_name: 'val',
    });
  });

  it('should convert keys with consecutive underscores correctly', () => {
    // e.g. my_long_field_name → myLongFieldName
    http.get('/api/test').subscribe(body => {
      expect(body).toEqual({ myLongFieldName: true });
    });

    httpTesting.expectOne('/api/test').flush({ my_long_field_name: true });
  });

  // ---------------------------------------------------------------------------
  // 2. Request body transformation  (camelCase → snake_case on POST/PUT/PATCH)
  // ---------------------------------------------------------------------------

  it('should transform POST request body from camelCase to snake_case', () => {
    http.post('/api/test', { userName: 'test' }).subscribe();

    const req = httpTesting.expectOne('/api/test');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ user_name: 'test' });
    req.flush({});
  });

  it('should transform PUT request body from camelCase to snake_case', () => {
    http.put('/api/test', { firstName: 'a', lastName: 'b' }).subscribe();

    const req = httpTesting.expectOne('/api/test');
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({ first_name: 'a', last_name: 'b' });
    req.flush({});
  });

  it('should transform PATCH request body from camelCase to snake_case', () => {
    http.patch('/api/test', { isActive: true }).subscribe();

    const req = httpTesting.expectOne('/api/test');
    expect(req.request.method).toBe('PATCH');
    expect(req.request.body).toEqual({ is_active: true });
    req.flush({});
  });

  it('should NOT transform GET request (no body)', () => {
    http.get('/api/test').subscribe();

    const req = httpTesting.expectOne('/api/test');
    expect(req.request.method).toBe('GET');
    expect(req.request.body).toBeNull();
    req.flush({});
  });

  it('should NOT transform DELETE request body', () => {
    http.delete('/api/test').subscribe();

    const req = httpTesting.expectOne('/api/test');
    expect(req.request.method).toBe('DELETE');
    expect(req.request.body).toBeNull();
    req.flush({});
  });

  it('should preserve _id key in request body', () => {
    http.post('/api/test', { _id: '123', userName: 'a' }).subscribe();

    const req = httpTesting.expectOne('/api/test');
    expect(req.request.body).toEqual({ _id: '123', user_name: 'a' });
    req.flush({});
  });

  it('should transform nested objects in request body', () => {
    http.post('/api/test', {
      userProfile: { firstName: 'a', addressLine: '123 St' },
    }).subscribe();

    const req = httpTesting.expectOne('/api/test');
    expect(req.request.body).toEqual({
      user_profile: { first_name: 'a', address_line: '123 St' },
    });
    req.flush({});
  });

  // ---------------------------------------------------------------------------
  // 3. Edge cases
  // ---------------------------------------------------------------------------

  it('should not error on POST with null body', () => {
    http.post('/api/test', null).subscribe();

    const req = httpTesting.expectOne('/api/test');
    // null body is falsy – interceptor should skip transformation
    expect(req.request.body).toBeNull();
    req.flush({});
  });

  it('should handle POST with empty object body', () => {
    http.post('/api/test', {}).subscribe();

    const req = httpTesting.expectOne('/api/test');
    expect(req.request.body).toEqual({});
    req.flush({});
  });

  it('should handle already-snake_case response idempotently for simple keys', () => {
    // If the response is already camelCase, snakeToCamel is still run but
    // a key like "userName" has no underscores, so stays "userName".
    http.get('/api/test').subscribe(body => {
      expect(body).toEqual({ userName: 'test' });
    });

    httpTesting.expectOne('/api/test').flush({ userName: 'test' });
  });

  it('should handle arrays of primitives in response without error', () => {
    http.get('/api/test').subscribe(body => {
      expect(body).toEqual([1, 2, 3]);
    });

    httpTesting.expectOne('/api/test').flush([1, 2, 3]);
  });

  it('should handle boolean values in response body', () => {
    http.get('/api/test').subscribe(body => {
      expect(body).toEqual({ isEnabled: true, hasError: false });
    });

    httpTesting.expectOne('/api/test').flush({
      is_enabled: true,
      has_error: false,
    });
  });

  it('should transform request arrays of objects in POST body', () => {
    http.post('/api/test', [{ itemName: 'a' }, { itemName: 'b' }]).subscribe();

    const req = httpTesting.expectOne('/api/test');
    expect(req.request.body).toEqual([{ item_name: 'a' }, { item_name: 'b' }]);
    req.flush({});
  });

  it('should handle response body with numeric snake_case keys', () => {
    // e.g. sensor_1_value → sensor1Value
    http.get('/api/test').subscribe(body => {
      expect(body).toEqual({ sensor1Value: 99 });
    });

    httpTesting.expectOne('/api/test').flush({ sensor_1_value: 99 });
  });
});
