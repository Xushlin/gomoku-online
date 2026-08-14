/**
 * The game key, shared by the manifest, the routes, and every API call.
 *
 * It must match `IdiomCrosswordRules.GameKey` on the server — that string is
 * what the puzzle rules registry resolves on, so a typo here produces a 404
 * rather than a compile error.
 */
export const IDIOM_CROSSWORD_KEY = 'idiom-crossword';
