/**
 * Maps a hub command error (usually a `HubException` whose message is the
 * English domain exception text) to a translation key under `game.errors.*`.
 * Match is case-insensitive substring on the error's `message`.
 */
export type HubErrorKey =
  | 'game.errors.not-your-turn'
  | 'game.errors.invalid-move'
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
  if (m.includes('invalid move') || m.includes('occupied') || m.includes('out of bounds')) {
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
