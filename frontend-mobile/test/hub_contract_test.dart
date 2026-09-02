// Every hub method this client listens for is one the server actually sends.
//
// **This exists because the client listened for a name that does not exist.** It read
// `RoomStateChanged`; the server sends `RoomState`. SignalR silently ignores a
// subscription nobody invokes, so the whole inbound half of the realtime connection was
// dead from the first day — and every test stayed green, because they all asserted the
// **server's** state over REST while the board on screen came from the one-shot REST
// snapshot taken when the room opened.
//
// It was found by opening the app on a real device and watching a joined game keep
// saying 等待中.
//
// The valid names are **derived from the server's own source**, never typed here: a
// hand-written list of hub methods beside the client is the same defect one layer up,
// and it would drift the first time the server renamed one.
import 'dart:io';

import 'package:flutter_test/flutter_test.dart';

/// `SendAsync("X"` in the notifier — i.e. what the server can push to a client.
Set<String> serverSendsFrom(File source) {
  final pattern = RegExp(r'SendAsync\(\s*"([A-Za-z]+)"');
  return pattern
      .allMatches(source.readAsStringSync())
      .map((m) => m.group(1)!)
      .toSet();
}

/// `connection.on('X'` in the mobile hub service.
Set<String> clientListensFrom(File source) {
  final pattern = RegExp(r"connection\.on\(\s*'([A-Za-z]+)'");
  return pattern
      .allMatches(source.readAsStringSync())
      .map((m) => m.group(1)!)
      .toSet();
}

void main() {
  final notifier = File('../backend/src/Gewu.Api/Hubs/SignalRRoomNotifier.cs');
  final hub = File('lib/data/services/match_hub_service.dart');

  test('both sources are readable, so the comparison below is not vacuous', () {
    // Without this, a moved file leaves both sets empty and "every name is valid" is
    // trivially true — which is exactly the shape of the bug this test exists for.
    expect(notifier.existsSync(), isTrue, reason: notifier.path);
    expect(hub.existsSync(), isTrue, reason: hub.path);
    expect(serverSendsFrom(notifier).length, greaterThanOrEqualTo(8));
    expect(clientListensFrom(hub), isNotEmpty);
  });

  test('every name the client listens for is one the server sends', () {
    final sends = serverSendsFrom(notifier);
    final listens = clientListensFrom(hub);

    expect(
      listens.difference(sends).toList()..sort(),
      equals(<String>[]),
      reason: 'the client listens for names the server never invokes; '
          'SignalR ignores those silently, so the symptom is a screen that never updates',
    );
  });

  test('and the one that carries board state is among them', () {
    // The specific subscription whose absence was invisible. Naming it here means a
    // future refactor that drops the listener fails loudly rather than quietly.
    expect(clientListensFrom(hub), contains('RoomState'));
    expect(serverSendsFrom(notifier), contains('RoomState'));
  });
}
