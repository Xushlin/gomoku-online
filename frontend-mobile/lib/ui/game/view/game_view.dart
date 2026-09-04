import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../../data/models/models.dart';
import '../../../i18n/translations.dart';
import '../../../theme/app_theme.dart';
import '../view_model/game_view_model.dart';
import '../../router.dart';
import '../board_registry.dart';
import 'chat_panel.dart';
import 'game_board.dart';

class GameView extends StatefulWidget {
  const GameView({super.key});

  @override
  State<GameView> createState() => _GameViewState();
}

class _GameViewState extends State<GameView> {
  /// Set once we are on our way out, so neither exit path fires twice.
  bool _leaving = false;

  /// Guards the result dialog against the next push re-opening it while it is up.
  bool _announcingOutcome = false;

  /// How many urges had arrived the last time we showed one.
  ///
  /// **A high-water mark rather than a flag**, because being urged twice has to be
  /// visible twice — and because `build` runs on every push, so "show it once" needs
  /// something to compare against.
  int _urgesShown = 0;

  /// Asks before giving up. **Irreversible, so the question is not optional.**
  ///
  /// It deliberately writes nothing down about the result: that arrives through the
  /// snapshot and the `GameEnded` push, which is the one path that already exists.
  Future<void> _resign(GameViewModel vm) async {
    final t = context.read<Translations>();
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: Text(t.t('game.actions.resign-confirm-title')),
        content: Text(t.t('game.actions.resign-confirm-body')),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(context).pop(false),
            child: Text(t.t('game.actions.resign-confirm-cancel')),
          ),
          TextButton(
            onPressed: () => Navigator.of(context).pop(true),
            child: Text(t.t('game.actions.resign-confirm-ok')),
          ),
        ],
      ),
    );
    if (confirmed != true || !mounted) return;
    await vm.resign();
  }

  /// Says who won, and offers the two ways out the copy already names.
  Future<void> _announce(
    GameViewModel vm,
    ({String titleKey, String? reasonKey}) outcome,
  ) async {
    if (!mounted) return;
    final t = context.read<Translations>();

    final backToLobby = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: Text(t.t(outcome.titleKey)),
        content: outcome.reasonKey == null ? null : Text(t.t(outcome.reasonKey!)),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(context).pop(false),
            child: Text(t.t('game.ended.dismiss')),
          ),
          TextButton(
            onPressed: () => Navigator.of(context).pop(true),
            child: Text(t.t('game.ended.back-to-lobby')),
          ),
        ],
      ),
    );
    if (!mounted) return;

    // Dismissed either way: re-announcing on every later push would be a worse defect
    // than not announcing at all.
    vm.dismissOutcome();
    _announcingOutcome = false;

    if (backToLobby == true) {
      // The game is over, so there is nothing to leave — no confirmation, no server
      // call. Just go.
      _leaving = true;
      _exit(vm);
    }
  }

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted) context.read<GameViewModel>().open();
    });
  }

  /// Leaves the room. **The single exit**, so the on-screen arrow and the system back
  /// button cannot disagree — they both arrive here through `PopScope`.
  Future<void> _leave(GameViewModel vm) async {
    if (_leaving) return;
    final t = context.read<Translations>();

    final warning = vm.leaveWarningKey;
    if (warning != null) {
      // `showDialog` rather than a hand-rolled overlay: focus trapping, the barrier and
      // back-button handling are not things to reimplement.
      final confirmed = await showDialog<bool>(
        context: context,
        builder: (context) => AlertDialog(
          title: Text(t.t('game.leave-confirm.title')),
          content: Text(t.t(warning)),
          actions: [
            TextButton(
              onPressed: () => Navigator.of(context).pop(false),
              child: Text(t.t('game.leave-confirm.stay')),
            ),
            TextButton(
              onPressed: () => Navigator.of(context).pop(true),
              child: Text(t.t('game.leave-confirm.leave')),
            ),
          ],
        ),
      );
      if (confirmed != true || !mounted) return;
    }

    // **Only navigate if the server agreed.** Leaving the screen on a refusal would
    // tell the player they left a room they are still sitting in.
    _leaving = true;
    final left = await vm.leave();
    if (!mounted) return;
    if (!left) {
      _leaving = false;
      return;
    }
    _exit(vm);
  }

  void _exit(GameViewModel vm) {
    final gameKey = vm.room?.gameKey;
    if (gameKey != null && gameKey.isNotEmpty) {
      context.go(lobbyRouteFor(gameKey));
    } else {
      context.go(catalogRoute);
    }
  }

  @override
  Widget build(BuildContext context) {
    final vm = context.watch<GameViewModel>();
    final t = context.read<Translations>();
    final room = vm.room;

    // The result, once, when the game is over.
    //
    // **`game.ended.dismiss` (「重新查看」) is what decides the shape:** a thing you can
    // close and still look at the board behind it — so a dialog, not a permanent banner.
    // Scheduled after the frame because showing a dialog during build is an error.
    final outcome = vm.outcome;
    if (outcome != null && !vm.outcomeDismissed && !_announcingOutcome && !_leaving) {
      _announcingOutcome = true;
      WidgetsBinding.instance.addPostFrameCallback((_) => _announce(vm, outcome));
    }

    // Somebody is waiting on us. **A push, not part of any snapshot** — re-fetching
    // the room would never reveal it. Scheduled after the frame because showing a
    // SnackBar during build is an error.
    if (vm.urgeCount > _urgesShown) {
      _urgesShown = vm.urgeCount;
      WidgetsBinding.instance.addPostFrameCallback((_) {
        if (!mounted) return;
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text(t.t('game.urge.toast'))),
        );
      });
    }

    // **Dissolved rooms are deleted, so no further `RoomState` will ever arrive.**
    // Waiting on this screen would be waiting forever. Navigation is scheduled after
    // the frame because navigating during build is an error.
    if (vm.wasDissolved && !_leaving) {
      _leaving = true;
      WidgetsBinding.instance.addPostFrameCallback((_) {
        if (mounted) _exit(vm);
      });
    }

    return PopScope(
      // **Every exit goes through `_leave`.** `canPop: false` routes the system back
      // gesture and the AppBar's own arrow into the same handler, which is what keeps
      // them from disagreeing — they already did once, before there was a route table.
      canPop: false,
      onPopInvokedWithResult: (didPop, _) {
        if (!didPop) _leave(vm);
      },
      child: Scaffold(
        appBar: AppBar(
          // No `leading:` override. `AppBar` shows a back button exactly when
          // `Navigator.canPop()` is true, so the on-screen arrow and the system back
          // button are now the same mechanism instead of two that can disagree — and
          // before this route table they did: the arrow worked, the system back exited
          // the app.
          title: Text(room?.name ?? ''),
          actions: [
            IconButton(
              tooltip: t.t('game.chat.title'),
              icon: const Icon(Icons.chat_bubble_outline),
              onPressed: () => _openChat(vm),
            ),
          ],
        ),
        body: Column(
          children: [
            Padding(
              padding: const EdgeInsets.all(12),
              child: Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  Text(_statusLabel(t, room?.status)),
                  Text(_turnLabel(t, vm, room)),
                ],
              ),
            ),
            // **The error is drawn OVER the board, not above it.** As a row in this
            // column it changed the column's height, so the board resized by 44 px the
            // moment a move was refused — and resizing the thing you just tapped is
            // worse feedback than the message is good. A `SnackBar` was the other
            // candidate and is wrong here: it dismisses itself, and one of this
            // client's integration tests asserts the refusal is *on screen*.
            Expanded(
              child: Stack(
                children: [
                  Positioned.fill(child: Center(child: _board(context, vm))),
                  if (vm.errorKey != null)
                    Positioned(
                      left: 12,
                      right: 12,
                      top: 0,
                      child: Text(
                        t.t(vm.errorKey!),
                        textAlign: TextAlign.center,
                        style: TextStyle(color: Theme.of(context).colorScheme.error),
                      ),
                    ),
                ],
              ),
            ),
            _actions(context, vm, t),
          ],
        ),
      ),
    );
  }

  /// Opens the conversation.
  ///
  /// **A `ListenableBuilder` around the panel**, because the sheet has its own element
  /// tree: a push that arrives while it is open changes the ViewModel, and without a
  /// listener rebuilding *inside* the sheet the new message would only appear after
  /// closing and reopening it.
  Future<void> _openChat(GameViewModel vm) async {
    final t = context.read<Translations>();
    await showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      builder: (context) => ListenableBuilder(
        listenable: vm,
        builder: (context, _) => ChatPanel(vm: vm, strings: t),
      ),
    );
  }

  /// Resign and urge.
  ///
  /// **Leaving is not here.** It already has one entry — the AppBar's arrow, which is
  /// the same mechanism as the system back button — and a second control that leaves
  /// would be a second thing to keep in step with the confirmation rules.
  ///
  /// Each button appears only when the platform could actually accept it, and the urge
  /// button says **why** when it cannot be pressed: a greyed-out control with no
  /// explanation is not an explanation.
  Widget _actions(BuildContext context, GameViewModel vm, Translations t) {
    if (!vm.canResign && !vm.canUrge) return const SizedBox.shrink();

    final reason = vm.urgeDisabledReasonKey;
    return SafeArea(
      top: false,
      child: Padding(
        padding: const EdgeInsets.fromLTRB(12, 0, 12, 8),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            // **The space is reserved whether or not there is a reason to show.**
            //
            // This line is the flicker somebody reported from a real phone: the reason
            // it carries is 「现在是你的回合」, which is true on your turn and false on
            // your opponent's — so it appeared and vanished on **every single ply**,
            // and the board above it (inside an `Expanded`) resized by 20 px each
            // time. Both games, because it has nothing to do with either renderer.
            //
            // `maintainSize` rather than deleting the line: `add-mobile-game-actions`
            // requires the urge entry to say *why* it cannot be pressed, and a greyed
            // control with no explanation is not an explanation.
            if (vm.canUrge)
              Visibility(
                visible: reason != null,
                maintainSize: true,
                maintainAnimation: true,
                maintainState: true,
                child: Padding(
                  padding: const EdgeInsets.only(bottom: 4),
                  child: Text(
                    t.t(reason ?? 'game.urge.button-disabled-own-turn'),
                    textAlign: TextAlign.center,
                    style: Theme.of(context).textTheme.bodySmall,
                  ),
                ),
              ),
            Row(
              children: [
                if (vm.canUrge)
                  Expanded(
                    child: OutlinedButton.icon(
                      onPressed: reason == null && !vm.sending ? vm.urge : null,
                      icon: const Icon(Icons.notifications_active_outlined),
                      label: Text(t.t('game.actions.urge')),
                    ),
                  ),
                if (vm.canUrge && vm.canResign) const SizedBox(width: 8),
                if (vm.canResign)
                  Expanded(
                    child: OutlinedButton.icon(
                      onPressed: vm.sending ? null : () => _resign(vm),
                      icon: const Icon(Icons.flag_outlined),
                      label: Text(t.t('game.actions.resign')),
                    ),
                  ),
              ],
            ),
          ],
        ),
      ),
    );
  }

  /// The board, or a placeholder while its shape is still unknown.
  ///
  /// **No default size.** The shape comes from the room's own `gameKey` via the
  /// catalogue; guessing 15×15 while that is in flight is exactly how a 10×9 board
  /// gets painted square, and it would look like a rendering bug rather than a missing
  /// fetch.
  Widget _board(BuildContext context, GameViewModel vm) {
    final descriptor = vm.descriptor;
    final renderer = descriptor == null ? null : rendererFor(descriptor.gameKey);

    if (descriptor == null || !descriptor.hasBoard || renderer == null) {
      return const CircularProgressIndicator();
    }

    return Padding(
      padding: const EdgeInsets.all(8),
      child: GameBoard(
        rows: descriptor.rows!,
        cols: descriptor.cols!,
        renderer: renderer,
        moves: vm.moves,
        // From the theme that is painting this screen. **Not from a theme or skin
        // name** — this call site used to pass the literal `defaultThemeName`, so the
        // board stayed one colour under every theme while the token bag said otherwise.
        skin: Theme.of(context).extension<BoardColors>()!.skin,
        selected: vm.selected,
        onTap: vm.tap,
      ),
    );
  }

  /// Whose move it is.
  ///
  /// **A side's name when this game's seats have one, a seat number otherwise** —
  /// dispatched on the game key, never on the seat count. 象棋 and 五子棋 both have two
  /// seats, so a seat-count criterion cannot tell them apart, and the case it was
  /// written for (a game with no 白方) slipped through exactly there.
  String _turnLabel(Translations t, GameViewModel vm, Room? room) {
    final seat = room?.game.currentSeat;
    if (seat == null) return '';

    final labelKey = vm.turnSeatLabelKey;
    if (labelKey != null) {
      return t.t('game.turn.side-turn', {'side': t.t(labelKey)});
    }
    return t.t('game.turn.seat-turn', {'seat': seat + 1});
  }

  String _statusLabel(Translations t, RoomStatus? status) => switch (status) {
    RoomStatus.waiting => t.t('game.room.status-waiting'),
    RoomStatus.playing => t.t('game.room.status-playing'),
    RoomStatus.finished => t.t('game.room.status-finished'),
    _ => '',
  };
}
