/// Whether to make a noise, and which one.
///
/// **The mute check is here, above the player, and that is the requirement rather than
/// a tidiness preference.** Muting by playing at zero volume looks identical on a quiet
/// device and still takes audio focus — on Android that pauses whatever the person was
/// listening to. Silence has to mean "we did not ask the device for anything".
library;

import 'dart:typed_data';

import '../models/models.dart';
import '../services/sound_player.dart';
import '../services/sound_synth.dart';
import 'settings_repository.dart';

class SoundRepository {
  SoundRepository({required this.player, required this.settings});

  final SoundPlayer player;
  final SettingsRepository settings;

  /// Synthesised once per event and kept. The buffers are a few kilobytes each and
  /// there are eleven of them; rebuilding one on every stone would be wasted work on
  /// the frame that is already doing the most.
  final _cache = <SoundEvent, Uint8List>{};

  bool get enabled => settings.current.value.soundOn;

  /// Plays [event], unless sound is off.
  ///
  /// **Returns nothing and never throws.** A game must not be able to stall or fail
  /// because an audio device is busy, missing, or unsupported — so callers do not
  /// await this, and a failure inside it is swallowed deliberately rather than
  /// surfaced as an error the player would have to read.
  void play(SoundEvent event) {
    if (!enabled) return;
    final wav = _cache.putIfAbsent(event, () => wavFor(event));
    // Unawaited on purpose; `catchError` keeps a rejected future from becoming an
    // unhandled async error, which in debug is a red screen over a working game.
    player.play(wav).catchError((Object _) {});
  }

  Future<void> dispose() => player.dispose();
}
