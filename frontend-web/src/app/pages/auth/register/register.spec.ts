import { HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { TranslocoTestingModule } from '@jsverse/transloco';
import { ActivatedRoute, Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { AuthService } from '../../../core/auth/auth.service';
import { Register } from './register';

class StubAuth {
  readonly accessToken = signal<string | null>(null);
  readonly user = signal<unknown>(null);
  readonly accessTokenExpiresAt = signal<Date | null>(null);
  readonly isAuthenticated = signal<boolean>(false);
  register = vi.fn(() => of(undefined));
}

function mount(auth: StubAuth) {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    imports: [
      Register,
      TranslocoTestingModule.forRoot({
        langs: { en: {} },
        translocoConfig: { availableLangs: ['en'], defaultLang: 'en' },
        preloadLangs: true,
      }),
    ],
    providers: [
      provideHttpClient(),
      provideHttpClientTesting(),
      {
        provide: Router,
        useValue: {
          navigate: vi.fn(() => Promise.resolve(true)),
          navigateByUrl: vi.fn(() => Promise.resolve(true)),
          createUrlTree: vi.fn(() => ({ toString: () => '/' })),
          serializeUrl: vi.fn(() => '/'),
          events: of(),
        },
      },
      {
        provide: ActivatedRoute,
        useValue: {
          snapshot: { queryParamMap: { get: () => null } },
          queryParamMap: of({ get: () => null }),
        },
      },
      { provide: AuthService, useValue: auth },
    ],
  });
  const fixture = TestBed.createComponent(Register);
  fixture.detectChanges();
  return fixture;
}

describe('Register page', () => {
  let auth: StubAuth;
  beforeEach(() => (auth = new StubAuth()));

  it('submit calls AuthService.register with the entered fields', () => {
    const fixture = mount(auth);
    const comp = fixture.componentInstance as unknown as {
      form: { setValue: (v: Record<string, string>) => void };
      submit: () => void;
    };
    comp.form.setValue({ email: 'bob@example.com', username: 'bob', password: 'Password1' });
    comp.submit();
    expect(auth.register).toHaveBeenCalledWith('bob@example.com', 'bob', 'Password1');
  });

  it('on 409 with ProblemDetails errors.email → stamps email field with serverKey', () => {
    auth.register = vi.fn(() =>
      throwError(
        () =>
          new HttpErrorResponse({
            status: 409,
            error: {
              type: 'EmailAlreadyExistsException',
              title: 'Conflict',
              errors: { Email: ['That email is already registered.'] },
            },
          }),
      ),
    );
    const fixture = mount(auth);
    const comp = fixture.componentInstance as unknown as {
      form: {
        setValue: (v: Record<string, string>) => void;
        controls: { email: { errors: Record<string, unknown> | null } };
      };
      submit: () => void;
    };
    comp.form.setValue({ email: 'bob@example.com', username: 'bob', password: 'Password1' });
    comp.submit();
    expect(comp.form.controls.email.errors?.['serverKey']).toBe('auth.register.errors.email-taken');
  });
});
