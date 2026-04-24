import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { beforeEach, describe, expect, it } from 'vitest';
import {
  DefaultPresenceApiService,
  PresenceApiService,
} from './presence-api.service';

function setup() {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(),
      provideHttpClientTesting(),
      { provide: PresenceApiService, useClass: DefaultPresenceApiService },
    ],
  });
  return {
    svc: TestBed.inject(PresenceApiService),
    http: TestBed.inject(HttpTestingController),
  };
}

describe('PresenceApiService', () => {
  beforeEach(() => setup());

  it('getOnlineCount() unwraps { count } to a plain number', () => {
    const { svc, http } = setup();
    let value: number | undefined;
    svc.getOnlineCount().subscribe((v) => (value = v));

    const req = http.expectOne('/api/presence/online-count');
    expect(req.request.method).toBe('GET');
    req.flush({ count: 42 });

    expect(value).toBe(42);
    http.verify();
  });
});
