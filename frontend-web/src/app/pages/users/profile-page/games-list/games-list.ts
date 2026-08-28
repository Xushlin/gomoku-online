import { CommonModule } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  signal,
} from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import type {
  PagedResult,
  UserGameSummaryDto,
} from '../../../../core/api/models/user-profile.model';
import type { UserSummary } from '../../../../core/api/models/room.model';
import { opponentsOf, outcomeKeyFor } from '../../../../games/game-history';
import { UsersApiService } from '../../../../core/api/users-api.service';
import { LanguageService } from '../../../../core/i18n/language.service';

const PAGE_SIZE = 10;

@Component({
  selector: 'app-games-list',
  standalone: true,
  imports: [CommonModule, RouterLink, TranslocoPipe],
  templateUrl: './games-list.html',
  styles: [':host { display: block; width: 100%; }'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class GamesList {
  private readonly users = inject(UsersApiService);
  private readonly router = inject(Router);
  protected readonly language = inject(LanguageService);

  readonly userId = input.required<string>();

  protected readonly page = signal<number>(1);
  protected readonly games = signal<readonly UserGameSummaryDto[]>([]);
  protected readonly total = signal<number>(0);
  protected readonly loading = signal<boolean>(false);
  protected readonly error = signal<boolean>(false);
  protected readonly pageSize = PAGE_SIZE;

  protected readonly totalPages = computed(() =>
    Math.max(1, Math.ceil(this.total() / PAGE_SIZE)),
  );
  protected readonly hasPrev = computed(() => this.page() > 1);
  protected readonly hasNext = computed(() => this.page() < this.totalPages());

  constructor() {
    effect((onCleanup) => {
      const id = this.userId();
      const page = this.page();
      this.loading.set(true);
      this.error.set(false);
      const sub = this.users.getGames(id, page, PAGE_SIZE).subscribe({
        next: (r: PagedResult<UserGameSummaryDto>) => {
          this.games.set(r.items);
          this.total.set(r.total);
          this.loading.set(false);
        },
        error: () => {
          this.games.set([]);
          this.error.set(true);
          this.loading.set(false);
        },
      });
      onCleanup(() => sub.unsubscribe());
    });
  }

  /** 除本人以外的每一个座位 —— 三人局有**两个**对手。见 `games/game-history.ts`。 */
  protected opponentsOf(g: UserGameSummaryDto): readonly UserSummary[] {
    return opponentsOf(g.seats, this.userId());
  }

  /**
   * 「我方视角」的结果,四支之一 —— 第四支是「这一行说不出」。
   *
   * **两处消费方走同一个函数**,因为一个用户同时看得见大厅卡片和个人主页:
   * 两份副本分叉的症状是同一局对局在两处给出不同的答案。
   */
  protected resultKey(g: UserGameSummaryDto): string {
    return outcomeKeyFor(g, this.userId());
  }

  protected reasonKey(g: UserGameSummaryDto): string {
    switch (g.endReason) {
      case 'Decided':
        return 'game.ended.reason-decided';
      case 'Resigned':
        return 'game.ended.reason-resigned';
      case 'TurnTimeout':
        return 'game.ended.reason-timeout';
    }
  }

  protected onRowClick(roomId: string): void {
    void this.router.navigateByUrl(`/replay/${roomId}`);
  }

  protected prevPage(): void {
    if (this.hasPrev()) this.page.set(this.page() - 1);
  }

  protected nextPage(): void {
    if (this.hasNext()) this.page.set(this.page() + 1);
  }
}
