import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { describe, expect, it } from 'vitest';
import { DefaultUsersApiService, UsersApiService } from './users-api.service';

function setup() {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(),
      provideHttpClientTesting(),
      { provide: UsersApiService, useClass: DefaultUsersApiService },
    ],
  });
  return {
    svc: TestBed.inject(UsersApiService),
    http: TestBed.inject(HttpTestingController),
  };
}

describe('UsersApiService', () => {
  it('getProfile() GETs /api/users/{id} with URL encoding', () => {
    const { svc, http } = setup();
    svc.getProfile('abc 123').subscribe();
    const req = http.expectOne('/api/users/abc%20123');
    expect(req.request.method).toBe('GET');
    req.flush({});
    http.verify();
  });

  it('getGames() GETs /api/users/{id}/games?page=&pageSize=', () => {
    const { svc, http } = setup();
    svc.getGames('u-1', 2, 10).subscribe();
    const req = http.expectOne(
      (r) =>
        r.url === '/api/users/u-1/games' &&
        r.params.get('page') === '2' &&
        r.params.get('pageSize') === '10',
    );
    expect(req.request.method).toBe('GET');
    req.flush({ items: [], total: 0, page: 2, pageSize: 10 });
    http.verify();
  });

  it('search() GETs /api/users with encoded query', () => {
    const { svc, http } = setup();
    svc.search('Ali ce', 1, 5).subscribe();
    const req = http.expectOne(
      (r) =>
        r.url === '/api/users' &&
        r.params.get('search') === 'Ali ce' &&
        r.params.get('page') === '1' &&
        r.params.get('pageSize') === '5',
    );
    expect(req.request.method).toBe('GET');
    req.flush({ items: [], total: 0, page: 1, pageSize: 5 });
    http.verify();
  });
});
