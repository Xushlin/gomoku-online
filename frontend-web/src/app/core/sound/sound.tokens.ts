/**
 * Sound layer contracts.
 *
 * `SoundEventName` is the closed set of UX events the rest of the app may
 * emit. Adding a sixth requires editing this union (TS exhaustiveness then
 * forces every registered pack to render it — or fall through silently).
 *
 * `SoundPack` is the shape of a pluggable audio "skin". A pack is given the
 * shared `AudioContext` and master `GainNode` and is expected to construct
 * a short-lived audio graph that auto-stops via `node.stop(when)`. Packs
 * MUST be synchronous (fire-and-forget — the browser schedules the actual
 * playback) and MUST NOT throw.
 */
export type SoundEventName =
  | 'move-place'
  | 'game-win'
  | 'game-lose'
  | 'game-draw'
  | 'urge';

export interface SoundPack {
  readonly play: (event: SoundEventName, ctx: AudioContext, masterGain: GainNode) => void;
}
