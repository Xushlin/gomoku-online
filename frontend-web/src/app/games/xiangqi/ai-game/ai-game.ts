import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import type { BotDifficulty, BotSide } from '../../../core/api/models/room.model';
import { RoomsApiService } from '../../../core/api/rooms-api.service';
import { XIANGQI_KEY } from '../game-key';

/**
 * 中国象棋 human-vs-AI entry.
 *
 * Deliberately the *whole* front end for this game: no lobby, no human-vs-human,
 * no leaderboard. `XiangqiRules.SupportsHumanVsHuman` is false, so a human-vs-human
 * entry would point at something the server refuses; `IsRated` is false, so a ladder
 * would be permanently empty. Parameterising gomoku's shipped `/home` lobby is a
 * decision about gomoku's UX and does not belong inside "let me play xiangqi".
 */
@Component({
  selector: 'app-xiangqi-ai-game',
  standalone: true,
  imports: [TranslocoPipe, RouterLink],
  templateUrl: './ai-game.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class XiangqiAiGame {
  private readonly rooms = inject(RoomsApiService);
  private readonly router = inject(Router);

  protected readonly difficulties: readonly BotDifficulty[] = ['Easy', 'Medium', 'Hard'];

  /**
   * `Black` is 红 and moves first — see `position.ts`. The labels players read say
   * 执红 / 执黑; the wire values stay `Black` / `White` because that is what the seat
   * is called everywhere else in the platform.
   */
  protected readonly sides: readonly BotSide[] = ['Black', 'White'];

  protected readonly difficulty = signal<BotDifficulty>('Medium');
  protected readonly side = signal<BotSide>('Black');
  protected readonly submitting = signal(false);
  protected readonly errorKey = signal<string | null>(null);

  protected pickDifficulty(d: BotDifficulty): void {
    this.difficulty.set(d);
  }

  protected pickSide(s: BotSide): void {
    this.side.set(s);
  }

  protected difficultyKey(d: BotDifficulty): string {
    return `xiangqi.difficulty-${d.toLowerCase()}`;
  }

  protected sideKey(s: BotSide): string {
    return `xiangqi.side-${s.toLowerCase()}`;
  }

  protected start(): void {
    if (this.submitting()) return;

    this.submitting.set(true);
    this.errorKey.set(null);

    this.rooms
      .createAiRoom(this.roomName(), this.difficulty(), this.side(), XIANGQI_KEY)
      .subscribe({
        next: (room) => {
          void this.router.navigateByUrl(`/rooms/${room.id}`);
        },
        error: () => {
          this.errorKey.set('xiangqi.error-create-failed');
          this.submitting.set(false);
        },
      });
  }

  /**
   * Same reasoning as 一字棋: an AI room appears in no lobby list, so this name is
   * visible to nobody. Asking the player to invent one would be pure friction. The
   * generated name satisfies the server's 3–50 character rule, which is NOT relaxed
   * for this — it also guards human-vs-human rooms, where the name matters.
   */
  private roomName(): string {
    return `中国象棋 vs AI_${this.difficulty()}`;
  }
}
