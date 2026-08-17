import { describe, expect, it } from 'vitest';
import en from '../../../../../public/i18n/en.json';
import zhCN from '../../../../../public/i18n/zh-CN.json';
import { hubErrorToKey } from './hub-error.mapper';

/**
 * The mapper reads the server's **error code**, not its prose.
 *
 * It used to fuzzy-match English exception text, and that only worked in
 * Development: a hub method throwing a plain exception has its message delivered
 * to the client only when `EnableDetailedErrors` is on, and that is
 * `IsDevelopment()`. In production every keyword missed. Measured, not deduced —
 * the same illegal 象棋 move read "That move isn't allowed." in Development and
 * "Something went wrong. Please try again." in Production.
 */
describe('hubErrorToKey', () => {
  it.each([
    ['not-your-turn', 'game.errors.not-your-turn'],
    ['invalid-move', 'game.errors.invalid-move'],
    ['self-check', 'game.errors.self-check'],
    ['idiom-not-found', 'game.errors.idiom-not-found'],
    ['idiom-does-not-link', 'game.errors.idiom-does-not-link'],
    ['idiom-already-used', 'game.errors.idiom-already-used'],
    ['room-not-in-play', 'game.errors.room-not-in-play'],
    ['not-a-player', 'game.errors.not-a-player'],
    ['not-opponents-turn', 'game.errors.not-opponents-turn'],
    ['invalid-chat-content', 'game.errors.invalid-chat'],
    ['spectator-channel-forbidden', 'game.chat.forbidden-error'],
    ['urge-too-frequent', 'game.errors.urge-cooldown'],
    ['concurrent-modification', 'game.errors.concurrent-move-refetched'],
  ])('maps the code %s', (code, key) => {
    expect(hubErrorToKey(new Error(code))).toBe(key);
  });

  it('tolerates surrounding whitespace', () => {
    expect(hubErrorToKey(new Error('  not-your-turn  '))).toBe('game.errors.not-your-turn');
  });

  it('unwraps the message SignalR actually puts on the wire', () => {
    // Measured, not assumed — and identical with EnableDetailedErrors on and off.
    // An earlier draft compared the whole string and therefore still returned
    // generic even though the server was already sending codes.
    const wire =
      "An unexpected error occurred invoking 'MovePiece' on the server. HubException: invalid-move";

    expect(hubErrorToKey(new Error(wire))).toBe('game.errors.invalid-move');
  });

  it('gives each 成语接龙 refusal its own message, not one shared one', () => {
    // 「不是成语」「接不上」「说过了」are three different corrections, and the chain
    // board deliberately judges nothing — so the server's refusal is the only place
    // a player learns which rule they broke. Sharing invalid-move says none of them.
    const wire = (code: string) =>
      `An unexpected error occurred invoking 'SayWord' on the server. HubException: ${code}`;

    const keys = ['idiom-not-found', 'idiom-does-not-link', 'idiom-already-used'].map((c) =>
      hubErrorToKey(new Error(wire(c))),
    );

    expect(keys).toEqual([
      'game.errors.idiom-not-found',
      'game.errors.idiom-does-not-link',
      'game.errors.idiom-already-used',
    ]);
    expect(new Set(keys).size).toBe(3);
    expect(keys).not.toContain('game.errors.invalid-move');
    expect(keys).not.toContain('game.errors.generic');
  });

  it('unwraps a self-check the same way', () => {
    const wire =
      "An unexpected error occurred invoking 'MovePiece' on the server. HubException: self-check";

    expect(hubErrorToKey(new Error(wire))).toBe('game.errors.self-check');
  });

  it('accepts a bare string, not just an Error', () => {
    expect(hubErrorToKey('invalid-move')).toBe('game.errors.invalid-move');
  });

  it('falls back to generic for a code it does not know', () => {
    // A server that has grown a new error reaches an older client as an
    // unrecognised code. Generic is the right answer — and it is now the
    // exception rather than the rule.
    expect(hubErrorToKey(new Error('some-brand-new-code'))).toBe('game.errors.generic');
  });

  it('does not try to interpret prose', () => {
    // The old mapper would have called this an invalid move. Guessing from
    // English is exactly what stopped working in production, so a sentence that
    // is not a code gets the honest answer.
    expect(hubErrorToKey(new Error('A General cannot move from (9, 4) to (7, 4).'))).toBe(
      'game.errors.generic',
    );
    // Including when it arrives wrapped, which is how a non-HubException looks
    // in Development.
    expect(
      hubErrorToKey(
        new Error(
          "An unexpected error occurred invoking 'MovePiece' on the server. InvalidMoveException: A General cannot move from (9, 4) to (7, 4).",
        ),
      ),
    ).toBe('game.errors.generic');
    expect(hubErrorToKey(new Error('It is not your turn.'))).toBe('game.errors.generic');
  });

  it('still detects a dead connection, which is a client-side condition', () => {
    // No hub method ran, so there is no server code to read.
    expect(hubErrorToKey(new Error("No connection with id 'abc' was found"))).toBe(
      'game.errors.network',
    );
    expect(hubErrorToKey(new Error('Connection disconnected'))).toBe('game.errors.network');
  });

  it('null error maps to generic', () => {
    expect(hubErrorToKey(null)).toBe('game.errors.generic');
    expect(hubErrorToKey(undefined)).toBe('game.errors.generic');
    expect(hubErrorToKey({})).toBe('game.errors.generic');
  });

  it('every key it can return has copy in both locales', () => {
    // A mapping that resolves to a missing key shows the player a raw
    // `game.errors.x` on screen — which is worse than the generic message it
    // was meant to improve on.
    const codes = [
      'not-your-turn',
      'invalid-move',
      'self-check',
      'room-not-in-play',
      'not-a-player',
      'not-opponents-turn',
      'invalid-chat-content',
      'spectator-channel-forbidden',
      'urge-too-frequent',
      'concurrent-modification',
      'anything-unmapped',
      "No connection with id 'x'",
    ];

    for (const [locale, tree] of Object.entries({ 'zh-CN': zhCN, en })) {
      for (const code of codes) {
        const key = hubErrorToKey(new Error(code));
        const value = key
          .split('.')
          .reduce<unknown>((node, part) => (node as Record<string, unknown>)?.[part], tree);
        expect(typeof value, `${locale} is missing ${key}`).toBe('string');
        expect((value as string).length, `${locale} has an empty ${key}`).toBeGreaterThan(0);
      }
    }
  });
});
