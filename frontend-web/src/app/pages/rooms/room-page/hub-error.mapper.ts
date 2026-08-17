/**
 * Maps a hub command failure to a translation key.
 *
 * The `HubException` message **is** the server's error code — a stable kebab-case
 * identifier, never prose. See `DomainErrorHubFilter`.
 *
 * This used to fuzzy-match the server's English exception text, and that did not
 * work outside Development. A hub method throwing a plain exception only has its
 * message delivered when `EnableDetailedErrors` is on, and that is
 * `IsDevelopment()` — so in production every keyword missed and every failure
 * showed "Something went wrong." Measured, not deduced: the same illegal 象棋 move
 * read "That move isn't allowed." in Development and "Something went wrong. Please
 * try again." in Production.
 *
 * `HubException` is the fix because its message survives in **both** environments.
 * It does not arrive verbatim, though — SignalR wraps it. Measured on the wire,
 * identical under `EnableDetailedErrors` true and false:
 *
 * ```
 * "An unexpected error occurred invoking 'MovePiece' on the server. HubException: invalid-move"
 * ```
 *
 * So the code has to be extracted, not compared. That prefix is why an earlier
 * draft of this mapper still returned generic even after the server started
 * sending codes — the whole string was being looked up.
 */
export type HubErrorKey =
  | 'game.errors.not-your-turn'
  | 'game.errors.invalid-move'
  | 'game.errors.self-check'
  | 'game.errors.idiom-not-found'
  | 'game.errors.idiom-does-not-link'
  | 'game.errors.idiom-already-used'
  | 'game.errors.room-not-in-play'
  | 'game.errors.not-a-player'
  | 'game.errors.not-opponents-turn'
  | 'game.errors.invalid-chat'
  | 'game.chat.forbidden-error'
  | 'game.errors.concurrent-move-refetched'
  | 'game.errors.urge-cooldown'
  | 'game.errors.network'
  | 'game.errors.generic';

/**
 * Server code → translation key.
 *
 * Exhaustive by construction: a code that is not here falls back to generic, but
 * a *new* server error now arrives as its own code rather than as prose that
 * happens to miss every keyword — so the fallback stops being the common case.
 */
const BY_CODE: Readonly<Record<string, HubErrorKey>> = {
  'not-your-turn': 'game.errors.not-your-turn',
  'invalid-move': 'game.errors.invalid-move',
  'self-check': 'game.errors.self-check',
  // 三条接龙规则各有自己的一行,而不是共用 invalid-move。象棋能共用是因为玩家看着盘面能
  // 自己想明白;接龙没有盘面,而它的界面**故意不在客户端判合法性**,所以服务端的拒绝是
  // 玩家了解规则的唯一途径 —— 「这一步不合法」说不出「不是成语」「接不上」「说过了」中的任何一种。
  'idiom-not-found': 'game.errors.idiom-not-found',
  'idiom-does-not-link': 'game.errors.idiom-does-not-link',
  'idiom-already-used': 'game.errors.idiom-already-used',
  'room-not-in-play': 'game.errors.room-not-in-play',
  'not-a-player': 'game.errors.not-a-player',
  'not-opponents-turn': 'game.errors.not-opponents-turn',
  'invalid-chat-content': 'game.errors.invalid-chat',
  'spectator-channel-forbidden': 'game.chat.forbidden-error',
  'urge-too-frequent': 'game.errors.urge-cooldown',
  'concurrent-modification': 'game.errors.concurrent-move-refetched',
};

export function hubErrorToKey(err: unknown): HubErrorKey {
  const message = extractMessage(err);
  if (!message) return 'game.errors.generic';

  // Connection failures are a *client-side* condition — the request never reached
  // a hub method, so there is no server code to read. This one stays a text check.
  const m = message.toLowerCase();
  if (m.includes('no connection') || m.includes('not started') || m.includes('disconnected')) {
    return 'game.errors.network';
  }

  return BY_CODE[extractCode(message)] ?? 'game.errors.generic';
}

/**
 * Pull the server's code out of SignalR's wrapper.
 *
 * The wire form is `…on the server. HubException: <code>`. Anything that is not
 * wrapped (a bare code, a plain sentence) is used as-is — and a plain sentence
 * then simply misses the table, which is the honest outcome: guessing meaning
 * from English is what this change removed.
 */
function extractCode(message: string): string {
  const marker = 'HubException: ';
  const at = message.lastIndexOf(marker);
  return (at === -1 ? message : message.slice(at + marker.length)).trim();
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
