import type { SoundEventName, SoundPack } from '../sound.tokens';

/**
 * Minimal sound pack — the quiet alternative. Identity: soft, short, sine-only
 * (wood owns noise/timbre, chiptune owns square/triangle). Peak gains sit at
 * roughly half of wood's so the pack reads as "unobtrusive" even at full
 * volume; durations stay under 400 ms total, the stone click under 80 ms.
 *
 * Event design:
 *   - move-place : single soft sine click, 660 Hz, ≤ 80 ms
 *   - urge       : two short 880 Hz clicks, 80 ms apart
 *   - game-win   : understated two-note rise C5 → G5
 *   - game-lose  : two-note fall G4 → C4
 *   - game-draw  : one neutral 440 Hz pulse
 *
 * Same contract as every pack: synchronous, no external resources, every node
 * auto-stops via `osc.stop(when)`, never throws.
 */
export const minimalPack: SoundPack = {
  play(event: SoundEventName, ctx: AudioContext, masterGain: GainNode): void {
    const now = ctx.currentTime;
    switch (event) {
      case 'move-place':
        click(ctx, masterGain, now, 660, 0.07, 0.16);
        return;
      case 'urge':
        click(ctx, masterGain, now, 880, 0.05, 0.15);
        click(ctx, masterGain, now + 0.08, 880, 0.05, 0.15);
        return;
      case 'game-win':
        note(ctx, masterGain, now, 523.25, 0.14, 0.14); // C5
        note(ctx, masterGain, now + 0.15, 783.99, 0.14, 0.14); // G5
        return;
      case 'game-lose':
        note(ctx, masterGain, now, 392.0, 0.14, 0.15); // G4
        note(ctx, masterGain, now + 0.15, 261.63, 0.14, 0.15); // C4
        return;
      case 'game-draw':
        note(ctx, masterGain, now, 440, 0.12, 0.11);
        return;
    }
  },
};

/** Very short percussive sine — steeper envelope than `note`. */
function click(ctx: AudioContext, dest: GainNode, start: number, freq: number, dur: number, peak: number): void {
  const osc = ctx.createOscillator();
  osc.type = 'sine';
  osc.frequency.value = freq;

  const gain = ctx.createGain();
  gain.gain.setValueAtTime(0, start);
  gain.gain.linearRampToValueAtTime(peak, start + 0.005);
  gain.gain.exponentialRampToValueAtTime(0.001, start + dur);

  osc.connect(gain).connect(dest);
  osc.start(start);
  osc.stop(start + dur + 0.01);
}

/** Tonal sine with a gentle attack and natural release. */
function note(ctx: AudioContext, dest: GainNode, start: number, freq: number, dur: number, peak: number): void {
  const osc = ctx.createOscillator();
  osc.type = 'sine';
  osc.frequency.value = freq;

  const gain = ctx.createGain();
  gain.gain.setValueAtTime(0, start);
  gain.gain.linearRampToValueAtTime(peak, start + 0.02);
  gain.gain.exponentialRampToValueAtTime(0.001, start + dur);

  osc.connect(gain).connect(dest);
  osc.start(start);
  osc.stop(start + dur + 0.02);
}
