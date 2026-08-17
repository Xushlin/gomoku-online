import type { GameDescriptor } from '../core/api/models/game-descriptor.model';
import { GameCapabilitiesService } from './game-capabilities.service';

/**
 * Test double for {@link GameCapabilitiesService}: hand it the descriptors the
 * case needs, synchronously.
 *
 * Lives in `src/` rather than a spec file because three suites need it, and a
 * copy per suite is three chances for them to drift into disagreeing about what
 * the server says.
 */
export class StubGameCapabilities extends GameCapabilitiesService {
  private readonly byKey: Map<string, GameDescriptor>;

  constructor(descriptors: readonly GameDescriptor[] = []) {
    super();
    this.byKey = new Map(descriptors.map((d) => [d.gameKey, d]));
  }

  /** Convenience: `rated('gomoku', 'xiangqi')` — 15×15 human-vs-human, rated. */
  static rated(...keys: readonly string[]): StubGameCapabilities {
    return new StubGameCapabilities(
      keys.map((gameKey) => ({
        gameKey,
        isRated: true,
        supportsHumanVsHuman: true,
        rows: 15,
        cols: 15,
      })),
    );
  }

  /** Board dimensions for one key, for suites that care about the size. */
  static sized(entries: Readonly<Record<string, { rows: number; cols: number }>>): StubGameCapabilities {
    return new StubGameCapabilities(
      Object.entries(entries).map(([gameKey, { rows, cols }]) => ({
        gameKey,
        isRated: false,
        supportsHumanVsHuman: false,
        rows,
        cols,
      })),
    );
  }

  /** Registered, but human-vs-AI only — 一字棋 and 象棋 are the real cases. */
  static aiOnly(...keys: readonly string[]): StubGameCapabilities {
    return new StubGameCapabilities(
      keys.map((gameKey) => ({
        gameKey,
        isRated: false,
        supportsHumanVsHuman: false,
        rows: 3,
        cols: 3,
      })),
    );
  }

  /** A stub that has not finished loading — for the "hold the render" cases. */
  static pending(): StubGameCapabilities {
    const stub = new StubGameCapabilities();
    stub.settled = false;
    return stub;
  }

  private settled = true;

  ensureLoaded(): void {
    // Already loaded, unless the case asked for `pending()`.
  }

  of(gameKey: string): GameDescriptor | undefined {
    return this.byKey.get(gameKey);
  }

  ratedKeys(): readonly string[] {
    return [...this.byKey.values()].filter((d) => d.isRated).map((d) => d.gameKey);
  }

  loaded(): boolean {
    return this.settled;
  }
}
