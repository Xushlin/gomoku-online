import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { PuzzleApiService } from '../../../core/api/puzzle-api.service';
import { IDIOM_GUESS_KEY } from '../game-key';
import {
  blankKey,
  type IdiomGuessLayout,
  type IdiomGuessPuzzle,
  type IdiomGuessRevealed,
  type IdiomGuessSolved,
} from '../model';

type Phase = 'loading' | 'playing' | 'done' | 'error';

/** 一道题在界面上的可变状态。 */
interface PuzzleState {
  readonly puzzle: IdiomGuessPuzzle;
  /** 玩家填进空位的字,键是 `题号:位置`。 */
  readonly filled: Record<string, string>;
  /** 已经答对并锁定。 */
  readonly solved: boolean;
  /** 答对后服务端给的出处;**没有就是 null**,那时不画纸条。 */
  readonly derivation: string | null;
  /** 刚答错 —— 用来抖一下。 */
  readonly wrong: boolean;
}

/**
 * 猜成语的关卡页。
 *
 * **客户端不持有答案,也不自己判对错。** 每填满一条就发一次 `check`,由服务端回答;
 * 答对之后那条**出处**也来自服务端的载荷 —— 词典没有 HTTP 面,客户端拼不出来。
 */
@Component({
  selector: 'app-idiom-guess-play',
  standalone: true,
  imports: [TranslocoPipe],
  templateUrl: './play.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class IdiomGuessPlay {
  private readonly api = inject(PuzzleApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly phase = signal<Phase>('loading');
  protected readonly puzzles = signal<readonly PuzzleState[]>([]);
  protected readonly attemptId = signal<string | null>(null);
  protected readonly hintsUsed = signal(0);
  protected readonly mistakes = signal(0);
  protected readonly stars = signal<number | null>(null);
  protected readonly busy = signal(false);
  /** 光标停在哪个空位上 —— 提示会优先揭它。 */
  protected readonly selected = signal<string | null>(null);

  protected readonly levelIndex = signal(0);
  protected readonly allSolved = computed(
    () => this.puzzles().length > 0 && this.puzzles().every((p) => p.solved),
  );

  constructor() {
    const raw = Number(this.route.snapshot.paramMap.get('index'));
    this.levelIndex.set(Number.isFinite(raw) && raw >= 0 ? raw : 0);
    this.start();
  }

  protected start(): void {
    this.phase.set('loading');
    this.hintsUsed.set(0);
    this.mistakes.set(0);
    this.stars.set(null);
    this.api.startAttempt(IDIOM_GUESS_KEY, this.levelIndex()).subscribe({
      next: (attempt) => {
        const layout = this.api.parseLayout<IdiomGuessLayout>(attempt.layoutJson);
        if (!layout?.puzzles?.length) {
          this.phase.set('error');
          return;
        }
        this.attemptId.set(attempt.attemptId);
        this.puzzles.set(
          layout.puzzles.map((puzzle) => ({
            puzzle,
            filled: {},
            solved: false,
            derivation: null,
            wrong: false,
          })),
        );
        this.selected.set(this.firstBlank());
        this.phase.set('playing');
      },
      error: () => this.phase.set('error'),
    });
  }

  /** 第一个还空着的位置。没有则 null。 */
  private firstBlank(): string | null {
    for (const state of this.puzzles()) {
      for (let i = 0; i < state.puzzle.chars.length; i++) {
        if (state.puzzle.chars[i] === null && !state.filled[blankKey(state.puzzle.index, i)]) {
          return blankKey(state.puzzle.index, i);
        }
      }
    }
    return null;
  }

  protected isBlank(puzzle: IdiomGuessPuzzle, position: number): boolean {
    return puzzle.chars[position] === null;
  }

  /** 这一格该显示什么:原本就有的字、玩家填的字,或空。 */
  protected charAt(state: PuzzleState, position: number): string {
    return state.puzzle.chars[position] ?? state.filled[blankKey(state.puzzle.index, position)] ?? '';
  }

  protected keyFor(state: PuzzleState, position: number): string {
    return blankKey(state.puzzle.index, position);
  }

  protected select(key: string): void {
    this.selected.set(key);
  }

  /** 一个空位上敲进一个字。填满这一条就自动发 `check`。 */
  protected type(state: PuzzleState, position: number, value: string): void {
    if (state.solved) return;
    // 只取第一个字符 —— 输入法可能一次给来一串。
    const char = [...value].filter((c) => c.trim().length > 0)[0] ?? '';
    const key = blankKey(state.puzzle.index, position);

    this.puzzles.update((all) =>
      all.map((s) =>
        s.puzzle.index === state.puzzle.index
          ? { ...s, filled: { ...s.filled, [key]: char }, wrong: false }
          : s,
      ),
    );

    const current = this.puzzles().find((s) => s.puzzle.index === state.puzzle.index)!;
    if (this.isComplete(current)) {
      this.check(current);
    }
  }

  private isComplete(state: PuzzleState): boolean {
    return state.puzzle.chars.every(
      (c, i) => c !== null || (state.filled[blankKey(state.puzzle.index, i)] ?? '') !== '',
    );
  }

  private wordOf(state: PuzzleState): string {
    return state.puzzle.chars
      .map((c, i) => c ?? state.filled[blankKey(state.puzzle.index, i)] ?? '')
      .join('');
  }

  private check(state: PuzzleState): void {
    const attemptId = this.attemptId();
    if (!attemptId) return;
    this.busy.set(true);

    this.api
      .check<IdiomGuessSolved>(attemptId, {
        puzzleIndex: state.puzzle.index,
        word: this.wordOf(state),
      })
      .subscribe({
        next: (result) => {
          this.mistakes.set(result.mistakes);
          this.puzzles.update((all) =>
            all.map((s) =>
              s.puzzle.index === state.puzzle.index
                ? {
                    ...s,
                    solved: result.isCorrect,
                    // 出处**可能没有** —— null 时模板不画那张纸条,画了就像加载失败。
                    derivation: result.isCorrect ? (result.solved?.derivation ?? null) : null,
                    wrong: !result.isCorrect,
                  }
                : s,
            ),
          );
          this.busy.set(false);
          if (result.isCorrect) {
            this.selected.set(this.firstBlank());
            if (this.allSolved()) this.submit();
          }
        },
        error: () => this.busy.set(false),
      });
  }

  protected hint(): void {
    const attemptId = this.attemptId();
    if (!attemptId || this.busy()) return;
    this.busy.set(true);

    const filled = this.puzzles().flatMap((s) =>
      Object.entries(s.filled)
        .filter(([, v]) => v !== '')
        .map(([k]) => k),
    );

    this.api
      .hint<IdiomGuessRevealed>(attemptId, { selected: this.selected(), filled })
      .subscribe({
        next: (result) => {
          this.hintsUsed.set(result.hintsUsed);
          const revealed = result.revealed;
          if (revealed) {
            const key = blankKey(revealed.puzzleIndex, revealed.position);
            this.puzzles.update((all) =>
              all.map((s) =>
                s.puzzle.index === revealed.puzzleIndex
                  ? { ...s, filled: { ...s.filled, [key]: revealed.char }, wrong: false }
                  : s,
              ),
            );
            const target = this.puzzles().find((s) => s.puzzle.index === revealed.puzzleIndex)!;
            if (this.isComplete(target) && !target.solved) {
              this.check(target);
              return;
            }
          }
          this.busy.set(false);
        },
        error: () => this.busy.set(false),
      });
  }

  private submit(): void {
    const attemptId = this.attemptId();
    if (!attemptId) return;

    const words: Record<string, string> = {};
    for (const state of this.puzzles()) {
      words[String(state.puzzle.index)] = this.wordOf(state);
    }

    this.api.submit(attemptId, { words }).subscribe({
      next: (result) => {
        this.stars.set(result.stars);
        this.hintsUsed.set(result.hintsUsed);
        this.mistakes.set(result.mistakes);
        this.phase.set('done');
      },
      error: () => this.phase.set('error'),
    });
  }

  protected backToList(): void {
    void this.router.navigate(['/g/idiom-guess']);
  }
}
