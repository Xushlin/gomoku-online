import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { AuthService } from '../../../core/auth/auth.service';
import { isProblemDetails } from '../../../core/auth/problem-details';
import { AuthCard } from '../shared/auth-card';
import { passwordPolicyValidator } from '../shared/password-policy.validator';
import { mapProblemDetailsToForm } from '../shared/problem-details.mapper';

@Component({
  selector: 'app-register',
  standalone: true,
  imports: [AuthCard, ReactiveFormsModule, RouterLink, TranslocoPipe],
  templateUrl: './register.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Register {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly fb = inject(FormBuilder);

  protected readonly submitting = signal(false);
  protected readonly bannerKey = signal<string | null>(null);

  protected readonly form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    username: ['', [Validators.required, Validators.minLength(3)]],
    password: ['', [Validators.required, passwordPolicyValidator()]],
  });

  protected submit(): void {
    if (this.form.invalid || this.submitting()) {
      this.form.markAllAsTouched();
      return;
    }
    this.submitting.set(true);
    this.bannerKey.set(null);
    const { email, username, password } = this.form.getRawValue();
    this.auth.register(email, username, password).subscribe({
      next: () => {
        this.submitting.set(false);
        void this.router.navigateByUrl('/home');
      },
      error: (err: unknown) => {
        this.submitting.set(false);
        this.handleError(err);
      },
    });
  }

  private handleError(err: unknown): void {
    if (err instanceof HttpErrorResponse) {
      if (err.status === 409) {
        const pd = isProblemDetails(err.error) ? err.error : undefined;
        const typeOrTitle = (pd?.type ?? '') + ' ' + (pd?.title ?? '');
        if (/email/i.test(typeOrTitle) || this.hasServerErrorFor('email', pd)) {
          this.form.controls.email.setErrors({
            ...(this.form.controls.email.errors ?? {}),
            serverKey: 'auth.register.errors.email-taken',
          });
          this.form.controls.email.markAsTouched();
          return;
        }
        if (/username/i.test(typeOrTitle) || this.hasServerErrorFor('username', pd)) {
          this.form.controls.username.setErrors({
            ...(this.form.controls.username.errors ?? {}),
            serverKey: 'auth.register.errors.username-taken',
          });
          this.form.controls.username.markAsTouched();
          return;
        }
      }
      if (err.status === 400 && isProblemDetails(err.error)) {
        const matched = mapProblemDetailsToForm(this.form, err.error);
        if (!matched) this.bannerKey.set('auth.errors.generic');
        return;
      }
      if (err.status === 0) {
        this.bannerKey.set('auth.errors.network');
        return;
      }
    }
    this.bannerKey.set('auth.errors.generic');
  }

  private hasServerErrorFor(
    field: string,
    pd: { errors?: Readonly<Record<string, readonly string[]>> } | undefined,
  ): boolean {
    if (!pd?.errors) return false;
    return Object.keys(pd.errors).some((k) => k.toLowerCase() === field.toLowerCase());
  }
}
