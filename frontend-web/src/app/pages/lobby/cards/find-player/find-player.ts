import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { debounceTime, distinctUntilChanged, map } from 'rxjs/operators';
import type { UserPublicProfileDto } from '../../../../core/api/models/user-profile.model';
import { UsersApiService } from '../../../../core/api/users-api.service';

const DEBOUNCE_MS = 250;
const MIN_CHARS = 3;
const MAX_RESULTS = 5;

@Component({
  selector: 'app-find-player-card',
  standalone: true,
  imports: [ReactiveFormsModule, TranslocoPipe],
  templateUrl: './find-player.html',
  styles: [':host { display: block; width: 100%; }'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FindPlayerCard {
  private readonly users = inject(UsersApiService);
  private readonly router = inject(Router);

  protected readonly inputCtrl = new FormControl('', { nonNullable: true });
  private readonly query = toSignal(
    this.inputCtrl.valueChanges.pipe(
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

  protected pick(user: UserPublicProfileDto): void {
    this.inputCtrl.reset('');
    this.results.set([]);
    this.searched.set(false);
    void this.router.navigateByUrl(`/users/${user.id}`);
  }
}
