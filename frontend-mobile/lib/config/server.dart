/// Where the server is, per platform.
///
/// **`10.0.2.2` on Android is not a typo.** Inside an emulator `localhost` is the
/// emulator itself; the host machine's loopback is reachable at `10.0.2.2`. Writing
/// `localhost` there makes every request fail with connection refused, and all the
/// screen shows is a failed login — which reads as "the backend is not running".
///
/// This is the same "the host tells the client where the server is" problem the
/// desktop shell has, with a different answer.
library;

import 'dart:io' show Platform;

/// Overridden at build time: `--dart-define=GEWU_SERVER=https://your-server`.
const _override = String.fromEnvironment('GEWU_SERVER');

/// Host loopback as seen from inside an Android emulator.
const androidHostLoopback = 'http://10.0.2.2:5145';

/// Everywhere else (Windows/macOS/Linux desktop, and a real device on the same box).
const localLoopback = 'http://localhost:5145';

/// Pure so it can be tested without a platform: pass the flags explicitly.
String serverAddressFor({required bool isAndroid, String override = ''}) {
  final trimmed = override.trim();
  if (trimmed.isNotEmpty) {
    // No trailing slash: `'https://x/' + '/api/rooms'` is `https://x//api/rooms`,
    // which most servers answer right up until one does not.
    return trimmed.endsWith('/') ? trimmed.substring(0, trimmed.length - 1) : trimmed;
  }
  return isAndroid ? androidHostLoopback : localLoopback;
}

/// The address this build actually uses.
String get serverAddress =>
    serverAddressFor(isAndroid: Platform.isAndroid, override: _override);
