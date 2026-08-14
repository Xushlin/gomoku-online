/**
 * The game key, shared by the manifest, the route, and every API call.
 *
 * It must match `GameKeys.TicTacToe` on the server — that string is what the
 * rules and AI registries resolve on, so a typo here produces a rejected
 * request rather than a compile error.
 */
export const TICTACTOE_KEY = 'tictactoe';
