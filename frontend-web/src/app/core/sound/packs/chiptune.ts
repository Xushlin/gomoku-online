import { unhandledSoundEvent, type SoundEventName, type SoundPack } from '../sound.tokens';

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
 *   - move-place      : square click ~50 ms, ~150 Hz, fast attack/decay
 *   - capture         : square descending 220 → 80 Hz over 90 ms — a bite taken
 *   - line-clear      : four-step ascending square blips
 *   - line-clear-quad : six-step run with a triangle note on top
 *   - level-up        : two ascending triangle notes
 *   - urge            : triangle sweep 300 → 700 Hz over 100 ms (8-bit alert)
 *   - game-win        : ascending square arpeggio C5/E5/G5 + flourish C6
 *                       ("level up" feel)
 *   - game-lose       : square descending 640 → 160 Hz over 700 ms
 *                       ("game over" feel)
 *   - card-deal       : five very short ascending square blips — a flurry
 *   - card-play       : two descending triangle blips — a slap, not a reward
 *   - game-draw       : two triangle 440 Hz pulses, neutral
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
      case 'capture':
        playCapture(ctx, masterGain, now);
        return;
      case 'line-clear':
        playRun(ctx, masterGain, now, [659.25, 830.61, 987.77, 1244.51], 'square', 0.045, 0.16);
        return;
      case 'line-clear-quad':
        playRun(
          ctx,
          masterGain,
          now,
          [659.25, 830.61, 987.77, 1244.51, 1479.98, 1975.53],
          'square',
          0.05,
          0.17,
        );
        playRun(ctx, masterGain, now + 0.3, [2093.0], 'triangle', 0.16, 0.14);
        return;
      case 'level-up':
        playRun(ctx, masterGain, now, [783.99, 1046.5], 'triangle', 0.1, 0.2);
        return;
      case 'card-deal':
        // 五个很短的方波,越发越高 —— 与 line-clear 的四个、quad 的六个都不同长。
        playRun(ctx, masterGain, now, [523.25, 587.33, 659.25, 698.46, 783.99], 'square', 0.032, 0.13);
        return;
      case 'card-play':
        // 两个下行的三角波 —— 「啪」的一下,而不是升调的奖励音。
        playRun(ctx, masterGain, now, [880, 587.33], 'triangle', 0.055, 0.17);
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
      default:
        return unhandledSoundEvent(event);
    }
  },
};

/**
 * Fast square descent. A capture is the one thing in 象棋 a player must not
 * mistake for a quiet move, so this shares no code with `playMovePlace`.
 */
function playCapture(ctx: AudioContext, dest: GainNode, now: number): void {
  const duration = 0.09;
  const osc = ctx.createOscillator();
  osc.type = 'square';
  osc.frequency.setValueAtTime(220, now);
  osc.frequency.exponentialRampToValueAtTime(80, now + duration);
  const gain = ctx.createGain();
  gain.gain.setValueAtTime(0, now);
  gain.gain.linearRampToValueAtTime(0.17, now + 0.006);
  gain.gain.exponentialRampToValueAtTime(0.001, now + duration);
  osc.connect(gain).connect(dest);
  osc.start(now);
  osc.stop(now + duration + 0.02);
}

/** Stepped blip run — the 8-bit shape for "something good just happened". */
function playRun(
  ctx: AudioContext,
  dest: GainNode,
  now: number,
  freqs: readonly number[],
  type: OscillatorType,
  noteDur: number,
  peak: number,
): void {
  freqs.forEach((freq, i) => {
    const start = now + i * noteDur;
    const osc = ctx.createOscillator();
    osc.type = type;
    osc.frequency.value = freq;
    const gain = ctx.createGain();
    gain.gain.setValueAtTime(0, start);
    gain.gain.linearRampToValueAtTime(peak, start + 0.006);
    gain.gain.exponentialRampToValueAtTime(0.001, start + noteDur);
    osc.connect(gain).connect(dest);
    osc.start(start);
    osc.stop(start + noteDur + 0.02);
  });
}

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
