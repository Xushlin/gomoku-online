/**
 * The game key, shared by the manifest, the route, the board switch, and every
 * API call.
 *
 * It must match `GameKeys.Xiangqi` on the server — that string is what the rules
 * and AI registries resolve on, so a typo here produces a rejected request rather
 * than a compile error.
 */
export const XIANGQI_KEY = 'xiangqi';
