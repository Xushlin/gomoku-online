/// The audio device.
///
/// An interface rather than the plugin directly, for the same reason `PreferencesStore`
/// is one: a unit test has no platform channel, and a fake here is the only way to
/// assert **that** something was played without playing it.
library;

import 'dart:typed_data';

import 'package:audioplayers/audioplayers.dart';

abstract class SoundPlayer {
  /// Plays one short clip. **Fire and forget** — see `SoundRepository` for why the
  /// caller must never await this.
  Future<void> play(Uint8List wav);

  Future<void> dispose();
}

class AudioPlayersSoundPlayer implements SoundPlayer {
  /// One player, reused. Constructing one per sound leaks native handles, and the
  /// clips here are short enough that cutting the previous one off is the right
  /// behaviour anyway: two stones placed 50 ms apart should click once, not overlap.
  final _player = AudioPlayer();

  @override
  Future<void> play(Uint8List wav) async {
    // `BytesSource` keeps the clip in memory — nothing is written to disk, so there is
    // no temp file to clean up and no path to get wrong.
    await _player.play(BytesSource(wav, mimeType: 'audio/wav'));
  }

  @override
  Future<void> dispose() => _player.dispose();
}

/// Records what it was asked to play. For tests, and for a build with no audio.
class RecordingSoundPlayer implements SoundPlayer {
  final played = <Uint8List>[];
  Object? failWith;

  @override
  Future<void> play(Uint8List wav) async {
    played.add(wav);
    if (failWith != null) throw failWith!;
  }

  @override
  Future<void> dispose() async {}
}
