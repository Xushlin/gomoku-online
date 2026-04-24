import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { TranslocoTestingModule } from '@jsverse/transloco';
import { ActivatedRoute, Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { HttpErrorResponse } from '@angular/common/http';
import { AuthService } from '../../../core/auth/auth.service';
import { Login } from './login';

class StubAuth {
  readonly accessToken = signal<string | null>(null);
  readonly user = signal<unknown>(null);
  readonly accessTokenExpiresAt = signal<Date | null>(null);
  readonly isAuthenticated = signal<boolean>(false);
  login = vi.fn(() => of(undefined));
}

function activatedRouteWith(params: Record<string, string> = {}): ActivatedRoute {
  const paramMap = { get: (k: string) => params[k] ?? null };
  return {
    queryParamMap: of(paramMap),
    snapshot: { queryParamMap: paramMap },
  } as unknown as ActivatedRoute;
}

describe('Login page', () => {
  let auth: StubAuth;

  beforeEach(() => {
    auth = new StubAuth();
  });

  function mount(params: Record<string, string> = {}) {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [
        Login,
        TranslocoTestingModule.forRoot({
          langs: {
            en: {
              auth: {
                login: {
                  title: 'Log in',
                  submit: 'Log in',
                  'submit-loading': 'Logging in…',
                  'email-label': 'Email',
                  'email-placeholder': 'you@example.com',
                  'password-label': 'Password',
                  'password-placeholder': '••••••••',
                  'no-account-cta': 'Register',
                  flash: { 'password-changed': 'Password changed' },
                  errors: {
                    'invalid-credentials': 'Email or password is incorrect.',
                    'account-inactive': 'Inactive',
                    network: 'Network error',
                  },
                },
                errors: {
                  generic: 'Something went wrong',
                  required: 'Required',
                  'email-invalid': 'Invalid email',
                },
              },
            },
          },
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
          },
        },
        { provide: AuthService, useValue: auth },
        { provide: ActivatedRoute, useValue: activatedRouteWith(params) },
      ],
    });
    const fixture = TestBed.createComponent(Login);
    fixture.detectChanges();
    return fixture;
  }

  it('submits with typed credentials and calls AuthService.login', () => {
    const fixture = mount();
    const comp = fixture.componentInstance as unknown as {
      form: { setValue: (v: Record<string, string>) => void };
      submit: () => void;
    };
    comp.form.setValue({ email: 'alice@example.com', password: 'Password1' });
    comp.submit();
    expect(auth.login).toHaveBeenCalledWith('alice@example.com', 'Password1');
  });

  it('on 401 shows the invalid-credentials banner and re-enables submit', () => {
    auth.login = vi.fn(() =>
      throwError(() => new HttpErrorResponse({ status: 401, statusText: 'Unauthorized' })),
    );
    const fixture = mount();
    const comp = fixture.componentInstance as unknown as {
      form: { setValue: (v: Record<string, string>) => void };
      submit: () => void;
      banner: () => { kind: string; key: string };
      submitting: () => boolean;
    };
    comp.form.setValue({ email: 'alice@example.com', password: 'wrong' });
    comp.submit();

    expect(comp.banner().kind).toBe('error');
    expect(comp.banner().key).toBe('auth.login.errors.invalid-credentials');
    expect(comp.submitting()).toBe(false);
  });

  it('sets the flash banner when ?flash=password-changed is present', () => {
    const fixture = mount({ flash: 'password-changed' });
    const comp = fixture.componentInstance as unknown as {
      banner: () => { kind: string; key: string };
    };
    expect(comp.banner().kind).toBe('info');
    expect(comp.banner().key).toBe('auth.login.flash.password-changed');
  });
});
