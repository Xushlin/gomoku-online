// Can `signalr_netcore` talk to this platform's hub at all — **in both directions**?
//
// This is step 0 for any realtime client here, and it runs BEFORE any UI exists. The
// package is a community one (1.4.4, last published 2025-09-05) and our hub wants a
// query-string JWT, the JSON protocol, and named methods. If it cannot do this, the
// plan needs a different transport — a bigger decision than the UI — so finding out
// after the screens are built means those screens were wasted.
//
// **The first version of this probe only proved half of that, and the half it skipped
// is the one that broke.** It sent `JoinRoom` and `MakeMove` and then checked over
// **REST** that the server had the move. Every byte it verified went *to* the server;
// nothing came back over the hub. The mobile client meanwhile subscribed to
// `RoomStateChanged` while the server sends `RoomState` — SignalR ignores a
// subscription to a name nobody invokes, silently, so the inbound half was dead from
// day one, this probe was green, and a joined game kept saying 等待中 on a real phone.
//
// So the shape here is deliberate:
//
//   outbound — `JoinRoom` and `MakeMove` are accepted
//   inbound  — a server-pushed `RoomState` **arrives**, and it contains the move
//   REST     — still checked, but it proves the SERVER received, not that we can hear
//
// It needs a live backend, so it SKIPS unless GEWU_PROBE_SERVER is set:
//
//   flutter test test/hub_probe_test.dart \
//     --dart-define=GEWU_PROBE_SERVER=http://127.0.0.1:5199
//
// Skipping rather than failing keeps CI green without pretending the probe ran — the
// skip message says so out loud.
import 'dart:async';
import 'dart:convert';
import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:signalr_netcore/hub_connection_builder.dart';

String get _server => const String.fromEnvironment('GEWU_PROBE_SERVER', defaultValue: '');

/// The hub method that carries room state. Kept as a constant so the positive control
/// for this probe is a one-line edit: point it at a name the server does not send and
/// the inbound assertion below must go red.
///
/// `test/hub_contract_test.dart` is what keeps the *client* honest about this name; it
/// derives the legal set from the server's own source.
const roomStateMethod = 'RoomState';

Future<Map<String, dynamic>> _post(String path, Object body, {String? token}) async {
  final res = await http.post(
    Uri.parse('$_server$path'),
    headers: {
      'content-type': 'application/json',
      if (token != null) 'authorization': 'Bearer $token',
    },
    body: jsonEncode(body),
  );
  if (res.statusCode >= 400) {
    throw StateError('$path -> ${res.statusCode} ${res.body}');
  }
  return res.body.isEmpty ? <String, dynamic>{} : jsonDecode(res.body) as Map<String, dynamic>;
}

void main() {
  test(
    'signalr_netcore can send to our hub AND hear it push back',
    () async {
      final stamp = DateTime.now().millisecondsSinceEpoch.toString().substring(6);

      // Two players: the hub refuses a move from a seat that is not yours, so a
      // one-player room would never reach the interesting call.
      final black = await _post('/api/auth/register', {
        'email': 'dartb$stamp@example.com',
        'username': 'dartb$stamp',
        'password': 'Probe-pass-1234',
      });
      final white = await _post('/api/auth/register', {
        'email': 'dartw$stamp@example.com',
        'username': 'dartw$stamp',
        'password': 'Probe-pass-1234',
      });

      final room = await _post(
        '/api/rooms',
        {'name': 'dart-probe-$stamp', 'gameKey': 'gomoku'},
        token: black['accessToken'] as String,
      );
      final roomId = room['id'] as String;
      await _post('/api/rooms/$roomId/join', {}, token: white['accessToken'] as String);

      // The hub takes the token on the query string, which is the bit most likely to be
      // unsupported by a third-party client.
      final token = black['accessToken'] as String;
      final connection = HubConnectionBuilder()
          .withUrl('$_server/hubs/match?access_token=${Uri.encodeComponent(token)}')
          .build();

      final states = <String>[];
      connection.onclose(({error}) => states.add('closed: $error'));

      // --- the inbound half ---------------------------------------------------
      // Subscribed BEFORE `start()`, because a push can arrive the moment we join.
      final pushed = Completer<Map<String, dynamic>>();
      final pushCount = <int>[0];
      connection.on(roomStateMethod, (args) {
        if (args == null || args.isEmpty || args.first is! Map) return;
        pushCount[0]++;
        if (!pushed.isCompleted) {
          pushed.complete(Map<String, dynamic>.from(args.first! as Map));
        }
      });

      await connection.start();
      states.add('started');

      await connection.invoke('JoinRoom', args: [roomId]);
      states.add('joined');

      // Black moves first. A real move, judged by the real rules.
      await connection.invoke('MakeMove', args: [roomId, 7, 7]);
      states.add('moved');

      // **The assertion the old probe did not have.** A bounded wait, because a probe
      // that hangs forever and one that passes look equally quiet from the outside.
      final Map<String, dynamic> state;
      try {
        state = await pushed.future.timeout(const Duration(seconds: 10));
      } on TimeoutException {
        await connection.stop();
        fail(
          'no `$roomStateMethod` push arrived within 10s. The hub accepted everything we '
          'sent, so the OUTBOUND half works — this is the inbound half, and it is the '
          'one that was silently dead for the whole first release of the mobile client.',
        );
      }
      states.add('heard');

      await connection.stop();

      expect(states, containsAll(<String>['started', 'joined', 'moved', 'heard']));

      // The push must carry the move — "a message arrived" is not "the right message
      // arrived", and a hub that pushed an empty snapshot would pass the line above.
      final pushedMoves =
          ((state['game'] as Map<String, dynamic>)['moves'] as List<dynamic>)
              .cast<Map<String, dynamic>>();
      expect(pushedMoves, hasLength(1), reason: 'the pushed snapshot must contain the move');
      expect(pushedMoves.single['row'], 7);
      expect(pushedMoves.single['col'], 7);
      expect(state['status'], 'Playing');

      // **REST, and what it is actually worth.** This was labelled a positive control in
      // the first version, and it is not one for the inbound half: it proves the
      // **server received** the move, not that **we can hear** the server. Both facts
      // are worth having; conflating them is the exact shape of the gap this probe used
      // to have.
      final res = await http.get(
        Uri.parse('$_server/api/rooms/$roomId'),
        headers: {'authorization': 'Bearer $token'},
      );
      final serverState = jsonDecode(res.body) as Map<String, dynamic>;
      final serverMoves = (serverState['game'] as Map<String, dynamic>)['moves'] as List<dynamic>;
      expect(serverMoves, hasLength(1), reason: 'the move should be recorded server-side');

      stdout.writeln(
        'PROBE OK: started -> joined -> moved -> heard. '
        '${pushCount[0]} push(es) received; the first carried '
        '${pushedMoves.length} move at (${pushedMoves.single['row']},'
        '${pushedMoves.single['col']}), and the server has ${serverMoves.length}.',
      );
    },
    skip: _server.isEmpty
        ? 'set GEWU_PROBE_SERVER (e.g. --dart-define=GEWU_PROBE_SERVER=http://127.0.0.1:5199) '
              'to run this against a live backend; it is NOT running now'
        : null,
  );
}
