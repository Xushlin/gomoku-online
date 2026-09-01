// Can `signalr_netcore` talk to this platform's hub at all?
//
// This is step 0 of `add-mobile-shell`, and it runs BEFORE any UI exists. The
// package is a community one (1.4.4, last published 2025-09-05) and our hub wants
// a query-string JWT, the JSON protocol, and named methods. If it cannot do
// JoinRoom + MakeMove, the mobile plan needs a different transport — a bigger
// decision than the UI rewrite — so finding out after the screens are built means
// those screens were wasted.
//
// It needs a live backend, so it SKIPS unless GEWU_PROBE_SERVER is set:
//
//   GEWU_PROBE_SERVER=http://localhost:5199 flutter test test/hub_probe_test.dart
//
// Skipping rather than failing keeps CI green without pretending the probe ran —
// the skip message says so out loud.
import 'dart:convert';
import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:signalr_netcore/hub_connection_builder.dart';

String get _server => const String.fromEnvironment('GEWU_PROBE_SERVER', defaultValue: '');

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
    'signalr_netcore can join a room and make a move on our hub',
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

      // The hub takes the token on the query string, which is the bit most likely
      // to be unsupported by a third-party client.
      final token = black['accessToken'] as String;
      final connection = HubConnectionBuilder()
          .withUrl('$_server/hubs/match?access_token=${Uri.encodeComponent(token)}')
          .build();

      final states = <String>[];
      connection.onclose(({error}) => states.add('closed: $error'));

      await connection.start();
      states.add('started');

      await connection.invoke('JoinRoom', args: [roomId]);
      states.add('joined');

      // Black moves first. A real move, judged by the real rules.
      await connection.invoke('MakeMove', args: [roomId, 7, 7]);
      states.add('moved');

      await connection.stop();

      expect(states, containsAll(<String>['started', 'joined', 'moved']));

      // Positive control: the move must actually be on the board now, not merely
      // "the call did not throw". A hub that silently swallowed it would pass the
      // line above.
      final res = await http.get(
        Uri.parse('$_server/api/rooms/$roomId'),
        headers: {'authorization': 'Bearer $token'},
      );
      final state = jsonDecode(res.body) as Map<String, dynamic>;
      final moves = (state['game'] as Map<String, dynamic>)['moves'] as List<dynamic>;
      expect(moves, hasLength(1), reason: 'the move should be recorded server-side');

      stdout.writeln('PROBE OK: started -> joined -> moved, and the server has 1 move');
    },
    skip: _server.isEmpty
        ? 'set GEWU_PROBE_SERVER (e.g. --dart-define=GEWU_PROBE_SERVER=http://localhost:5199) '
              'to run this against a live backend; it is NOT running now'
        : null,
  );
}
