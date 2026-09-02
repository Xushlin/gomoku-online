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

  /// Which seat this user occupies, or null when they are not playing this game.
  ///
  /// **By id, never by username** — a username is a display name and this platform has
  /// already paid twice for treating one as an identity.
  int? get mySeat {
    final me = _auth.currentUser?.id;
    if (me == null) return null;
    for (final seat in room?.seats ?? const <RoomSeat>[]) {
      if (seat.playerId == me) return seat.index;
    }
    return null;
  }

  bool get _playingNow => room?.status == RoomStatus.playing && mySeat != null;

  /// Whether this user is watching rather than playing.
  bool get isSpectator => room?.isSpectator(_auth.currentUser?.id) ?? false;

  /// Which chat channels this user can actually reach.
  ///
  /// **The criterion is who can reach the channel, not whether this client supports
  /// spectating.** Written the second way, the spectator tab would appear in front of
  /// players the day spectating shipped — a permanently empty tab, which looks like a
  /// broken one.
  List<ChatChannel> get chatChannels =>
      isSpectator ? const [ChatChannel.room, ChatChannel.spectator] : const [ChatChannel.room];

  /// Which channel new messages go to. Room unless the user picked the other one.
  ChatChannel chatChannel = ChatChannel.room;

  void chooseChatChannel(ChatChannel channel) {
    if (!chatChannels.contains(channel)) return;
    chatChannel = channel;
    notifyIfAlive();
  }

  /// Whether the resign entry may be shown at all.
  ///
  /// **Three conditions, and the third is the one that is not obvious.** `Room.Resign`
  /// needs exactly two seats to name a winner; on a three-seat game the API answers 409
  /// and the web client once returned a **500** on a real click because the client
  /// assumed the count. Today both games here are two-seat, so this is constantly true
  /// — **which is exactly why it is asserted against a fabricated three-seat room**, or
  /// the branch would be an empty loop.
  ///
  /// The number comes from **the room**, not from the game catalogue: it is this room
  /// being resigned, and the room already says how many seats it has.
  bool get canResign => _playingNow && room?.totalSeats == 2;

  /// Whether the urge entry may be shown at all. Being *able* to press it is
  /// [urgeDisabledReasonKey] being null.
  bool get canUrge => _playingNow;

  /// Why the urge button cannot be pressed right now, or null when it can.
  ///
  /// **No cooldown timer lives here.** Whether the server will accept an urge is the
  /// server's conclusion; this client only repeats back the 429 it was given. A
  /// parallel 30-second timer would be a second copy of a rule, and the way two copies
  /// fail is the button saying "yes" while the server says "no".
  String? get urgeDisabledReasonKey {
    if (!canUrge) return null;
    if (room?.game.currentSeat == mySeat) return 'game.urge.button-disabled-own-turn';
    if (_urgeRefused) return 'game.urge.button-disabled-cooldown';
    return null;
  }

  bool _urgeRefused = false;

  /// Gives up the game. **The caller has already asked** — this method does not.
  ///
  /// It writes down nothing about the outcome on success. The result reaches the screen
  /// through the one path that already exists ([outcome], fed by the snapshot and the
  /// `GameEnded` push); a second path would be a second answer to "who won".
  Future<void> resign() async {
    if (sending) return;
    sending = true;
    errorKey = null;
    notifyIfAlive();
    try {
      await _rooms.resign(roomId);
    } catch (_) {
      errorKey = 'game.errors.generic';
    } finally {
      sending = false;
      notifyIfAlive();
    }
  }

  /// Urges whoever owes a move.
  Future<void> urge() async {
    if (sending) return;
    sending = true;
    errorKey = null;
    notifyIfAlive();
    try {
      await _rooms.urge(roomId);
      _urgeRefused = false;
    } on RoomFailure catch (failure) {
      _refuseUrge(failure.status);
    } catch (e) {
      // The hub reports a domain refusal as a `HubException` whose message carries the
      // code — there is no status line on that path, so the code is what identifies it.
      _refuseUrge(null, '$e');
    } finally {
      sending = false;
      notifyIfAlive();
    }
  }

  void _refuseUrge(int? status, [String message = '']) {
    final cooldown = status == 429 || message.contains('UrgeTooFrequent');
    _urgeRefused = cooldown;
    errorKey = cooldown ? 'game.errors.urge-cooldown' : 'game.errors.generic';
  }

  /// Everything said in this room, oldest first.
  List<ChatMessage> get chatMessages => _rooms.chat.value;

  /// The error from the last send, or null. Separate from [errorKey] so a refused
  /// message does not look like a refused move.
  String? chatErrorKey;

  bool sendingChat = false;

  /// Says something in the room channel.
  ///
  /// **Whitespace-only is not sent, and that is not a legality judgement** — there is
  /// simply nothing to send. Whether a message is acceptable (trim 1-500) is the
  /// server's call, and this client does not keep a second copy of that rule: two
  /// copies diverge, and the way it shows is the input saying yes while the server
  /// says no.
  Future<void> sendChat(String content) async {
    if (sendingChat || content.trim().isEmpty) return;
    sendingChat = true;
    chatErrorKey = null;
    notifyIfAlive();
    try {
      await _rooms.sendChat(roomId, content, chatChannel);
    } catch (e) {
      chatErrorKey = _chatErrorFor('$e');
    } finally {
      sendingChat = false;
      notifyIfAlive();
    }
  }

  /// Maps the server's refusal to copy.
  ///
  /// **Not everything is a length problem.** Mapping every failure to the length
  /// message would pass a test that only checks the length case, and telling somebody
  /// their message is too long when the connection dropped is a wrong answer that
  /// looks like a right one.
  static String _chatErrorFor(String message) {
    if (message.contains('TooLong') || message.contains('MaxLength')) {
      return 'game.chat.max-length-error';
    }
    if (message.contains('Chat') || message.contains('Invalid')) {
      return 'game.errors.invalid-chat';
    }
    return 'game.errors.generic';
  }

  void _onChat() => notifyIfAlive();

  /// Bumped each time somebody urges this user. The View shows a toast on it.
  int urgeCount = 0;

  /// Who urged, last — for the toast. Null before the first one.
  String? get urgedBy => _rooms.lastUrgedBy;

  void _onUrged() {
    urgeCount = _rooms.urged.value;
    // A fresh urge means the other side is waiting, so it cannot be our own cooldown.
    notifyIfAlive();
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
  /// Whether leaving needs to ask.
  ///
  /// **Not for a spectator.** The warning is about a seat whose clock keeps running;
  /// a spectator has no seat and no clock, so asking would be a question with no
  /// consequence behind it.
  bool get leavingNeedsConfirmation =>
      room?.status == RoomStatus.playing && !isSpectator;

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
      if (isSpectator) {
        await _rooms.unspectate(roomId);
      } else {
        await _rooms.leave(roomId, asHostOfWaitingRoom: leavingDissolves);
      }
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
    _rooms.urged.addListener(_onUrged);
    _rooms.chat.addListener(_onChat);
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

  /// Set once the result has been shown and dismissed, so it is not re-announced on
  /// every push. **Re-popping a dialog on each snapshot is worse than not popping one.**
  bool outcomeDismissed = false;

  /// What to say when the game is over, or null while it is still on.
  ///
  /// Win or lose is decided by **user id**, never by username: a username is a display
  /// name, and this platform has already paid twice for treating one as an identity.
  ({String titleKey, String? reasonKey})? get outcome {
    final game = room?.game;
    if (game == null || !game.isOver) return null;

    final titleKey = switch (game.result) {
      GameResult.draw => 'game.ended.title-draw',
      GameResult.decided =>
        game.winnerUserId != null && game.winnerUserId == _auth.currentUser?.id
            ? 'game.ended.title-win'
            : 'game.ended.title-lose',
      _ => null,
    };
    if (titleKey == null) return null;

    final reasonKey = switch (game.endReason) {
      GameEndReason.decided => 'game.ended.reason-decided',
      GameEndReason.resigned => 'game.ended.reason-resigned',
      GameEndReason.turnTimeout => 'game.ended.reason-timeout',
      _ => null,
    };
    return (titleKey: titleKey, reasonKey: reasonKey);
  }

  void dismissOutcome() {
    outcomeDismissed = true;
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
    // **A spectator sends nothing, and the check is here rather than in the View.** A
    // rule enforced only by not drawing something stops holding the moment a second
    // path reaches this object — and there are already four ways into a room.
    if (sending || isSpectator) return;
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
    _rooms.urged.removeListener(_onUrged);
    _rooms.chat.removeListener(_onChat);
    super.dispose();
  }
}
