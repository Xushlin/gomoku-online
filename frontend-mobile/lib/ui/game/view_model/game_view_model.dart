import '../../../data/models/models.dart';
import '../../../data/repositories/game_catalog_repository.dart';
import '../../../data/repositories/room_repository.dart';
import '../../view_model.dart';

/// One room in play.
///
/// It owns the subscription to the repository's live room and republishes as its own
/// change notification, so the View listens to exactly one thing.
class GameViewModel extends ViewModel {
  GameViewModel({
    required this._rooms,
    required this._catalog,
    required this.roomId,
  });

  final RoomRepository _rooms;
  final GameCatalogRepository _catalog;
  final String roomId;

  Room? room;
  bool sending = false;
  String? errorKey;

  List<Move> get moves => room?.game.moves ?? const [];

  /// The board's shape, read from **the room's own `gameKey`** — not from the route.
  ///
  /// The server's `RoomStateDto` doc gives the reason: there are four ways into a room
  /// (a redirect from creating it, a reload, a bookmarked link, "my games") and only
  /// the first leaves the client already knowing the game. Null until the catalogue
  /// and the room are both in; the View shows a loading state rather than guessing a
  /// size, because **a default board size is how 10×9 gets painted as 15×15.**
  GameDescriptor? get descriptor {
    final key = room?.gameKey;
    return key == null || key.isEmpty ? null : _catalog.of(key);
  }

  Future<void> open() async {
    _rooms.live.addListener(_onPush);
    try {
      // The catalogue before the room: the board cannot be drawn without it, and it is
      // cached after the first load so this is free on every later room.
      await _catalog.load();
      room = await _rooms.open(roomId);
    } on RoomFailure {
      errorKey = 'game.errors.generic';
    } catch (_) {
      errorKey = 'game.errors.network';
    }
    notifyIfAlive();
  }

  void _onPush() {
    room = _rooms.live.value ?? room;
    notifyIfAlive();
  }

  /// **No legality check here** — the server owns that (design D2). A second copy of
  /// the rules is a second truth, and the player reads the disagreement as a bug.
  Future<void> place(int row, int col) async {
    if (sending) return;
    sending = true;
    errorKey = null;
    notifyIfAlive();
    try {
      await _rooms.makeMove(roomId, row, col);
    } catch (_) {
      errorKey = 'game.errors.invalid-move';
    } finally {
      sending = false;
      notifyIfAlive();
    }
  }

  @override
  void dispose() {
    _rooms.live.removeListener(_onPush);
    super.dispose();
  }
}
