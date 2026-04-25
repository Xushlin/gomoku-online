import { HttpErrorResponse } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import type { UserPublicProfileDto } from '../../../core/api/models/user-profile.model';
import { PresenceApiService } from '../../../core/api/presence-api.service';
import { UsersApiService } from '../../../core/api/users-api.service';
import { LanguageService } from '../../../core/i18n/language.service';
import { GamesList } from './games-list/games-list';

@Component({
  selector: 'app-profile-page',
  standalone: true,
  imports: [CommonModule, GamesList, RouterLink, TranslocoPipe],
  templateUrl: './profile-page.html',
  styles: [':host { display: block; width: 100%; }'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProfilePage implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly users = inject(UsersApiService);
  private readonly presenceApi = inject(PresenceApiService);
  protected readonly language = inject(LanguageService);

  protected userId = signal<string | null>(null);
  protected readonly profile = signal<UserPublicProfileDto | null>(null);
  protected readonly loading = signal<boolean>(true);
  protected readonly notFound = signal<boolean>(false);
  protected readonly loadError = signal<boolean>(false);
  /** Presence state. `null` = unknown / failed → don't render the dot. */
  protected readonly presence = signal<boolean | null>(null);

  protected readonly winRateLabel = computed<string>(() => {
    const p = this.profile();
    if (!p) return '—';
    const denom = p.wins + p.losses + p.draws;
    if (denom === 0) return '—';
    return `${((p.wins / denom) * 100).toFixed(1)}%`;
  });

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.notFound.set(true);
      this.loading.set(false);
      return;
    }
    this.userId.set(id);
    this.fetch(id);
  }

  protected retry(): void {
    const id = this.userId();
    if (id) this.fetch(id);
  }

  private fetch(id: string): void {
    this.loading.set(true);
    this.notFound.set(false);
    this.loadError.set(false);
    this.presence.set(null);
    this.users.getProfile(id).subscribe({
      next: (p) => {
        this.profile.set(p);
        this.loading.set(false);
      },
      error: (err: unknown) => {
        this.loading.set(false);
        if (err instanceof HttpErrorResponse && err.status === 404) {
          this.notFound.set(true);
          return;
        }
        this.loadError.set(true);
      },
    });
    this.presenceApi.getUserOnline(id).subscribe({
      next: (online) => this.presence.set(online),
      error: () => this.presence.set(null),
    });
  }
}
