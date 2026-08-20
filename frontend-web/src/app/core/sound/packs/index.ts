import type { SoundPack } from '../sound.tokens';

/**
 * The built-in packs, as one list of **loaders**.
 *
 * `DefaultSoundService` registers by walking this, and the pack tests take their
 * subjects from it. Both used to be written out by hand, which is the defect this
 * repo has paid for three times: `GomokuRules.Registry` and `GomokuRules.AiRegistry`
 * were each a hand-written fixture under a comment claiming it matched production
 * DI, while production had grown a third entry. `BuiltInGameRules.All` and
 * `BuiltInGameAis.All` exist for exactly that reason; this is the same fix at the
 * smallest possible size.
 *
 * Insertion order is `availablePacks()` order, which is the order the header's
 * menu renders — so it is not arbitrary.
 *
 * **They are loaders rather than the packs themselves, and that is a measurement.**
 * `SoundService` is injected in `provideAppInitializer`, so a static import here put
 * all three pack bodies in the **initial** bundle. Measured by stubbing the three
 * files and rebuilding: **481.23 kB → 472.54 kB, i.e. the packs were 8.69 kB of
 * first paint** — for audio that cannot make a sound until the user has interacted
 * with the page at least once. `add-card-sounds` is what pushed the 480 kB budget
 * over, and this is the option CLAUDE.md had already named for exactly this moment.
 *
 * The cost is that the first `play()` of a session may resolve after the event that
 * asked for it. `DefaultSoundService` warms the active pack at construction (not
 * awaited) so that window is normally closed before anything plays, and a `play()`
 * that arrives first is **queued, not dropped** — a silent first move would be a
 * defect, a slightly late one is not.
 */
export const PACK_LOADERS: Readonly<Record<string, () => Promise<SoundPack>>> = {
  wood: () => import('./wood').then((m) => m.woodPack),
  chiptune: () => import('./chiptune').then((m) => m.chiptunePack),
  minimal: () => import('./minimal').then((m) => m.minimalPack),
};

/**
 * Pack names, derived from the loader map.
 *
 * The i18n parity walk needs the names and nothing else; importing the packs to
 * read their keys would drag all three back into whatever bundle asked.
 */
export const PACK_NAMES: readonly string[] = Object.keys(PACK_LOADERS);
