/**
 * The game key, shared by the manifest, the routes, and every score-run call.
 *
 * It must match `TetrisRules.GameKey` on the server. That string is what
 * `ScoreAttackGames.IsScoreAttackGame` resolves on, so a typo here produces a
 * 404 from `POST /api/score-runs` rather than a compile error.
 *
 * Unlike the board games, this key is in no server-side rules registry — tetris
 * has no `IGameRules`, so it is absent from `GET /api/games` and the client
 * cannot learn about it from a descriptor. This constant is the only place it is
 * written down on this side.
 */
export const TETRIS_KEY = 'tetris';
