/// The sound pack, synthesised on the device.
///
/// **There is nothing to sync from `frontend-web`, and that is a fact about the web
/// client rather than a shortcut here.** Its three packs are built with WebAudio at
/// play time (`play(event, ctx: AudioContext, masterGain: GainNode)`); the repository
/// contains **no audio files at all**. So what gets ported is the *design* — which
/// frequency, how long, how loud — and those numbers are written down in
/// `frontend-web/src/app/core/sound/packs/minimal.ts`.
///
/// **And synthesising rather than bundling is what makes this testable.** A bundled
/// `.wav` can only be asserted to exist. A generated buffer can be asserted on its
/// length, its peak amplitude and its **dominant frequency** — which is how
/// `test/sound_test.dart` checks a click really is 660 Hz without anybody hearing it.
library;

import 'dart:math' as math;
import 'dart:typed_data';

import '../models/models.dart';

/// 22.05 kHz is plenty: the highest note here is E6 (1318 Hz), so Nyquist has an order
/// of magnitude of headroom, and the buffers stay small enough to build on every play.
const sampleRate = 22050;

/// One tone in an event's little score.
class _Tone {
  const _Tone(this.startSeconds, this.freq, this.durationSeconds, this.peak, {this.click = true});

  final double startSeconds;
  final double freq;
  final double durationSeconds;

  /// 0..1, before the master level.
  final double peak;

  /// A click has a 5 ms attack; a note has 20 ms. The web pack draws the same
  /// distinction and it is what keeps a stone from sounding like a chime.
  final bool click;
}

/// Every event's score. **Ported number for number from the web `minimal` pack** —
/// this is a port, not a re-scoring: two clients that sound different are two products.
const Map<SoundEvent, List<_Tone>> _score = {
  SoundEvent.movePlace: [_Tone(0, 660, 0.07, 0.16)],
  SoundEvent.capture: [_Tone(0, 660, 0.05, 0.16), _Tone(0.06, 440, 0.06, 0.16)],
  SoundEvent.lineClear: [
    _Tone(0, 659.25, 0.10, 0.14, click: false), // E5
    _Tone(0.11, 987.77, 0.10, 0.14, click: false), // B5
  ],
  SoundEvent.lineClearQuad: [
    _Tone(0, 659.25, 0.10, 0.15, click: false), // E5
    _Tone(0.11, 987.77, 0.10, 0.15, click: false), // B5
    _Tone(0.22, 1318.51, 0.10, 0.15, click: false), // E6
  ],
  SoundEvent.levelUp: [_Tone(0, 1046.5, 0.14, 0.15, click: false)], // C6
  SoundEvent.cardDeal: [
    _Tone(0, 520, 0.04, 0.10),
    _Tone(0.06, 520, 0.04, 0.10),
    _Tone(0.12, 520, 0.04, 0.10),
  ],
  SoundEvent.cardPlay: [_Tone(0, 330, 0.06, 0.13)],
  SoundEvent.urge: [_Tone(0, 880, 0.05, 0.15), _Tone(0.08, 880, 0.05, 0.15)],
  SoundEvent.gameWin: [
    _Tone(0, 523.25, 0.14, 0.14, click: false), // C5
    _Tone(0.15, 783.99, 0.14, 0.14, click: false), // G5
  ],
  SoundEvent.gameLose: [
    _Tone(0, 392.0, 0.14, 0.15, click: false), // G4
    _Tone(0.15, 261.63, 0.14, 0.15, click: false), // C4
  ],
  SoundEvent.gameDraw: [_Tone(0, 440, 0.12, 0.11, click: false)],
};

/// The PCM for one event: 16-bit signed, mono, [sampleRate] Hz.
///
/// Exposed separately from [wavFor] because **the samples are the thing worth
/// asserting** — a WAV header adds 44 bytes and no information.
Int16List samplesFor(SoundEvent event) {
  final tones = _score[event];
  // Every event has a score: `_score` is a `const` map over the enum and
  // `test/sound_test.dart` walks the enum to prove none is missing. Returning silence
  // here rather than throwing keeps the "sound never breaks a game" rule true even if
  // that walk is ever wrong.
  if (tones == null || tones.isEmpty) return Int16List(0);

  final endSeconds = tones
      .map((t) => t.startSeconds + t.durationSeconds)
      .reduce((a, b) => a > b ? a : b);
  final total = (endSeconds * sampleRate).ceil();
  final buffer = Float64List(total);

  for (final tone in tones) {
    final start = (tone.startSeconds * sampleRate).round();
    final length = (tone.durationSeconds * sampleRate).round();
    final attack = ((tone.click ? 0.005 : 0.02) * sampleRate).round();

    for (var i = 0; i < length; i++) {
      final index = start + i;
      if (index >= total) break;

      // Linear attack, then an exponential decay to 0.001 — the same envelope the web
      // pack builds out of `linearRampToValueAtTime` + `exponentialRampToValueAtTime`.
      final double envelope;
      if (i < attack) {
        envelope = tone.peak * (i / attack);
      } else {
        final t = (i - attack) / math.max(1, length - attack);
        envelope = tone.peak * math.pow(0.001 / tone.peak, t).toDouble();
      }
      buffer[index] += envelope * math.sin(2 * math.pi * tone.freq * i / sampleRate);
    }
  }

  final out = Int16List(total);
  for (var i = 0; i < total; i++) {
    // Clamp rather than wrap: overlapping tones can exceed 1.0, and a wrapped sample is
    // a loud crack rather than a quiet distortion.
    final v = buffer[i].clamp(-1.0, 1.0);
    out[i] = (v * 32767).round();
  }
  return out;
}

/// A complete mono 16-bit PCM WAV file for [event].
Uint8List wavFor(SoundEvent event) {
  final samples = samplesFor(event);
  final dataBytes = samples.length * 2;
  final out = ByteData(44 + dataBytes);

  void ascii(int offset, String tag) {
    for (var i = 0; i < tag.length; i++) {
      out.setUint8(offset + i, tag.codeUnitAt(i));
    }
  }

  ascii(0, 'RIFF');
  out.setUint32(4, 36 + dataBytes, Endian.little);
  ascii(8, 'WAVE');
  ascii(12, 'fmt ');
  out.setUint32(16, 16, Endian.little); // PCM chunk size
  out.setUint16(20, 1, Endian.little); // format = PCM
  out.setUint16(22, 1, Endian.little); // channels = mono
  out.setUint32(24, sampleRate, Endian.little);
  out.setUint32(28, sampleRate * 2, Endian.little); // byte rate
  out.setUint16(32, 2, Endian.little); // block align
  out.setUint16(34, 16, Endian.little); // bits per sample
  ascii(36, 'data');
  out.setUint32(40, dataBytes, Endian.little);
  for (var i = 0; i < samples.length; i++) {
    out.setInt16(44 + i * 2, samples[i], Endian.little);
  }
  return out.buffer.asUint8List();
}
