import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { AuthService } from '../../../core/auth/auth.service';
import { isProblemDetails } from '../../../core/auth/problem-details';
import { AuthCard } from '../shared/auth-card';
import { mapProblemDetailsToForm } from '../shared/problem-details.mapper';

type BannerKind = 'none' | 'info' | 'error';

interface Banner {
  readonly kind: BannerKind;
  readonly key: string;
}

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [AuthCard, ReactiveFormsModule, RouterLink, TranslocoPipe],
  templateUrl: './login.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Login {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly fb = inject(FormBuilder);

  protected readonly submitting = signal(false);
  protected readonly banner = signal<Banner>({ kind: 'none', key: '' });

  protected readonly form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required]],
  });

  private readonly queryParams = toSignal(
    this.route.queryParamMap.pipe(takeUntilDestroyed()),
    { initialValue: null },
  );
  private readonly returnUrl = computed(() => this.queryParams()?.get('returnUrl') ?? '/home');
  protected readonly flash = computed(() => this.queryParams()?.get('flash'));

  constructor() {
    const flashParam = this.route.snapshot.queryParamMap.get('flash');
    if (flashParam === 'password-changed') {
      this.banner.set({ kind: 'info', key: 'auth.login.flash.password-changed' });
    }
  }

  protected submit(): void {
    if (this.form.invalid || this.submitting()) {
      this.form.markAllAsTouched();
      return;
    }
    this.submitting.set(true);
    this.banner.set({ kind: 'none', key: '' });
    const { email, password } = this.form.getRawValue();
    this.auth.login(email, password).subscribe({
      next: () => {
        this.submitting.set(false);
        void this.router.navigateByUrl(this.returnUrl());
      },
      error: (err: unknown) => {
        this.submitting.set(false);
        this.handleError(err);
      },
    });
  }

  private handleError(err: unknown): void {
    if (err instanceof HttpErrorResponse) {
      if (err.status === 401) {
        this.banner.set({ kind: 'error', key: 'auth.login.errors.invalid-credentials' });
        return;
      }
      if (err.status === 403) {
        this.banner.set({ kind: 'error', key: 'auth.login.errors.account-inactive' });
        return;
      }
      if (err.status === 400 && isProblemDetails(err.error)) {
        const matched = mapProblemDetailsToForm(this.form, err.error);
        if (!matched) {
          this.banner.set({ kind: 'error', key: 'auth.errors.generic' });
        }
        return;
      }
      if (err.status === 0) {
        this.banner.set({ kind: 'error', key: 'auth.login.errors.network' });
        return;
      }
    }
    this.banner.set({ kind: 'error', key: 'auth.errors.generic' });
  }
}
