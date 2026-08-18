import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
} from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { Router } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { debounceTime, distinctUntilChanged, map } from 'rxjs/operators';
import type { UserPublicProfileDto } from '../../../../core/api/models/user-profile.model';
import { UsersApiService } from '../../../../core/api/users-api.service';

const DEBOUNCE_MS = 250;
const MIN_CHARS = 3;
const MAX_RESULTS = 5;

/**
 * Find a player by name, on the platform home.
 *
 * The input is a plain signal rather than a `FormControl`, and that is a **bundle**
 * decision, not a style one: this card was the *only* eagerly-loaded consumer of
 * `@angular/forms` — the auth pages, the lobby dialogs and the chat panel are all
 * behind lazy routes — so one debounced text box was pulling **34 kB** of forms
 * machinery into the initial bundle. Measured, not guessed: it is what took the
 * initial chunk from 504.65 kB (4.65 kB over the 500 kB budget) to under it.
 *
 * Nothing about the behaviour changes: same 250 ms debounce, same 3-character
 * minimum, same de-duplication of repeated queries.
 */
@Component({
  selector: 'app-find-player-card',
  standalone: true,
  imports: [TranslocoPipe],
  templateUrl: './find-player.html',
  styles: [':host { display: block; width: 100%; }'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FindPlayerCard {
  private readonly users = inject(UsersApiService);
  private readonly router = inject(Router);

  /** What is in the box right now — bound with `[value]` + `(input)`. */
  protected readonly text = signal('');

  /** The debounced, de-duplicated, trimmed query that actually drives the search. */
  private readonly query = toSignal(
    toObservable(this.text).pipe(
      debounceTime(DEBOUNCE_MS),
      distinctUntilChanged(),
      map((v) => v.trim()),
    ),
    { initialValue: '' },
  );

  protected readonly results = signal<readonly UserPublicProfileDto[]>([]);
  protected readonly loading = signal<boolean>(false);
  protected readonly error = signal<boolean>(false);
  protected readonly searched = signal<boolean>(false);

  protected readonly hintTooShort = computed(
    () => this.query().length > 0 && this.query().length < MIN_CHARS,
  );
  protected readonly showNoResults = computed(
    () =>
      !this.loading() &&
      !this.error() &&
      this.searched() &&
      this.query().length >= MIN_CHARS &&
      this.results().length === 0,
  );

  constructor() {
    effect((onCleanup) => {
      const q = this.query();
      if (q.length < MIN_CHARS) {
        this.results.set([]);
        this.searched.set(false);
        this.error.set(false);
        return;
      }
      this.loading.set(true);
      this.error.set(false);
      const sub = this.users.search(q, 1, MAX_RESULTS).subscribe({
        next: (r) => {
          this.results.set(r.items);
          this.searched.set(true);
          this.loading.set(false);
        },
        error: () => {
          this.results.set([]);
          this.error.set(true);
          this.searched.set(true);
          this.loading.set(false);
        },
      });
      onCleanup(() => sub.unsubscribe());
    });
  }

  protected onInput(event: Event): void {
    this.text.set((event.target as HTMLInputElement).value);
  }

  protected pick(user: UserPublicProfileDto): void {
    this.text.set('');
    this.results.set([]);
    this.searched.set(false);
    void this.router.navigateByUrl(`/users/${user.id}`);
  }
}
