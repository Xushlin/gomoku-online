/**
 * Maps a hub command error (usually a `HubException` whose message is the
 * English domain exception text) to a translation key under `game.errors.*`.
 * Match is case-insensitive substring on the error's `message`.
 *
 * ⚠️ This is a fuzzy match against the server's English prose — effectively a
 * second copy of the domain's exception wording, kept in sync by nothing. That was
 * tolerable while the only reachable failures were "not your turn" and a concurrency
 * clash: an unmapped message fell through to a generic toast that a player would
 * almost never see.
 *
 * 中国象棋 changes the stakes. Its board deliberately knows no rules, so an illegal
 * attempt is the ordinary way a player discovers what a piece can do — and
 * "Something went wrong" reads as a broken app, not as a refused move. The proper
 * fix is a structured error code on the hub contract rather than more phrases here;
 * that is a cross-cutting change to error handling and is recorded as a follow-up.
 */
export type HubErrorKey =
  | 'game.errors.not-your-turn'
  | 'game.errors.invalid-move'
  | 'game.errors.self-check'
  | 'game.errors.concurrent-move-refetched'
  | 'game.errors.urge-cooldown'
  | 'game.errors.network'
  | 'game.errors.generic';

export function hubErrorToKey(err: unknown): HubErrorKey {
  const message = extractMessage(err);
  if (!message) return 'game.errors.generic';
  const m = message.toLowerCase();

  if (m.includes('no connection') || m.includes('not started') || m.includes('disconnected')) {
    return 'game.errors.network';
  }
  if (m.includes('not your turn') || m.includes('notopponent')) {
    return 'game.errors.not-your-turn';
  }
  if (m.includes('too frequent') || m.includes('urgetoo')) {
    return 'game.errors.urge-cooldown';
  }
  if (m.includes('concurrent') || m.includes('dbupdateconcurrency')) {
    return 'game.errors.concurrent-move-refetched';
  }
  // Leaving your own general attacked (self-check, or the two generals facing down
  // an open file) gets its own message. It is the single most common refusal in
  // xiangqi, and "that move is not legal" does not tell the player what they missed.
  if (m.includes('in check')) {
    return 'game.errors.self-check';
  }
  if (
    m.includes('invalid move') ||
    m.includes('occupied') ||
    m.includes('out of bounds') ||
    // Xiangqi's phrasings. The client deliberately knows no rules (it would be a
    // second source of truth), so an illegal attempt is a NORMAL event here rather
    // than the near-impossibility it is in gomoku, where you can only click an empty
    // cell. Falling through to "something went wrong" reads as a system fault.
    m.includes('cannot move from') ||
    m.includes('there is no piece at') ||
    m.includes('does not belong to') ||
    m.includes('must change the piece') ||
    m.includes('origin square') ||
    m.includes('outside the')
  ) {
    return 'game.errors.invalid-move';
  }
  return 'game.errors.generic';
}

function extractMessage(err: unknown): string | null {
  if (!err) return null;
  if (typeof err === 'string') return err;
  if (err instanceof Error) return err.message;
  if (typeof err === 'object' && 'message' in err) {
    const m = (err as { message?: unknown }).message;
    if (typeof m === 'string') return m;
  }
  return null;
}
