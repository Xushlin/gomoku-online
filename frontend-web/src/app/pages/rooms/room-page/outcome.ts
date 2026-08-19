import type { GameResult } from '../../../core/api/models/room.model';

/** How the game that just ended turned out **for me**. */
export type MyOutcome = 'win' | 'lose' | 'draw';

/**
 * The two fields the judgement needs — narrower than `GameEndedDto` on purpose, so both
 * the hub payload and the dialog's own data satisfy it without either being converted.
 */
export interface DecidedGame {
  readonly result: GameResult;
  readonly winnerUserId: string | null;
}

/**
 * Whether I won, lost or drew.
 *
 * The judgement is `winnerUserId === myUserId` and it lives here **once**. Two call
 * sites need it — the end-of-game dialog's title and the win/lose sound — and two
 * copies of one comparison disagree exactly when it matters: the dialog says you won
 * while the speaker plays the losing sting.
 *
 * It used to be `result === 'BlackWin' && mySide === 'black'`, which needed two
 * mirrors: which colour I am, and which colour won. A spectator holds neither, so it
 * fell through to `'lose'` — **every game told every spectator they had lost.** Now a
 * spectator is simply not the winner, which is true, and the branch that reaches them
 * is the same one that reaches a defeated player.
 *
 * @param ended The result and winner of the game that just ended.
 * @param myUserId The signed-in user's id; `null` / `undefined` for a spectator who
 *   is not signed in.
 */
export function myOutcome(ended: DecidedGame, myUserId: string | null | undefined): MyOutcome {
  if (ended.result === 'Draw') return 'draw';
  return ended.winnerUserId !== null && ended.winnerUserId === myUserId ? 'win' : 'lose';
}
