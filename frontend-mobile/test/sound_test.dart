// Sound, checked without listening to it.
//
// **This is the point of synthesising rather than bundling.** A packaged `.wav` can
// only be asserted to exist; a generated buffer is a list of numbers, so it can be
// asserted on its length, its peak, and — the one that actually matters — its
// **dominant frequency**. `move-place` is supposed to be a 660 Hz click, and the test
// below measures 660 Hz out of the samples.
//
// There is nothing to sync from `frontend-web` here, and that is a fact about the web
// client: its three packs are built with WebAudio at play time and the repository
// holds no audio files at all. What is ported is the *design* — the frequencies,
// durations and gains written down in `packs/minimal.ts` — and the **event set**,
// which is derived from that source rather than copied.
import 'dart:io';
import 'dart:math' as math;
import 'dart:typed_data';

import 'package:flutter_test/flutter_test.dart';

import 'package:gewu_mobile/data/models/models.dart';
import 'package:gewu_mobile/data/repositories/settings_repository.dart';
import 'package:gewu_mobile/data/repositories/sound_repository.dart';
import 'package:gewu_mobile/data/services/preferences_store.dart';
import 'package:gewu_mobile/data/services/sound_player.dart';
import 'package:gewu_mobile/data/services/sound_synth.dart';

/// Goertzel: how much energy sits at [freq] in [samples].
///
/// Cheaper and clearer than a full FFT for the one question being asked — "is this
/// buffer mostly this note?" — and short enough to read, which matters more here than
/// speed.
double _energyAt(Int16List samples, double freq) {
  final w = 2 * math.pi * freq / sampleRate;
  final coeff = 2 * math.cos(w);
  var s1 = 0.0, s2 = 0.0;
  for (final sample in samples) {
    final s0 = sample / 32768.0 + coeff * s1 - s2;
    s2 = s1;
    s1 = s0;
  }
  return s1 * s1 + s2 * s2 - coeff * s1 * s2;
}

/// The strongest frequency in [samples], searched over the range these packs use.
double _dominantFrequency(Int16List samples) {
  var best = 0.0, bestEnergy = -1.0;
  for (var f = 200.0; f <= 1500.0; f += 1.0) {
    final e = _energyAt(samples, f);
    if (e > bestEnergy) {
      bestEnergy = e;
      best = f;
    }
  }
  return best;
}

/// The web client's event names, read out of its source.
Set<String> webSoundEvents() {
  final source = File(
    '../frontend-web/src/app/core/sound/sound.tokens.ts',
  ).readAsStringSync();
  final body = source.substring(
    source.indexOf('export const SOUND_EVENTS = ['),
    source.indexOf('] as const;'),
  );
  return RegExp("^\\s*'([a-z-]+)',", multiLine: true)
      .allMatches(body)
      .map((m) => m.group(1)!)
      .toSet();
}

void main() {
  group('the event set is derived from the web client, not copied', () {
    test('the two sets are equal', () {
      // **Equal, not "contains".** A subset assertion goes green when this client
      // silently drops an event, which is the exact failure a hand-written list
      // produces — and the symptom is one sound that never plays.
      //
      // Positive control: delete an enum value and this goes red.
      final web = webSoundEvents();
      final mine = SoundEvent.values.map((e) => e.wire).toSet();

      expect(web, isNotEmpty, reason: 'a walk over zero names asserts nothing');
      expect(mine, equals(web));
    });

    test('and the walk really read the file', () {
      // Non-vacuity for the parse: if the regex or the markers ever stop matching, the
      // set above becomes empty and equality would only hold against an empty enum.
      expect(webSoundEvents(), contains('move-place'));
      expect(webSoundEvents().length, greaterThan(5));
    });
  });

  group('every event makes a sound', () {
    test('none of them is empty or silent', () {
      for (final event in SoundEvent.values) {
        final samples = samplesFor(event);
        expect(samples, isNotEmpty, reason: '${event.wire} produced no samples');
        expect(
          samples.any((s) => s != 0),
          isTrue,
          reason: '${event.wire} produced silence, which is worse than no sound at all: '
              'it looks like the feature works',
        );
      }
    });

    test('and none of them clips', () {
      // Overlapping tones can sum past full scale. Clamping keeps that as quiet
      // distortion; wrapping would make it a loud crack. Assert we are not sitting on
      // the rail for any meaningful stretch.
      for (final event in SoundEvent.values) {
        final samples = samplesFor(event);
        final railed = samples.where((s) => s.abs() >= 32767).length;
        expect(
          railed,
          lessThan(samples.length ~/ 100),
          reason: '${event.wire} spends too long at full scale',
        );
      }
    });
  });

  group('the notes are the notes', () {
    test('a stone click is 660 Hz', () {
      // **The assertion this whole design exists for.** Nobody here can hear anything;
      // the buffer is numbers, so the pitch is measurable.
      //
      // Positive control: make the synth ignore the frequency parameter and this goes
      // red — measured.
      final f = _dominantFrequency(samplesFor(SoundEvent.movePlace));
      expect(f, closeTo(660, 15));
    });

    test('a win opens on C5 and a loss opens on G4', () {
      // Different pitches, and specifically these two: win rises, loss falls. Testing
      // only "they differ" would pass an implementation that swapped them.
      const attack = 0.12; // just the first note of each two-note figure
      Int16List head(SoundEvent e) {
        final s = samplesFor(e);
        return Int16List.sublistView(s, 0, math.min(s.length, (attack * sampleRate).round()));
      }

      expect(_dominantFrequency(head(SoundEvent.gameWin)), closeTo(523.25, 15));
      expect(_dominantFrequency(head(SoundEvent.gameLose)), closeTo(392.0, 15));
    });

    test('urge is 880 Hz and is two clicks, not one', () {
      final samples = samplesFor(SoundEvent.urge);
      expect(_dominantFrequency(samples), closeTo(880, 20));

      // Two bursts 80 ms apart means a quiet gap in between — assert the shape, or
      // "two clicks" is just a comment.
      final gapStart = (0.055 * sampleRate).round();
      final gapEnd = (0.075 * sampleRate).round();
      final gapPeak = Int16List.sublistView(samples, gapStart, gapEnd)
          .map((s) => s.abs())
          .reduce(math.max);
      final overallPeak = samples.map((s) => s.abs()).reduce(math.max);
      expect(gapPeak, lessThan(overallPeak ~/ 4), reason: 'there must be a gap');
    });

    test('a capture does not sound like a plain move', () {
      // 象棋's two move sounds have to be distinguishable, or the extra event is
      // decoration.
      expect(
        samplesFor(SoundEvent.capture).length,
        isNot(samplesFor(SoundEvent.movePlace).length),
      );
    });
  });

  group('the WAV wrapper', () {
    test('is a real header the platform can read', () {
      final wav = wavFor(SoundEvent.movePlace);
      String tag(int at) => String.fromCharCodes(wav.sublist(at, at + 4));

      expect(tag(0), 'RIFF');
      expect(tag(8), 'WAVE');
      expect(tag(12), 'fmt ');
      expect(tag(36), 'data');

      final view = ByteData.sublistView(wav);
      expect(view.getUint16(20, Endian.little), 1, reason: 'PCM');
      expect(view.getUint16(22, Endian.little), 1, reason: 'mono');
      expect(view.getUint32(24, Endian.little), sampleRate);
      expect(view.getUint16(34, Endian.little), 16, reason: '16-bit');
      // The declared data size must match what is actually there, or a player reads
      // past the end or stops early.
      expect(view.getUint32(40, Endian.little), wav.length - 44);
      expect(view.getUint32(4, Endian.little), wav.length - 8);
    });
  });

  group('muting does not reach the device', () {
    ({SoundRepository sound, RecordingSoundPlayer player, SettingsRepository settings})
        build() {
      final player = RecordingSoundPlayer();
      final settings = SettingsRepository(MemoryPreferencesStore());
      return (
        sound: SoundRepository(player: player, settings: settings),
        player: player,
        settings: settings,
      );
    }

    test('on plays', () async {
      // The precondition for the test below: "nothing was played" is green when the
      // whole path is broken.
      final o = build();
      expect(o.settings.current.value.soundOn, isTrue, reason: 'the default is on');
      o.sound.play(SoundEvent.movePlace);
      expect(o.player.played, hasLength(1));
    });

    test('off does not even ask the device', () async {
      // **Not "plays at zero volume".** That looks the same on a quiet phone and still
      // takes audio focus, which pauses whatever the person was listening to.
      //
      // Positive control: change the guard to pass a zero volume through and this goes
      // red.
      final o = build();
      await o.settings.setSoundOn(false);
      o.sound.play(SoundEvent.movePlace);
      expect(o.player.played, isEmpty);
    });

    test('the choice survives a restart, both ways', () async {
      final store = MemoryPreferencesStore();
      await SettingsRepository(store).setSoundOn(false);
      expect(SettingsRepository(store).current.value.soundOn, isFalse);

      await SettingsRepository(store).setSoundOn(true);
      expect(SettingsRepository(store).current.value.soundOn, isTrue);
    });

    test('and it is a third independent axis', () async {
      // Same argument as theme vs. brightness: switching one must not reset the others.
      final settings = SettingsRepository(MemoryPreferencesStore());
      await settings.setSoundOn(false);
      await settings.setDark(false);

      final other = settings.current.value;
      expect(other.soundOn, isFalse, reason: 'brightness must not have unmuted us');

      await settings.setTheme('material');
      expect(settings.current.value.soundOn, isFalse, reason: 'nor must the theme');
      expect(settings.current.value.isDark, isFalse);
    });
  });

  group('a broken audio device cannot break a game', () {
    test('a throwing player does not propagate', () async {
      final player = RecordingSoundPlayer()..failWith = Exception('no audio device');
      final sound = SoundRepository(
        player: player,
        settings: SettingsRepository(MemoryPreferencesStore()),
      );

      // No try/catch here on purpose: if `play` propagated, this test would fail, and
      // that is exactly the failure a player would see as a broken game.
      sound.play(SoundEvent.movePlace);
      await Future<void>.delayed(Duration.zero);
      expect(player.played, hasLength(1), reason: 'it did try');
    });
  });

  group('the buffers are built once', () {
    test('the same event twice does not resynthesise', () {
      final sound = SoundRepository(
        player: RecordingSoundPlayer(),
        settings: SettingsRepository(MemoryPreferencesStore()),
      );
      sound.play(SoundEvent.movePlace);
      sound.play(SoundEvent.movePlace);
      final played = (sound.player as RecordingSoundPlayer).played;
      expect(played, hasLength(2));
      expect(identical(played[0], played[1]), isTrue, reason: 'the cache handed back one buffer');
    });
  });
}
