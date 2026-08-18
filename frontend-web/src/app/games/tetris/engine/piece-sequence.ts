import { TETROMINO_KINDS, type TetrominoKind } from './tetromino';

/** Pieces per bag — one of each kind. */
export const BAG_SIZE = 7;

/**
 * `seed → piece sequence`, bit-identical to the backend's `TetrisPieceSequence`.
 *
 * **This is the one thing in this game that legitimately has two
 * implementations**, and the reason it is allowed is that it is a pure function
 * a test can align item by item. `add-tetris` did exactly that with a third
 * implementation in Python: for seed `20260818` all three agree on the first 21
 * pieces (three whole bags). `piece-sequence.spec.ts` pins those same numbers.
 *
 * Seven-bag rather than uniform random: long runs of one kind make the score
 * depend on luck instead of play, and a leaderboard needs the opposite.
 *
 * xorshift32 rather than `Math.random` for the same reason the server does not
 * use `System.Random` — the sequence must be reproducible across runtimes *and*
 * languages, forever, because every historical run is replayed from its seed.
 */
export function pieceSequence(seed: number, count: number): readonly TetrominoKind[] {
  if (count < 0) throw new RangeError('count must not be negative');

  const out: TetrominoKind[] = [];
  // State 0 would leave xorshift stuck at 0 forever — that degenerates into
  // "always the first kind". The server substitutes the same constant.
  let state = (seed >>> 0) === 0 ? 0x9e3779b9 : seed >>> 0;

  while (out.length < count) {
    const bag: TetrominoKind[] = [...TETROMINO_KINDS];

    // Fisher–Yates, back to front — shuffle one bag.
    for (let i = BAG_SIZE - 1; i > 0; i--) {
      state = nextState(state);
      const j = state % (i + 1);
      [bag[i], bag[j]] = [bag[j], bag[i]];
    }

    for (const kind of bag) {
      if (out.length === count) break;
      out.push(kind);
    }
  }

  return out;
}

/**
 * xorshift32.
 *
 * The **final** `>>> 0` is load-bearing, and only that one. JavaScript's `<<` and
 * `^` return a *signed* 32-bit value, so without it `state % (i + 1)` can come out
 * negative and the shuffle swaps with `bag[-3]` — `undefined` in the sequence, which
 * is how a whole run stops matching the server's. The server's `uint` arithmetic has
 * no such hazard; this is the one place a faithful port needs code the original does
 * not.
 *
 * The intermediate `>>> 0`s are **not** load-bearing, and I originally wrote a
 * comment here claiming they were. Mutation testing said otherwise: removing the
 * first one leaves all 34 tests green, because `<<`, `^` and `>>>` all operate on
 * the same 32 bits regardless of how the value is *signed*, and only the final
 * coercion feeds `%`. They stay for readability — a state variable that is always in
 * unsigned range is easier to compare against the C# — but the reason is now the
 * true one.
 */
function nextState(state: number): number {
  let s = state >>> 0;
  s = (s ^ (s << 13)) >>> 0;
  s = (s ^ (s >>> 17)) >>> 0;
  s = (s ^ (s << 5)) >>> 0;
  return s;
}
