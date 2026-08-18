import type { SoundPack } from '../sound.tokens';
import { chiptunePack } from './chiptune';
import { minimalPack } from './minimal';
import { woodPack } from './wood';

/**
 * The built-in packs, as one list.
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
 */
export const BUILT_IN_PACKS: Readonly<Record<string, SoundPack>> = {
  wood: woodPack,
  chiptune: chiptunePack,
  minimal: minimalPack,
};

export { chiptunePack, minimalPack, woodPack };
