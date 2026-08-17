/**
 * Server-declared capabilities of a registered versus game — mirrors the
 * backend's `Gewu.Application.Common.DTOs.GameDescriptorDto`, which is a
 * read-only projection of `IGameRulesRegistry`.
 *
 * This exists so the client never has to keep its own copy of "which games are
 * rated". The manifest already carries one deliberate copy of server data (the
 * board dimensions), and that one is tolerable because a mismatch shows up as a
 * visibly wrong number of cells and the server rejects out-of-range moves.
 * A `rated` copy would have neither safety net: its failure mode is *a
 * permanently empty leaderboard*, which looks exactly like a new game nobody
 * has played yet. A mismatch you cannot see is a mismatch nobody will fix.
 *
 * Only versus games appear here. Puzzle games run on `IPuzzleRules` and have
 * their own REST surface; merging the two would produce a DTO with half its
 * fields always null.
 */
export interface GameDescriptor {
  /** Game key, matching the room's `gameKey` and the manifest's `key`. */
  readonly gameKey: string;

  /** Whether finished games settle ELO — i.e. whether this game has a ladder. */
  readonly isRated: boolean;

  /** Whether the platform offers human-vs-human for this game. */
  readonly supportsHumanVsHuman: boolean;

  /**
   * Whether this game has a computer opponent — projected from
   * `IGameAiRegistry`, the same registry `POST /api/rooms/ai` validates against,
   * so what the client shows and what the server accepts cannot disagree.
   *
   * 成语接龙 is the first game with human play and no AI, and the reason it
   * needs a field rather than an assumption is not cosmetic: before this was
   * enforced, creating an AI room for it produced a live rated game against a
   * bot that could never move, which the turn timeout then awarded to the human.
   */
  readonly supportsAi: boolean;

  /**
   * Board rows — `null` when the game has **no board at all** (成语接龙).
   *
   * That is a different claim from "this client does not know the game", and the
   * two must not share a fallback: routing "no board" through `DEFAULT_BOARD`
   * would describe a word game as a 15×15 gomoku grid.
   */
  readonly rows: number | null;

  /** Board columns; `null` whenever {@link GameDescriptor.rows} is. */
  readonly cols: number | null;
}
