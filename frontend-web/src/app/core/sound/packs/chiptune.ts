import type { SoundEventName, SoundPack } from '../sound.tokens';

/**
 * Chiptune sound pack — 8-bit-style synthesis using only square and
 * triangle oscillators. Audibly distinct from `wood` (which uses sine +
 * filtered noise). No external assets, no fetch.
 *
 * Square waves carry more harmonic energy than sines at the same
 * numeric gain, so peak gains here are 30–50 % lower to keep perceived
 * loudness in line with `wood`.
 *
 * Event design:
 *   - move-place : square click ~50 ms, ~150 Hz, fast attack/decay
 *   - urge       : triangle sweep 300 → 700 Hz over 100 ms (8-bit alert)
 *   - game-win   : ascending square arpeggio C5/E5/G5 + flourish C6
 *                  ("level up" feel)
 *   - game-lose  : square descending 640 → 160 Hz over 700 ms
 *                  ("game over" feel)
 *   - game-draw  : two triangle 440 Hz pulses, neutral
 *
 * MUST NOT use sawtooth (too harsh for the events on this list).
 */
export const chiptunePack: SoundPack = {
  play(event: SoundEventName, ctx: AudioContext, masterGain: GainNode): void {
    const now = ctx.currentTime;
    switch (event) {
      case 'move-place':
        playMovePlace(ctx, masterGain, now);
        return;
      case 'urge':
        playUrge(ctx, masterGain, now);
        return;
      case 'game-win':
        playWin(ctx, masterGain, now);
        return;
      case 'game-lose':
        playLose(ctx, masterGain, now);
        return;
      case 'game-draw':
        playDraw(ctx, masterGain, now);
        return;
    }
  },
};

function playMovePlace(ctx: AudioContext, dest: GainNode, now: number): void {
  const duration = 0.05;
  const osc = ctx.createOscillator();
  osc.type = 'square';
  osc.frequency.value = 150;
  const gain = ctx.createGain();
  gain.gain.setValueAtTime(0, now);
  gain.gain.linearRampToValueAtTime(0.18, now + 0.005);
  gain.gain.exponentialRampToValueAtTime(0.001, now + duration);
  osc.connect(gain).connect(dest);
  osc.start(now);
  osc.stop(now + duration + 0.02);
}

function playUrge(ctx: AudioContext, dest: GainNode, now: number): void {
  const duration = 0.1;
  const osc = ctx.createOscillator();
  osc.type = 'triangle';
  osc.frequency.setValueAtTime(300, now);
  osc.frequency.exponentialRampToValueAtTime(700, now + duration);
  const gain = ctx.createGain();
  gain.gain.setValueAtTime(0, now);
  gain.gain.linearRampToValueAtTime(0.25, now + 0.01);
  gain.gain.linearRampToValueAtTime(0, now + duration);
  osc.connect(gain).connect(dest);
  osc.start(now);
  osc.stop(now + duration + 0.02);
}

function playWin(ctx: AudioContext, dest: GainNode, now: number): void {
  // Square-wave "level up": C5, E5, G5 quick + C6 flourish.
  const notes = [
    { freq: 523.25, dur: 0.09 },
    { freq: 659.25, dur: 0.09 },
    { freq: 783.99, dur: 0.09 },
    { freq: 1046.5, dur: 0.16 },
  ];
  let t = now;
  for (const { freq, dur } of notes) {
    const osc = ctx.createOscillator();
    osc.type = 'square';
    osc.frequency.value = freq;
    const gain = ctx.createGain();
    gain.gain.setValueAtTime(0, t);
    gain.gain.linearRampToValueAtTime(0.16, t + 0.008);
    gain.gain.exponentialRampToValueAtTime(0.001, t + dur);
    osc.connect(gain).connect(dest);
    osc.start(t);
    osc.stop(t + dur + 0.02);
    t += dur;
  }
}

function playLose(ctx: AudioContext, dest: GainNode, now: number): void {
  const duration = 0.7;
  const osc = ctx.createOscillator();
  osc.type = 'square';
  osc.frequency.setValueAtTime(640, now);
  osc.frequency.exponentialRampToValueAtTime(160, now + duration);
  const gain = ctx.createGain();
  gain.gain.setValueAtTime(0.18, now);
  gain.gain.linearRampToValueAtTime(0, now + duration);
  osc.connect(gain).connect(dest);
  osc.start(now);
  osc.stop(now + duration + 0.02);
}

function playDraw(ctx: AudioContext, dest: GainNode, now: number): void {
  const pulses: readonly number[] = [now, now + 0.18];
  for (const start of pulses) {
    const osc = ctx.createOscillator();
    osc.type = 'triangle';
    osc.frequency.value = 440;
    const gain = ctx.createGain();
    gain.gain.setValueAtTime(0, start);
    gain.gain.linearRampToValueAtTime(0.22, start + 0.02);
    gain.gain.exponentialRampToValueAtTime(0.001, start + 0.14);
    osc.connect(gain).connect(dest);
    osc.start(start);
    osc.stop(start + 0.18);
  }
}
