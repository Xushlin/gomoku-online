/**
 * The game key, shared by the manifest, the route, and every API call.
 *
 * It must match `GameKeys.Gomoku` on the server — that string is what the rules
 * and AI registries resolve on, so a typo here produces a rejected request rather
 * than a compile error.
 *
 * It exists as of `require-room-game-key`, which made `gameKey` mandatory on the
 * room endpoints. Until then gomoku was the one game whose key was never written
 * down on this side: the server substituted it for any request that omitted the
 * field, so the lobby's "this is gomoku" decision lived nowhere the client could
 * be read for it.
 */
export const GOMOKU_KEY = 'gomoku';
