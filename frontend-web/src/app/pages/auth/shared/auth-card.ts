import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/**
 * Presentational wrapper for the three auth pages. Takes a translated title
 * and renders children inside a centred card using only token utilities.
 */
@Component({
  selector: 'app-auth-card',
  standalone: true,
  template: `
    <section
      class="bg-surface text-text border-border rounded-card shadow-elevated mx-auto w-full max-w-sm border p-6 sm:max-w-md"
    >
      <h1 class="mb-6 text-xl font-semibold tracking-tight">{{ title() }}</h1>
      <ng-content />
    </section>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AuthCard {
  readonly title = input.required<string>();
}
