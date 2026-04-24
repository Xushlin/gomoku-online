import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { AuthService } from '../../../core/auth/auth.service';
import { isProblemDetails } from '../../../core/auth/problem-details';
import { AuthCard } from '../shared/auth-card';
import { matchFieldsValidator } from '../shared/match-fields.validator';
import { passwordPolicyValidator } from '../shared/password-policy.validator';
import { mapProblemDetailsToForm } from '../shared/problem-details.mapper';

@Component({
  selector: 'app-change-password',
  standalone: true,
  imports: [AuthCard, ReactiveFormsModule, TranslocoPipe],
  templateUrl: './change-password.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ChangePassword {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly fb = inject(FormBuilder);

  protected readonly submitting = signal(false);
  protected readonly bannerKey = signal<string | null>(null);

  protected readonly form = this.fb.nonNullable.group(
    {
      currentPassword: ['', [Validators.required]],
      newPassword: ['', [Validators.required, passwordPolicyValidator()]],
      confirmPassword: ['', [Validators.required]],
    },
    { validators: [matchFieldsValidator('newPassword', 'confirmPassword')] },
  );

  protected submit(): void {
    if (this.form.invalid || this.submitting()) {
      this.form.markAllAsTouched();
      return;
    }
    this.submitting.set(true);
    this.bannerKey.set(null);
    const { currentPassword, newPassword } = this.form.getRawValue();
    this.auth.changePassword(currentPassword, newPassword).subscribe({
      next: () => {
        this.submitting.set(false);
        void this.router.navigate(['/login'], {
          queryParams: { flash: 'password-changed' },
        });
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
        this.form.controls.currentPassword.setErrors({
          ...(this.form.controls.currentPassword.errors ?? {}),
          serverKey: 'auth.change-password.errors.wrong-current',
        });
        this.form.controls.currentPassword.markAsTouched();
        return;
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
}
