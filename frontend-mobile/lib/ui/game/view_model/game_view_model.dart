import '../../../data/models/models.dart';
import '../../../data/repositories/auth_repository.dart';
import '../../../data/repositories/game_catalog_repository.dart';
import '../../../data/repositories/room_repository.dart';
import '../../view_model.dart';
import '../board_registry.dart';
import '../seat_labels.dart';
import '../view/board_renderer.dart';

/// One room in play.
///
/// It owns the subscription to the repository's live room and republishes as its own
/// change notification, so the View listens to exactly one thing.
class GameViewModel extends ViewModel {
  GameViewModel({
    required this._rooms,
    required this._catalog,
    required this._auth,
    required this.roomId,
  });

  final RoomRepository _rooms;
  final GameCatalogRepository _catalog;
  final AuthRepository _auth;
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

  /// This game's board, from the registry — the only place this client knows a game.
  BoardRenderer? get _renderer {
    final key = descriptor?.gameKey;
    return key == null ? null : rendererFor(key);
  }

  /// The translation key naming whichever seat is to play, or null when this game's
  /// seats have no name. **By game key, never by seat count.**
  String? get turnSeatLabelKey {
    final key = descriptor?.gameKey;
    final seat = room?.game.currentSeat;
    return key == null || seat == null ? null : seatLabelKey(key, seat);
  }

  /// Whether leaving takes the host's route.
  ///
  /// **The server decides this, not the UI:** `/leave` refuses the host of a *waiting*
  /// room (`HostCannotLeaveWaitingRoom`), and `/dissolve` exists only for waiting
  /// rooms. Compared by **id**, because a username is a display name and this platform
  /// has already paid twice for treating one as an identity.
  bool get leavingDissolves {
    final current = room;
    if (current == null) return false;
    return current.status == RoomStatus.waiting &&
        current.hostId != null &&
        current.hostId == _auth.currentUser?.id;
  }

  /// Whether leaving needs to ask first.
  ///
  /// Only a game in play: leaving does not end it and the seat stays yours, but the
  /// turn clock keeps running and timing out has consequences. A waiting room has
  /// nothing to warn about — **the same criterion the web client uses, deliberately
  /// not a second one**: two rules diverge, and the way divergence shows up is one path
  /// quietly stopping asking.
  bool get leavingNeedsConfirmation => room?.status == RoomStatus.playing;

  /// The warning to show, or null when none is needed.
  String? get leaveWarningKey =>
      leavingNeedsConfirmation ? 'game.leave-confirm.match' : null;

  /// Leaves the room on the server and on the hub.
  ///
  /// Returns true when the caller should navigate away. False means the server refused
  /// and the error is on [errorKey] — **navigating anyway would tell the player they
  /// left a room they are still sitting in.**
  Future<bool> leave() async {
    if (sending) return false;
    sending = true;
    errorKey = null;
    notifyIfAlive();
    try {
      await _rooms.leave(roomId, asHostOfWaitingRoom: leavingDissolves);
      return true;
    } catch (_) {
      errorKey = 'game.errors.generic';
      return false;
    } finally {
      sending = false;
      notifyIfAlive();
    }
  }

  Future<void> open() async {
    _rooms.live.addListener(_onPush);
    _rooms.dissolved.addListener(_onDissolved);
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

  /// True once the host dissolved this room. The View navigates out on it.
  bool wasDissolved = false;

  void _onDissolved() {
    wasDissolved = true;
    notifyIfAlive();
  }

  /// The chosen origin, for a game that relocates pieces. Null for 五子棋 always.
  (int, int)? selected;

  /// One tap on the board.
  ///
  /// A placement game sends immediately. A relocation game needs two taps, and the
  /// rules for the second one are **entirely about the destination**, never about
  /// whether the move is legal:
  ///
  /// - an occupant of the same side as the selected piece **re-selects**, sending
  ///   nothing;
  /// - the selected square itself deselects;
  /// - anything else — empty or enemy — is sent, and the server decides.
  ///
  /// **That first rule is also a constraint on tests:** "move onto my own piece" can
  /// never exercise a server rejection, because nothing leaves the client. A test of an
  /// illegal move needs a destination that is empty or enemy, which is also the only
  /// way to know this client did not quietly block it itself.
  Future<void> tap(int row, int col) async {
    if (sending) return;
    final renderer = _renderer;
    if (renderer == null) return;

    if (!renderer.relocates) {
      await _send(() => _rooms.makeMove(roomId, row, col));
      return;
    }

    final origin = selected;
    if (origin == null) {
      if (renderer.seatAt(moves, row, col) == null) return;
      selected = (row, col);
      notifyIfAlive();
      return;
    }

    if (origin == (row, col)) {
      selected = null;
      notifyIfAlive();
      return;
    }

    final originSeat = renderer.seatAt(moves, origin.$1, origin.$2);
    if (originSeat != null && renderer.seatAt(moves, row, col) == originSeat) {
      selected = (row, col);
      notifyIfAlive();
      return;
    }

    selected = null;
    await _send(() => _rooms.movePiece(roomId, origin.$1, origin.$2, row, col));
  }

  /// **No legality check anywhere in here** — the server owns that (design D2). A second
  /// copy of the rules is a second truth, and the player reads the disagreement as a bug.
  Future<void> _send(Future<void> Function() action) async {
    sending = true;
    errorKey = null;
    notifyIfAlive();
    try {
      await action();
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
    _rooms.dissolved.removeListener(_onDissolved);
    super.dispose();
  }
}
