// Step 0 for the room's other half: chat, urge, spectate, resign.
//
//   flutter test test/room_social_probe_test.dart \
//     --dart-define=GEWU_PROBE_SERVER=http://127.0.0.1:5199
//
// Same discipline as `test/hub_probe_test.dart`, and for the same reason: the last time
// this client built screens on an unmeasured transport, the entire inbound half was
// dead from day one and every test was green. So before any of these four get a button:
//
//   * `SendChat` takes a **C# enum** as its third argument. Both the REST pipeline and
//     the hub's `PayloadSerializerOptions` register `JsonStringEnumConverter`, so the
//     wire form should be a string — but **SignalR rejects a badly-typed argument in
//     the binding layer, before any filter and below the configured log level**. It is
//     invisible from both ends. This probe measures which forms bind, rather than
//     reading the DI registration and hoping.
//   * `UrgeReceived` is documented as going **only to the urged player**. "Only" is a
//     negative claim, and a negative claim is green when nothing arrives at all — so it
//     is asserted alongside a positive one on the same connection.
//   * The spectator chat channel is the same shape: a player must NOT see it. The
//     precondition is that the same player CAN see a room-channel message, or the
//     assertion is measuring a dead subscription.
//
// It skips unless GEWU_PROBE_SERVER is set — skipping keeps CI green without
// pretending the probe ran.
import 'dart:convert';
import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:signalr_netcore/hub_connection.dart';
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

Future<Map<String, dynamic>> _register(String who, String stamp) => _post('/api/auth/register', {
  'email': '$who$stamp@example.com',
  'username': '$who$stamp',
  'password': 'Probe-pass-1234',
});

/// One connection plus a recorder for every method name we care about.
class _Ear {
  _Ear(this.label, this.connection);

  final String label;
  final HubConnection connection;
  final events = <String, List<Map<String, dynamic>>>{};

  void listen(String method) {
    events[method] = [];
    connection.on(method, (args) {
      final first = (args != null && args.isNotEmpty) ? args.first : null;
      events[method]!.add(first is Map ? Map<String, dynamic>.from(first) : {'raw': '$first'});
    });
  }

  List<Map<String, dynamic>> got(String method) => events[method] ?? const [];

  /// Waits until [method] has at least [count] events, or gives up.
  ///
  /// A bounded wait, because a probe that hangs and a probe that passes look equally
  /// quiet from outside.
  Future<bool> waitFor(String method, {int count = 1, Duration limit = const Duration(seconds: 8)}) async {
    final deadline = DateTime.now().add(limit);
    while (DateTime.now().isBefore(deadline)) {
      if (got(method).length >= count) return true;
      await Future<void>.delayed(const Duration(milliseconds: 150));
    }
    return false;
  }
}

Future<_Ear> _connect(String label, String token, List<String> methods) async {
  final connection = HubConnectionBuilder()
      .withUrl('$_server/hubs/match?access_token=${Uri.encodeComponent(token)}')
      .build();
  final ear = _Ear(label, connection);
  // Subscribed BEFORE start(): a push can arrive the moment we join.
  for (final m in methods) {
    ear.listen(m);
  }
  await connection.start();
  return ear;
}

void main() {
  test(
    'chat, urge, spectate and resign all work over this transport',
    () async {
      final stamp = DateTime.now().millisecondsSinceEpoch.toString().substring(6);
      final findings = <String>[];
      final failures = <String>[];

      // **Measure all four, then assert.** A probe that stops at the first surprise has
      // only measured up to there — and the whole point of running this before any UI
      // is to learn every transport's real behaviour in one trip.
      void check(String what, bool ok) {
        findings.add('${ok ? "OK  " : "FAIL"}  $what');
        if (!ok) failures.add(what);
      }

      final a = await _register('soca', stamp);
      final b = await _register('socb', stamp);
      final c = await _register('socc', stamp);
      final aToken = a['accessToken'] as String;
      final bToken = b['accessToken'] as String;
      final cToken = c['accessToken'] as String;
      final aId = (a['user'] as Map<String, dynamic>)['id'] as String;
      final bId = (b['user'] as Map<String, dynamic>)['id'] as String;

      final room = await _post(
        '/api/rooms',
        {'name': 'social-$stamp', 'gameKey': 'gomoku'},
        token: aToken,
      );
      final roomId = room['id'] as String;
      await _post('/api/rooms/$roomId/join', {}, token: bToken);

      const watched = [
        'ChatMessage',
        'UrgeReceived',
        'SpectatorJoined',
        'SpectatorLeft',
        'GameEnded',
        'RoomState',
      ];
      final earA = await _connect('A', aToken, watched);
      final earB = await _connect('B', bToken, watched);
      final earC = await _connect('C', cToken, watched);

      await earA.connection.invoke('JoinRoom', args: [roomId]);
      await earB.connection.invoke('JoinRoom', args: [roomId]);

      // ======================================================================
      // 1. chat — and which wire form the enum argument binds to
      // ======================================================================
      // The string form first: that is what `JsonStringEnumConverter` implies, and it
      // is what this client would write.
      await earA.connection.invoke('SendChat', args: [roomId, 'hello-$stamp', 'Room']);
      final heardString = await earB.waitFor('ChatMessage');
      findings.add('SendChat with channel as STRING "Room" -> B heard: $heardString');
      expect(
        heardString,
        isTrue,
        reason: 'the string enum form must bind, or this client cannot chat at all',
      );

      final msg = earB.got('ChatMessage').last;
      expect(msg['content'], 'hello-$stamp');
      expect(msg['senderUserId'], aId, reason: 'the sender must be identifiable');
      findings.add('  channel came back as ${jsonEncode(msg['channel'])} '
          '(${msg['channel'].runtimeType}); sentAt=${msg['sentAt']}');

      // And the integer form, purely to record what happens. **Not asserted either
      // way** — the answer is a fact about the server, and writing down a guess is how
      // a client ends up sending the one form that is silently dropped.
      final beforeInt = earB.got('ChatMessage').length;
      var intFormError = '';
      try {
        await earA.connection.invoke('SendChat', args: [roomId, 'int-$stamp', 0]);
      } catch (e) {
        intFormError = '$e';
      }
      final heardInt = await earB.waitFor(
        'ChatMessage',
        count: beforeInt + 1,
        limit: const Duration(seconds: 3),
      );
      findings.add('SendChat with channel as INT 0 -> B heard: $heardInt'
          '${intFormError.isEmpty ? '' : ' (invoke threw: $intFormError)'}');

      // ======================================================================
      // 2. urge — pushed to the urged player ONLY
      // ======================================================================
      // B urges. A is black and moves first, so A is the one holding things up.
      // Whose turn is it, from the server rather than from an assumption? `Room` urges
      // **the player who owes a move**, so the seats matter.
      final beforeUrge = jsonDecode((await http.get(
        Uri.parse('$_server/api/rooms/$roomId'),
        headers: {'authorization': 'Bearer $aToken'},
      )).body) as Map<String, dynamic>;
      findings.add('before urge: status=${beforeUrge['status']}, '
          'currentSeat=${(beforeUrge['game'] as Map<String, dynamic>?)?['currentSeat']}, '
          'seats=${jsonEncode(beforeUrge['seats'])}');
      findings.add('  A=$aId  B=$bId');

      var urgeError = '';
      try {
        await earB.connection.invoke('Urge', args: [roomId]);
      } catch (e) {
        urgeError = '$e';
      }
      final aWasUrged = await earA.waitFor('UrgeReceived');
      findings.add('Urge by B -> A heard UrgeReceived: $aWasUrged; '
          'B heard ${earB.got('UrgeReceived').length}'
          '${urgeError.isEmpty ? '' : '; invoke threw: $urgeError'}');
      check('the urged player hears UrgeReceived', aWasUrged);
      if (aWasUrged) {
        check('and is told who urged', earA.got('UrgeReceived').last['fromUserId'] == bId);
      }
      // The negative half is only meaningful when the positive one delivered.
      check('the urger is not told they urged themselves', earB.got('UrgeReceived').isEmpty);

      // ======================================================================
      // 3. spectate — REST first, then the hub group
      // ======================================================================
      await _post('/api/rooms/$roomId/spectate', {}, token: cToken);
      // **Three steps, and the middle one is the one you would skip.** `JoinRoom` is
      // what puts a connection in the *room* group; `JoinSpectatorGroup` only adds the
      // spectator sub-group. The first version of this probe called only the second and
      // measured a spectator who could not hear the room channel — which reads exactly
      // like a server bug and is not one. `JoinRoom` also asks the aggregate what this
      // caller is, so the view group comes out right without the client asserting it.
      await earC.connection.invoke('JoinRoom', args: [roomId]);
      await earC.connection.invoke('JoinSpectatorGroup', args: [roomId]);
      final playersSawSpectator = await earA.waitFor('SpectatorJoined');
      findings.add('C spectated -> A heard SpectatorJoined: $playersSawSpectator');
      check('a player hears SpectatorJoined', playersSawSpectator);

      // A spectator can hear the room channel…
      final cBefore = earC.got('ChatMessage').length;
      await earA.connection.invoke('SendChat', args: [roomId, 'to-room-$stamp', 'Room']);
      final spectatorHeardRoom = await earC.waitFor('ChatMessage', count: cBefore + 1);
      findings.add('room-channel message -> spectator heard: $spectatorHeardRoom');
      check('a spectator hears the room channel', spectatorHeardRoom);

      // …and the spectator channel must NOT reach a player. **The precondition is the
      // line above plus this counter**: A demonstrably hears room-channel messages, so
      // "A heard nothing" below is about the channel and not about a dead subscription.
      final aBefore = earA.got('ChatMessage').length;
      expect(aBefore, greaterThan(0), reason: 'precondition: A does hear chat at all');
      await earC.connection.invoke('SendChat', args: [roomId, 'spec-only-$stamp', 'Spectator']);
      final aHeardSpectatorChannel = await earA.waitFor(
        'ChatMessage',
        count: aBefore + 1,
        limit: const Duration(seconds: 3),
      );
      findings.add('spectator-channel message -> player heard: $aHeardSpectatorChannel '
          '(must be false)');
      check('the spectator channel does NOT reach the table', !aHeardSpectatorChannel);

      // The room's snapshot must list the spectator, or a client cannot render 围观.
      final snap = jsonDecode((await http.get(
        Uri.parse('$_server/api/rooms/$roomId'),
        headers: {'authorization': 'Bearer $aToken'},
      )).body) as Map<String, dynamic>;
      findings.add('GET room -> spectators=${jsonEncode(snap['spectators'])}, '
          'chatMessages=${(snap['chatMessages'] as List<dynamic>).length}');
      check('the snapshot carries the spectator', (snap['spectators'] as List).length == 1);
      check('the snapshot carries chat history', (snap['chatMessages'] as List).isNotEmpty);

      // ======================================================================
      // 4. resign — REST, and the push that follows it
      // ======================================================================
      final resignRes = await http.post(
        Uri.parse('$_server/api/rooms/$roomId/resign'),
        headers: {'authorization': 'Bearer $aToken'},
      );
      findings.add('POST resign -> ${resignRes.statusCode} ${resignRes.body}');
      check('a two-seat game can be resigned', resignRes.statusCode < 400);

      final bHeardEnd = await earB.waitFor('GameEnded');
      findings.add('resign -> B heard GameEnded: $bHeardEnd');
      check('the other player hears GameEnded', bHeardEnd);
      if (bHeardEnd) {
        final ended = earB.got('GameEnded').last;
        findings.add('  GameEnded payload: ${jsonEncode(ended)}');
        check('result=Decided, reason=Resigned, winner=the other seat',
            ended['result'] == 'Decided' &&
                ended['endReason'] == 'Resigned' &&
                ended['winnerUserId'] == bId);
      }

      // Unspectating is the other half of the pair, and it has its own route.
      final unspec = await http.delete(
        Uri.parse('$_server/api/rooms/$roomId/spectate'),
        headers: {'authorization': 'Bearer $cToken'},
      );
      findings.add('DELETE spectate -> ${unspec.statusCode}');
      check('unspectate is DELETE /api/rooms/{id}/spectate', unspec.statusCode < 400);

      for (final ear in [earA, earB, earC]) {
        await ear.connection.stop();
      }

      stdout.writeln('\n=== room-social probe findings ===');
      for (final f in findings) {
        stdout.writeln('  $f');
      }
      expect(failures, isEmpty, reason: 'see the findings above');
    },
    timeout: const Timeout(Duration(minutes: 2)),
    skip: _server.isEmpty
        ? 'set GEWU_PROBE_SERVER (e.g. --dart-define=GEWU_PROBE_SERVER=http://127.0.0.1:5199) '
              'to run this against a live backend; it is NOT running now'
        : null,
  );
}
