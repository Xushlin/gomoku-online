import { ChangeDetectionStrategy, Component, computed, input, output, signal } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';

import type { MoveDto, RoomState, Stone } from '../../../core/api/models/room.model';

/**
 * 成语接龙's play surface: the chain played so far, plus a box to add to it.
 *
 * The third and last board shape in the match family, and the one with no board.
 * Its inputs and outputs mirror `Board` and `XiangqiBoard` exactly — read any one
 * of the three and you have read the others.
 *
 * **It judges no legality.** `add-web-klotski` set the test for this: not *should
 * the client know the rules*, but *would knowing them produce a second truth that
 * can diverge*. 成语接龙 splits under that test, which is why the answer needs
 * stating rather than inferring from the neighbours. Two of its three rules — links
 * onto the previous word, not already played — are decidable from what is already
 * on screen. The third, *is it in the dictionary*, needs 30,895 words this client
 * does not and should not carry.
 *
 * So it shows the character the next word must start with, because that character
 * is the last character of a word already rendered — reading it out is display, not
 * adjudication — and it does not gate the submit button on it. Three reasons, in
 * order of weight:
 *
 *   1. A partly-authoritative input is worse than a non-authoritative one. If two
 *      refusals are instant and the third takes a round trip, the field behaves
 *      inconsistently for reasons the player cannot see.
 *   2. This client's history can be one ply stale. Blocking a word because it does
 *      not link to what *this* client last rendered can refuse a legal word.
 *   3. Refusal is now informative — each rule carries its own error code, so being
 *      told why costs one round trip and no guessing.
 */
@Component({
  selector: 'app-chain-board',
  standalone: true,
  imports: [TranslocoPipe],
  templateUrl: './chain-board.html',
  styles: [':host { display: block; width: 100%; }'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ChainBoard {
  readonly state = input<RoomState | null>(null);
  readonly mySide = input<'black' | 'white' | 'spectator'>('spectator');
  readonly submitting = input<boolean>(false);
  readonly = input<boolean>(false);
  readonly wordSay = output<string>();

  /**
   * The one cap the server actually has — `Move.Text`'s `HasMaxLength(64)`.
   *
   * Deliberately **not** 4. Measured against the shipped dictionary: 29,502 idioms
   * are four characters and **1,393 are not**, running 3 to 15 — 「一不做，二不休」
   * and 「各人自扫门前雪，莫管他家瓦上霜」 among them. A four-character cap would
   * make those unenterable. Some entries contain a full-width comma, so there is no
   * character-class filter either.
   */
  protected readonly maxWordLength = 64;

  /** What the player has typed. Cleared once a ply lands, not once one is sent. */
  protected readonly draft = signal('');

  protected readonly moves = computed<readonly MoveDto[]>(() => this.state()?.game?.moves ?? []);

  /**
   * The character the next word must begin with — the last character of the last
   * word played. `null` on move one, where any idiom in the dictionary is legal.
   */
  protected readonly requiredFirstChar = computed<string | null>(() => {
    const played = this.moves();
    const last = played.length > 0 ? played[played.length - 1].text : null;
    return last ? last[last.length - 1] : null;
  });

  /** `Stone` value of the seat the viewer occupies; `null` for spectators. */
  private readonly myStone = computed<Stone | null>(() => {
    const side = this.mySide();
    if (side === 'black') return 'Black';
    if (side === 'white') return 'White';
    return null;
  });

  private readonly myTurn = computed<boolean>(() => {
    const mine = this.myStone();
    return mine !== null && this.state()?.game?.currentTurn === mine;
  });

  /** Identical predicate to the other two boards, on purpose. */
  protected readonly inputDisabled = computed<boolean>(
    () =>
      this.readonly() ||
      this.submitting() ||
      this.mySide() === 'spectator' ||
      this.state()?.status !== 'Playing' ||
      !this.myTurn(),
  );

  protected readonly isSpectator = computed(() => this.mySide() === 'spectator');

  /** Blank is not a move; everything else is the server's call. */
  protected readonly canSubmit = computed(
    () => !this.inputDisabled() && this.draft().trim().length > 0,
  );

  protected onInput(event: Event): void {
    this.draft.set((event.target as HTMLInputElement).value);
  }

  protected submit(): void {
    const word = this.draft().trim();
    if (this.inputDisabled() || word.length === 0) return;
    this.wordSay.emit(word);
    this.draft.set('');
  }
}
