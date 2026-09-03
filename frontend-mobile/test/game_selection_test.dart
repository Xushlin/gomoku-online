// Two taps make a relocation, and the rules are entirely about the destination.
//
// **The first rule is also a constraint on every other test of this area:** tapping a
// piece of the same side re-selects it and sends nothing, so "move onto my own piece"
// can never exercise a server rejection — nothing leaves the client. A test of an
// illegal move needs a destination that is empty or enemy, which is also the only way
// to know this client did not quietly block the move itself.
import 'dart:async';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:gewu_mobile/data/models/models.dart';
import 'package:gewu_mobile/data/repositories/auth_repository.dart';
import 'package:gewu_mobile/data/repositories/game_catalog_repository.dart';
import 'package:gewu_mobile/data/repositories/room_repository.dart';
import 'package:gewu_mobile/data/services/dio_client.dart';
import 'package:gewu_mobile/data/services/match_hub_service.dart';
import 'package:gewu_mobile/data/services/token_store.dart';
import 'package:gewu_mobile/ui/game/board_registry.dart';
import 'package:gewu_mobile/ui/game/view_model/game_view_model.dart';
import 'package:gewu_mobile/data/repositories/settings_repository.dart';
import 'package:gewu_mobile/data/repositories/sound_repository.dart';
import 'package:gewu_mobile/data/services/preferences_store.dart';
import 'package:gewu_mobile/data/services/sound_player.dart';
import 'package:gewu_mobile/ui/game/xiangqi/position.dart';

/// What the client sent, if anything. **A hub that records instead of connecting**, so
/// "nothing was sent" is an assertion rather than the absence of a crash.
class RecordingHub extends MatchHub {
  RecordingHub() : super(serverAddress: 'http://example.invalid', accessToken: _empty);

  static String _empty() => '';

  final placements = <List<int>>[];
  final relocations = <List<int>>[];

  @override
  Future<void> makeMove(String roomId, int row, int col) async {
    placements.add([row, col]);
  }

  @override
  Future<void> movePiece(
    String roomId,
    int fromRow,
    int fromCol,
    int row,
    int col,
  ) async {
    relocations.add([fromRow, fromCol, row, col]);
  }
}

class FixedAdapter implements HttpClientAdapter {
  FixedAdapter(this.body);

  final String body;

  @override
  Future<ResponseBody> fetch(
    RequestOptions options,
    Stream<Uint8List>? requestStream,
    Future<void>? cancelFuture,
  ) async => ResponseBody.fromString(
    body,
    200,
    headers: {
      Headers.contentTypeHeader: [Headers.jsonContentType],
    },
  );

  @override
  void close({bool force = false}) {}
}

const _games = '[{"gameKey":"xiangqi","isRated":true,"supportsHumanVsHuman":true,'
    '"supportsAi":true,"seatCount":2,"rows":10,"cols":9},'
    '{"gameKey":"gomoku","isRated":true,"supportsHumanVsHuman":true,'
    '"supportsAi":true,"seatCount":2,"rows":15,"cols":15}]';

/// A view model already holding an open room of [gameKey], with no network in play.
Future<({GameViewModel vm, RecordingHub hub})> openRoom(String gameKey) async {
  final hub = RecordingHub();
  final dio = buildDio(
    baseUrl: 'http://example.invalid',
    tokens: MemoryTokenStore(),
    refresh: () async => false,
    adapter: FixedAdapter(_games),
  );
  final catalog = GameCatalogRepository(dio);
  await catalog.load();

  final vm = GameViewModel(
    rooms: RoomRepository(dio: dio, hub: hub),
    catalog: catalog,
    auth: AuthRepository(dio: dio, tokens: MemoryTokenStore()),
    sound: recordingSound(),
    roomId: 'r1',
  );
  // Set the room directly: `open()` would go through the hub, and what is under test is
  // the tap logic, not the connection.
  vm.room = Room(
    id: 'r1',
    name: 'test',
    gameKey: gameKey,
    status: RoomStatus.playing,
    seats: const [],
    game: GameSnapshot.empty,
  );
  return (vm: vm, hub: hub);
}

/// A sound repository over a fake device, so a test can assert what was played — and,
/// just as importantly, what was not.
SoundRepository recordingSound([RecordingSoundPlayer? player]) => SoundRepository(
  player: player ?? RecordingSoundPlayer(),
  settings: SettingsRepository(MemoryPreferencesStore()),
);

void main() {
  group('五子棋 places on one tap', () {
    test('a tap is sent immediately and nothing is ever selected', () async {
      final open = await openRoom(gomokuGameKey);
      await open.vm.tap(7, 7);

      expect(open.hub.placements, [
        [7, 7],
      ]);
      expect(open.hub.relocations, isEmpty);
      expect(open.vm.selected, isNull, reason: 'a placement game has no selection step');
    });
  });

  group('象棋 needs two taps', () {
    test('the first tap picks up a piece and sends nothing', () async {
      final open = await openRoom(xiangqiGameKey);
      // Red's right cannon.
      await open.vm.tap(7, 7);

      expect(open.vm.selected, (7, 7));
      expect(open.hub.relocations, isEmpty, reason: 'selecting is not moving');
      expect(open.hub.placements, isEmpty, reason: 'and it must not use MakeMove');
    });

    test('a tap on an empty intersection with nothing selected does nothing', () async {
      final open = await openRoom(xiangqiGameKey);
      await open.vm.tap(5, 5); // the river

      expect(open.vm.selected, isNull);
      expect(open.hub.relocations, isEmpty);
    });

    test('the second tap on an empty square is sent as from -> to', () async {
      final open = await openRoom(xiangqiGameKey);
      await open.vm.tap(7, 7);
      await open.vm.tap(7, 4); // 炮二平五

      expect(open.hub.relocations, [
        [7, 7, 7, 4],
      ]);
      expect(open.hub.placements, isEmpty, reason: 'MovePiece, not MakeMove');
      expect(open.vm.selected, isNull, reason: 'the selection is spent');
    });

    test('tapping another piece of the same side re-selects, and sends nothing', () async {
      final open = await openRoom(xiangqiGameKey);
      await open.vm.tap(7, 7); // red cannon
      await open.vm.tap(9, 0); // red chariot

      expect(open.vm.selected, (9, 0));
      expect(open.hub.relocations, isEmpty, reason: 'this is the rule that makes '
          '"move onto my own piece" untestable as a server rejection');
    });

    test('tapping the selected square deselects', () async {
      final open = await openRoom(xiangqiGameKey);
      await open.vm.tap(7, 7);
      await open.vm.tap(7, 7);

      expect(open.vm.selected, isNull);
      expect(open.hub.relocations, isEmpty);
    });

    test('a destination holding an enemy piece IS sent — the server judges it', () async {
      final open = await openRoom(xiangqiGameKey);
      await open.vm.tap(9, 0); // red chariot, bottom-left
      await open.vm.tap(0, 0); // black chariot, top-left — an illegal move, and not ours to refuse

      expect(open.hub.relocations, [
        [9, 0, 0, 0],
      ]);
      expect(open.vm.selected, isNull);
    });

    test('an obviously illegal move to an empty square is still sent', () async {
      // The whole point of design D2, stated as a test: this client must not have an
      // opinion. A cannon cannot reach (0, 4) in one move; it goes to the server anyway.
      final open = await openRoom(xiangqiGameKey);
      await open.vm.tap(7, 7);
      await open.vm.tap(0, 4);

      expect(open.hub.relocations, hasLength(1));
    });
  });

  group('the seat label follows the game, not the seat count', () {
    test('象棋 says red for seat 0 and 五子棋 says black', () async {
      final xiangqi = await openRoom(xiangqiGameKey);
      final gomoku = await openRoom(gomokuGameKey);

      for (final open in [xiangqi, gomoku]) {
        open.vm.room = Room(
          id: 'r1',
          name: 'test',
          gameKey: open.vm.room!.gameKey,
          status: RoomStatus.playing,
          seats: const [],
          game: const GameSnapshot(moves: [], currentSeat: redSeat),
        );
      }

      expect(xiangqi.vm.turnSeatLabelKey, 'game.seat.red');
      expect(gomoku.vm.turnSeatLabelKey, 'game.seat.black');
      // Both games have two seats — which is why a seat-count criterion cannot tell
      // them apart, and why this pair of assertions has to exist together.
      expect(xiangqi.vm.descriptor!.seatCount, gomoku.vm.descriptor!.seatCount);
    });
  });
}
