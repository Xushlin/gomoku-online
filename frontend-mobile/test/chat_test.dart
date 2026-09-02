// Saying things in a room.
//
// The server has had all of this since day one: `SendChat` on the hub, a `ChatMessage`
// push, and the history riding on the room snapshot. This client had none of it.
//
// **Two facts here were measured against the live hub before any of it was written**
// (`test/room_social_probe_test.dart`), because SignalR rejects a badly-typed argument
// in the binding layer — before any filter, below the log level, invisible from both
// ends:
//
//   * the channel binds as the **string** `"Room"` (an integer binds too);
//   * `GET /api/rooms/{id}` really does carry `chatMessages`, so there is no second
//     endpoint to call for history.
import 'dart:convert';
import 'dart:io';

import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:gewu_mobile/data/models/models.dart';
import 'package:gewu_mobile/data/repositories/auth_repository.dart';
import 'package:gewu_mobile/data/repositories/game_catalog_repository.dart';
import 'package:gewu_mobile/data/repositories/room_repository.dart';
import 'package:gewu_mobile/data/services/dio_client.dart';
import 'package:gewu_mobile/data/services/match_hub_service.dart';
import 'package:gewu_mobile/data/services/token_store.dart';
import 'package:gewu_mobile/ui/game/view_model/game_view_model.dart';

const _me = 'me-1';

const _json = {
  Headers.contentTypeHeader: [Headers.jsonContentType],
};

const _games = '[{"gameKey":"gomoku","isRated":true,"supportsHumanVsHuman":true,'
    '"supportsAi":true,"seatCount":2,"rows":15,"cols":15}]';

Map<String, dynamic> messageJson(String id, String content, {String channel = 'Room'}) => {
  'id': id,
  'senderUserId': 'u-$id',
  'senderUsername': 'p$id',
  'content': content,
  'channel': channel,
  'sentAt': '2026-09-02T10:00:00Z',
};

String roomJson({List<Map<String, dynamic>> chat = const []}) => jsonEncode({
  'id': 'r1',
  'name': 'room',
  'gameKey': 'gomoku',
  'status': 'Playing',
  'seatCount': 2,
  'seats': [
    {'index': 0, 'player': {'id': _me, 'username': 'me'}},
    {'index': 1, 'player': {'id': 'them-1', 'username': 'them'}},
  ],
  'host': {'id': _me, 'username': 'me'},
  'game': {'moves': <dynamic>[], 'currentSeat': 0},
  'chatMessages': chat,
});

class ChatAdapter implements HttpClientAdapter {
  ChatAdapter(this.room);

  final String room;

  @override
  Future<ResponseBody> fetch(
    RequestOptions options,
    Stream<Uint8List>? requestStream,
    Future<void>? cancelFuture,
  ) async {
    if (options.path.startsWith('/api/games')) {
      return ResponseBody.fromString(_games, 200, headers: _json);
    }
    return ResponseBody.fromString(room, 200, headers: _json);
  }

  @override
  void close({bool force = false}) {}
}

/// A hub that records what was sent and can push a message back.
class ChatHub extends MatchHub {
  ChatHub({this.refusal})
      : super(serverAddress: 'http://example.invalid', accessToken: _empty);

  static String _empty() => '';

  final Object? refusal;

  /// Every `SendChat` invocation, as the argument list the hub would receive.
  final sent = <List<Object?>>[];
  final incoming = ValueNotifier<Map<String, dynamic>?>(null);
  final pushes = ValueNotifier<RoomSnapshot?>(null);
  final dissolves = ValueNotifier<int>(0);
  final urges = ValueNotifier<int>(0);

  @override
  ValueListenable<RoomSnapshot?> get state => pushes;

  @override
  ValueListenable<int> get dissolved => dissolves;

  @override
  ValueListenable<int> get urged => urges;

  @override
  ValueListenable<Map<String, dynamic>?> get chat => incoming;

  @override
  Future<void> joinRoom(String roomId) async {}

  @override
  Future<void> leaveRoom(String roomId) async {}

  @override
  Future<void> sendChat(String roomId, String content, ChatChannelWire channel) async {
    sent.add([roomId, content, channel.wire]);
    if (refusal != null) throw refusal!;
  }

  void arrive(Map<String, dynamic> message) => incoming.value = message;
}

Future<({GameViewModel vm, ChatHub hub})> open(
  String room, {
  Object? refusal,
}) async {
  final hub = ChatHub(refusal: refusal);
  final dio = buildDio(
    baseUrl: 'http://example.invalid',
    tokens: MemoryTokenStore(),
    refresh: () async => false,
    adapter: ChatAdapter(room),
  );
  final catalog = GameCatalogRepository(dio);
  await catalog.load();
  final auth = AuthRepository(dio: dio, tokens: MemoryTokenStore())
    ..currentUser = const AuthUser(id: _me, username: 'me');

  final vm = GameViewModel(
    rooms: RoomRepository(dio: dio, hub: hub),
    catalog: catalog,
    auth: auth,
    roomId: 'r1',
  );
  await vm.open();
  return (vm: vm, hub: hub);
}

void main() {
  group('the channel', () {
    test('is parsed by name, and an unknown one stays unknown', () {
      expect(ChatChannel.parse('Room'), ChatChannel.room);
      expect(ChatChannel.parse('Spectator'), ChatChannel.spectator);
      // **Not `room`.** Collapsing an unrecognised channel to the room channel would
      // take something the platform deliberately keeps away from the table and
      // broadcast it there.
      expect(ChatChannel.parse('SomethingNew'), ChatChannel.unknown);
      expect(ChatChannel.parse(null), ChatChannel.unknown);
    });

    test('the service-side wire names agree with the model', () {
      // `data/services` may not import `data/models`, so the two-value wire enum is a
      // deliberate duplicate. Two values is small enough that this assertion keeps them
      // honest — and it is the assertion, not the size, that makes the copy acceptable.
      expect(ChatChannelWire.room.wire, ChatChannel.room.wire);
      expect(ChatChannelWire.spectator.wire, ChatChannel.spectator.wire);
    });
  });

  group('history and pushes', () {
    test('opening a room already has what was said', () async {
      final o = await open(roomJson(chat: [
        messageJson('1', 'a'),
        messageJson('2', 'b'),
        messageJson('3', 'c'),
      ]));
      expect(o.vm.chatMessages.map((m) => m.content), ['a', 'b', 'c']);
    });

    test('a push appends and leaves the history alone', () async {
      // **The headline.** The server pushes one message, not the conversation; a
      // listener that assigned would wipe the history the first time anybody spoke.
      //
      // Positive control: replace the append with an assignment and this goes red.
      final o = await open(roomJson(chat: [
        messageJson('1', 'a'),
        messageJson('2', 'b'),
        messageJson('3', 'c'),
      ]));
      expect(o.vm.chatMessages, hasLength(3), reason: 'precondition');

      o.hub.arrive(messageJson('4', 'd'));
      expect(o.vm.chatMessages.map((m) => m.content), ['a', 'b', 'c', 'd']);
    });

    test('the same message twice does not appear twice', () async {
      // A reconnect can replay. Ids are what tell them apart.
      final o = await open(roomJson());
      o.hub.arrive(messageJson('1', 'a'));
      o.hub.arrive(messageJson('1', 'a'));
      expect(o.vm.chatMessages, hasLength(1));
    });

    test('the sender name is the one the server recorded', () async {
      final o = await open(roomJson(chat: [messageJson('1', 'a')]));
      expect(o.vm.chatMessages.single.senderUsername, 'p1');
    });

    test('the listener is removed on dispose', () async {
      final o = await open(roomJson());
      o.hub.arrive(messageJson('1', 'a'));
      expect(o.vm.chatMessages, hasLength(1), reason: 'precondition — it was live');

      var notifications = 0;
      o.vm.addListener(() => notifications++);
      o.vm.dispose();
      o.hub.arrive(messageJson('2', 'b'));
      expect(notifications, 0, reason: 'a disposed screen must not be notified');
    });
  });

  group('sending', () {
    test('exactly three arguments, and the channel is the string Room', () async {
      // SignalR applies no optional-parameter defaults in either direction, so the
      // count is part of the contract, not a detail.
      final o = await open(roomJson());
      await o.vm.sendChat('hello');
      expect(o.hub.sent, hasLength(1));
      expect(o.hub.sent.single, ['r1', 'hello', 'Room']);
      expect(o.hub.sent.single, hasLength(3));
    });

    test('whitespace alone is not sent, and that is not a legality judgement', () async {
      final o = await open(roomJson());
      // **The precondition is the test.** "It did not send" is green when the whole
      // path is broken, so first prove the path works.
      await o.vm.sendChat('real');
      expect(o.hub.sent, hasLength(1), reason: 'precondition — sending works');

      await o.vm.sendChat('   ');
      expect(o.hub.sent, hasLength(1), reason: 'nothing to send is not a refusal');
    });

    test('a refusal about length says so', () async {
      final o = await open(
        roomJson(),
        refusal: Exception('ChatMessageTooLong'),
      );
      await o.vm.sendChat('x');
      expect(o.vm.chatErrorKey, 'game.chat.max-length-error');
    });

    test('a refusal that is not about length does not claim to be', () async {
      // Without this, mapping everything to the length message passes the test above —
      // and telling somebody their message is too long when the connection dropped is a
      // wrong answer that looks like a right one.
      final o = await open(roomJson(), refusal: Exception('WebSocket closed'));
      await o.vm.sendChat('x');
      expect(o.vm.chatErrorKey, 'game.errors.generic');
    });

    test('a chat failure does not look like a move failure', () async {
      final o = await open(roomJson(), refusal: Exception('InvalidChatMessage'));
      await o.vm.sendChat('x');
      expect(o.vm.chatErrorKey, 'game.errors.invalid-chat');
      expect(o.vm.errorKey, isNull, reason: 'the board must not show a chat error');
    });
  });

  group('there is no spectator channel here yet', () {
    test('the panel names no spectator key', () {
      // A tab for a channel only spectators can reach, on a screen only players can
      // reach, is a permanently empty tab — and an empty tab looks like a broken one.
      // This client cannot spectate yet; the tab arrives with that ability.
      final source = File('lib/ui/game/view/chat_panel.dart').readAsLinesSync().where((l) {
        final t = l.trimLeft();
        return !t.startsWith('//') && !t.startsWith('*') && !t.startsWith('/*');
      }).join(' ');
      expect(source.contains('tab-spectator'), isFalse);
      expect(source.contains('tab-room'), isFalse, reason: 'one channel needs no tabs');
      // Non-vacuity: the file is the one we think it is and does render chat.
      expect(source.contains('game.chat.send'), isTrue);
    });

    test('and a spectator-channel message is not shown to a player', () async {
      final o = await open(roomJson(chat: [
        messageJson('1', 'to the room'),
        messageJson('2', 'behind their backs', channel: 'Spectator'),
      ]));
      // The model keeps both — the server sent both — and the panel filters. Assert the
      // filter the panel uses, so this stays true if the panel is rebuilt.
      expect(o.vm.chatMessages, hasLength(2));
      expect(
        o.vm.chatMessages.where((m) => m.channel == ChatChannel.room).map((m) => m.content),
        ['to the room'],
      );
    });
  });

  group('every key this can produce has copy', () {
    test('in both locales', () {
      const keys = [
        'game.chat.title',
        'game.chat.empty',
        'game.chat.placeholder',
        'game.chat.send',
        'game.chat.max-length-error',
        'game.errors.invalid-chat',
      ];
      for (final locale in const ['zh-CN', 'en']) {
        final bundle = jsonDecode(
          File('assets/i18n/$locale.json').readAsStringSync(),
        ) as Map<String, dynamic>;
        String? lookup(String key) {
          dynamic node = bundle;
          for (final part in key.split('.')) {
            if (node is! Map<String, dynamic>) return null;
            node = node[part];
          }
          return node is String ? node : null;
        }

        expect(
          [for (final k in keys) if (lookup(k) == null) k],
          equals(<String>[]),
          reason: '$locale — a chat panel rendering a raw key is worse than none',
        );
      }
    });
  });
}
