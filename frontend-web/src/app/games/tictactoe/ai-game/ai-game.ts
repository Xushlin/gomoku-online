import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import type { BotDifficulty, BotSide } from '../../../core/api/models/room.model';
import { RoomsApiService } from '../../../core/api/rooms-api.service';
import { TICTACTOE_KEY } from '../game-key';

/**
 * 一字棋 human-vs-AI entry.
 *
 * Deliberately the *whole* front end for this game: no lobby, no human-vs-human,
 * no leaderboard. The game is unrated, so a leaderboard would have nothing in it;
 * and parameterising gomoku's shipped `/home` lobby is a decision about gomoku's
 * UX that should not ride along inside "let me play tic-tac-toe".
 */
@Component({
  selector: 'app-tictactoe-ai-game',
  standalone: true,
  imports: [TranslocoPipe, RouterLink],
  templateUrl: './ai-game.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TicTacToeAiGame {
  private readonly rooms = inject(RoomsApiService);
  private readonly router = inject(Router);

  protected readonly difficulties: readonly BotDifficulty[] = ['Easy', 'Medium', 'Hard'];
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
    return `tictactoe.difficulty-${d.toLowerCase()}`;
  }

  protected sideKey(s: BotSide): string {
    return `tictactoe.side-${s.toLowerCase()}`;
  }

  protected start(): void {
    if (this.submitting()) return;

    this.submitting.set(true);
    this.errorKey.set(null);

    this.rooms
      .createAiRoom(this.roomName(), this.difficulty(), this.side(), TICTACTOE_KEY)
      .subscribe({
        next: (room) => {
          void this.router.navigateByUrl(`/rooms/${room.id}`);
        },
        error: () => {
          this.errorKey.set('tictactoe.error-create-failed');
          this.submitting.set(false);
        },
      });
  }

  /**
   * Room names exist so strangers can recognise a room in a lobby list. An AI room
   * appears in no list — the lobby query filters by game and 一字棋 has no lobby —
   * so this name is visible to nobody, including the player. Asking them to invent
   * one would be pure friction, so the client makes one that satisfies the server's
   * 3–50 character rule. The server's validation is NOT relaxed for this: it also
   * guards human-vs-human rooms, where the name does matter.
   */
  private roomName(): string {
    return `一字棋 vs AI_${this.difficulty()}`;
  }
}
