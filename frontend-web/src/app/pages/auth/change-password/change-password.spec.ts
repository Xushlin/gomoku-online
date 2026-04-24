import { HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { TranslocoTestingModule } from '@jsverse/transloco';
import { Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { AuthService } from '../../../core/auth/auth.service';
import { ChangePassword } from './change-password';

class StubAuth {
  readonly accessToken = signal<string | null>(null);
  readonly user = signal<unknown>(null);
  readonly accessTokenExpiresAt = signal<Date | null>(null);
  readonly isAuthenticated = signal<boolean>(true);
  changePassword = vi.fn(() => of(undefined));
}

function mount(auth: StubAuth) {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    imports: [
      ChangePassword,
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
        },
      },
      { provide: AuthService, useValue: auth },
    ],
  });
  const fixture = TestBed.createComponent(ChangePassword);
  fixture.detectChanges();
  return fixture;
}

describe('ChangePassword page', () => {
  let auth: StubAuth;
  beforeEach(() => (auth = new StubAuth()));

  it('submit calls AuthService.changePassword when passwords match', () => {
    const fixture = mount(auth);
    const comp = fixture.componentInstance as unknown as {
      form: { setValue: (v: Record<string, string>) => void };
      submit: () => void;
    };
    comp.form.setValue({
      currentPassword: 'OldPass1',
      newPassword: 'NewPass2',
      confirmPassword: 'NewPass2',
    });
    comp.submit();
    expect(auth.changePassword).toHaveBeenCalledWith('OldPass1', 'NewPass2');
  });

  it('confirm mismatch blocks submit', () => {
    const fixture = mount(auth);
    const comp = fixture.componentInstance as unknown as {
      form: { setValue: (v: Record<string, string>) => void; invalid: boolean };
      submit: () => void;
    };
    comp.form.setValue({
      currentPassword: 'OldPass1',
      newPassword: 'NewPass2',
      confirmPassword: 'different!',
    });
    comp.submit();
    expect(comp.form.invalid).toBe(true);
    expect(auth.changePassword).not.toHaveBeenCalled();
  });

  it('401 → stamps currentPassword with wrong-current serverKey', () => {
    auth.changePassword = vi.fn(() =>
      throwError(() => new HttpErrorResponse({ status: 401, statusText: 'Unauthorized' })),
    );
    const fixture = mount(auth);
    const comp = fixture.componentInstance as unknown as {
      form: {
        setValue: (v: Record<string, string>) => void;
        controls: { currentPassword: { errors: Record<string, unknown> | null } };
      };
      submit: () => void;
    };
    comp.form.setValue({
      currentPassword: 'wrong',
      newPassword: 'NewPass2',
      confirmPassword: 'NewPass2',
    });
    comp.submit();
    expect(comp.form.controls.currentPassword.errors?.['serverKey']).toBe(
      'auth.change-password.errors.wrong-current',
    );
  });
});
