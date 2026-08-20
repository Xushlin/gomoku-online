import { unhandledSoundEvent, type SoundEventName, type SoundPack } from '../sound.tokens';

/**
 * Default sound pack. Every event is synthesised on the fly via Web Audio
 * API — no audio files shipped, no network requests, no licensing concerns.
 *
 * Event design:
 *   - move-place      : short lowpass-filtered noise burst (~60 ms) — wooden tap
 *   - capture         : harder, brighter noise burst plus a low sine thud —
 *                       "clack, and something has weight to it"
 *   - line-clear      : three ascending sine notes — rows going away
 *   - line-clear-quad : four ascending notes, higher and longer
 *   - level-up        : two notes an octave apart
 *   - card-deal       : five very short bright noise ticks, 55 ms apart, cutoff
 *                       rising — a riffle, not five taps
 *   - card-play       : one dull noise burst (low cutoff, longer tail) plus a
 *                       soft 150 Hz body — a card slapping felt, which is a
 *                       different object from a stone hitting wood
 *   - urge            : sine sweep 220 → 520 Hz over 120 ms — attention pop
 *   - game-win        : ascending C5–E5–G5 arpeggio (sine + AD envelope)
 *   - game-lose       : sine sweep 600 → 180 Hz over 600 ms with linear gain decay
 *   - game-draw       : two soft 400 Hz pulses
 *
 * Each event constructs a fresh, short-lived audio graph that auto-stops via
 * `node.stop(when)`. Garbage collection cleans up.
 */
export const woodPack: SoundPack = {
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
        playAscending(ctx, masterGain, now, [659.25, 830.61, 987.77], 0.07, 0.24);
        return;
      case 'line-clear-quad':
        playAscending(ctx, masterGain, now, [659.25, 830.61, 987.77, 1318.51], 0.08, 0.3);
        return;
      case 'level-up':
        playAscending(ctx, masterGain, now, [523.25, 1046.5], 0.11, 0.26);
        return;
      case 'card-deal':
        playCardDeal(ctx, masterGain, now);
        return;
      case 'card-play':
        playCardPlay(ctx, masterGain, now);
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

function playMovePlace(ctx: AudioContext, dest: GainNode, now: number): void {
  const duration = 0.06;
  const buffer = ctx.createBuffer(1, Math.max(1, Math.floor(ctx.sampleRate * duration)), ctx.sampleRate);
  const data = buffer.getChannelData(0);
  for (let i = 0; i < data.length; i++) {
    const env = Math.exp(-i / (ctx.sampleRate * 0.012));
    data[i] = (Math.random() * 2 - 1) * env;
  }
  const source = ctx.createBufferSource();
  source.buffer = buffer;

  const filter = ctx.createBiquadFilter();
  filter.type = 'lowpass';
  filter.frequency.value = 1800;
  filter.Q.value = 0.7;

  const gain = ctx.createGain();
  gain.gain.value = 0.35;

  source.connect(filter).connect(gain).connect(dest);
  source.start(now);
  source.stop(now + duration + 0.02);
}

/**
 * Two layers, and the second one is the point: a capture has to be audibly a
 * *different event* from a quiet move, not a louder one. The noise burst opens
 * brighter (3600 Hz vs 1800) and the low sine underneath gives it weight.
 */
function playCapture(ctx: AudioContext, dest: GainNode, now: number): void {
  const clackDuration = 0.05;
  const buffer = ctx.createBuffer(
    1,
    Math.max(1, Math.floor(ctx.sampleRate * clackDuration)),
    ctx.sampleRate,
  );
  const data = buffer.getChannelData(0);
  for (let i = 0; i < data.length; i++) {
    const env = Math.exp(-i / (ctx.sampleRate * 0.008));
    data[i] = (Math.random() * 2 - 1) * env;
  }
  const source = ctx.createBufferSource();
  source.buffer = buffer;

  const filter = ctx.createBiquadFilter();
  filter.type = 'lowpass';
  filter.frequency.value = 3600;
  filter.Q.value = 0.9;

  const clackGain = ctx.createGain();
  clackGain.gain.value = 0.32;

  source.connect(filter).connect(clackGain).connect(dest);
  source.start(now);
  source.stop(now + clackDuration + 0.02);

  const thudDuration = 0.14;
  const osc = ctx.createOscillator();
  osc.type = 'sine';
  osc.frequency.setValueAtTime(150, now);
  osc.frequency.exponentialRampToValueAtTime(90, now + thudDuration);

  const thudGain = ctx.createGain();
  thudGain.gain.setValueAtTime(0, now);
  thudGain.gain.linearRampToValueAtTime(0.3, now + 0.008);
  thudGain.gain.exponentialRampToValueAtTime(0.001, now + thudDuration);

  osc.connect(thudGain).connect(dest);
  osc.start(now);
  osc.stop(now + thudDuration + 0.02);
}

/** Ascending sine figure — one shape serving line clears and level-ups. */
function playAscending(
  ctx: AudioContext,
  dest: GainNode,
  now: number,
  freqs: readonly number[],
  noteDur: number,
  peak: number,
): void {
  freqs.forEach((freq, i) => {
    const start = now + i * noteDur;
    const osc = ctx.createOscillator();
    osc.type = 'sine';
    osc.frequency.value = freq;
    const gain = ctx.createGain();
    gain.gain.setValueAtTime(0, start);
    gain.gain.linearRampToValueAtTime(peak, start + 0.01);
    gain.gain.exponentialRampToValueAtTime(0.001, start + noteDur + 0.06);
    osc.connect(gain).connect(dest);
    osc.start(start);
    osc.stop(start + noteDur + 0.08);
  });
}

function playUrge(ctx: AudioContext, dest: GainNode, now: number): void {
  const duration = 0.12;
  const osc = ctx.createOscillator();
  osc.type = 'sine';
  osc.frequency.setValueAtTime(220, now);
  osc.frequency.exponentialRampToValueAtTime(520, now + duration);

  const gain = ctx.createGain();
  gain.gain.setValueAtTime(0, now);
  gain.gain.linearRampToValueAtTime(0.3, now + 0.01);
  gain.gain.linearRampToValueAtTime(0, now + duration);

  osc.connect(gain).connect(dest);
  osc.start(now);
  osc.stop(now + duration + 0.02);
}

function playWin(ctx: AudioContext, dest: GainNode, now: number): void {
  // Major arpeggio C5 (523.25), E5 (659.25), G5 (783.99) — universally
  // reads as "good news". Each note 120 ms with attack/release tail.
  const notes = [523.25, 659.25, 783.99];
  const noteDur = 0.12;
  notes.forEach((freq, i) => {
    const start = now + i * noteDur;
    const osc = ctx.createOscillator();
    osc.type = 'sine';
    osc.frequency.value = freq;
    const gain = ctx.createGain();
    gain.gain.setValueAtTime(0, start);
    gain.gain.linearRampToValueAtTime(0.28, start + 0.015);
    gain.gain.exponentialRampToValueAtTime(0.001, start + noteDur + 0.18);
    osc.connect(gain).connect(dest);
    osc.start(start);
    osc.stop(start + noteDur + 0.22);
  });
}

function playLose(ctx: AudioContext, dest: GainNode, now: number): void {
  const duration = 0.6;
  const osc = ctx.createOscillator();
  osc.type = 'sine';
  osc.frequency.setValueAtTime(600, now);
  osc.frequency.exponentialRampToValueAtTime(180, now + duration);

  const gain = ctx.createGain();
  gain.gain.setValueAtTime(0.3, now);
  gain.gain.linearRampToValueAtTime(0, now + duration);

  osc.connect(gain).connect(dest);
  osc.start(now);
  osc.stop(now + duration + 0.02);
}

function playDraw(ctx: AudioContext, dest: GainNode, now: number): void {
  // Two soft 400 Hz pulses, neutral.
  const pulses: readonly number[] = [now, now + 0.18];
  for (const start of pulses) {
    const osc = ctx.createOscillator();
    osc.type = 'sine';
    osc.frequency.value = 400;
    const gain = ctx.createGain();
    gain.gain.setValueAtTime(0, start);
    gain.gain.linearRampToValueAtTime(0.22, start + 0.02);
    gain.gain.exponentialRampToValueAtTime(0.001, start + 0.14);
    osc.connect(gain).connect(dest);
    osc.start(start);
    osc.stop(start + 0.18);
  }
}

/**
 * 一副牌发下来 —— 五下很短的亮噪声,越发越亮。
 *
 * 刻意**不是**五次 `move-place`:那是「五个人各落了一子」,而发牌是一个动作。
 * 所以每一下更短(18 ms 对 60 ms)、更亮,而且滤波器的截止频率一路上行。
 */
function playCardDeal(ctx: AudioContext, dest: GainNode, now: number): void {
  const ticks = 5;
  for (let i = 0; i < ticks; i++) {
    const start = now + i * 0.055;
    const duration = 0.018;
    const buffer = ctx.createBuffer(
      1,
      Math.max(1, Math.floor(ctx.sampleRate * duration)),
      ctx.sampleRate,
    );
    const data = buffer.getChannelData(0);
    for (let s = 0; s < data.length; s++) {
      const env = Math.exp(-s / (ctx.sampleRate * 0.004));
      data[s] = (Math.random() * 2 - 1) * env;
    }
    const source = ctx.createBufferSource();
    source.buffer = buffer;

    const filter = ctx.createBiquadFilter();
    filter.type = 'highpass';
    filter.frequency.value = 1400 + i * 260;

    const gain = ctx.createGain();
    gain.gain.setValueAtTime(0.16, start);
    gain.gain.exponentialRampToValueAtTime(0.001, start + duration);

    source.connect(filter).connect(gain).connect(dest);
    source.start(start);
    source.stop(start + duration + 0.01);
  }
}

/**
 * 一手牌拍在桌上 —— 闷一点的噪声加一点低频的身体。
 *
 * 与 `capture` 共享「噪声 + 正弦」的形状而不共享参数:那一下是亮而硬的(木头对木头),
 * 这一下是钝的(纸对呢面)。两者在 `pack-contract.spec` 的指纹下必须不同。
 */
function playCardPlay(ctx: AudioContext, dest: GainNode, now: number): void {
  const duration = 0.085;
  const buffer = ctx.createBuffer(1, Math.max(1, Math.floor(ctx.sampleRate * duration)), ctx.sampleRate);
  const data = buffer.getChannelData(0);
  for (let i = 0; i < data.length; i++) {
    const env = Math.exp(-i / (ctx.sampleRate * 0.02));
    data[i] = (Math.random() * 2 - 1) * env;
  }
  const source = ctx.createBufferSource();
  source.buffer = buffer;

  const filter = ctx.createBiquadFilter();
  filter.type = 'lowpass';
  filter.frequency.value = 900;

  const noiseGain = ctx.createGain();
  noiseGain.gain.setValueAtTime(0.22, now);
  noiseGain.gain.exponentialRampToValueAtTime(0.001, now + duration);

  source.connect(filter).connect(noiseGain).connect(dest);
  source.start(now);
  source.stop(now + duration + 0.01);

  const body = ctx.createOscillator();
  body.type = 'sine';
  body.frequency.value = 150;

  const bodyGain = ctx.createGain();
  bodyGain.gain.setValueAtTime(0.14, now);
  bodyGain.gain.exponentialRampToValueAtTime(0.001, now + 0.07);

  body.connect(bodyGain).connect(dest);
  body.start(now);
  body.stop(now + 0.08);
}
