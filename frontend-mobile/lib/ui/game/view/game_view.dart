import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../../data/models/models.dart';
import '../../../i18n/translations.dart';
import '../../../theme/app_theme.dart';
import '../view_model/game_view_model.dart';
import '../../router.dart';
import '../board_registry.dart';
import 'game_board.dart';

class GameView extends StatefulWidget {
  const GameView({super.key});

  @override
  State<GameView> createState() => _GameViewState();
}

class _GameViewState extends State<GameView> {
  /// Set once we are on our way out, so neither exit path fires twice.
  bool _leaving = false;

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
            if (vm.errorKey != null)
              Padding(
                padding: const EdgeInsets.all(12),
                child: Text(
                  t.t(vm.errorKey!),
                  style: TextStyle(color: Theme.of(context).colorScheme.error),
                ),
              ),
            Expanded(child: Center(child: _board(context, vm))),
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
        background: AppTheme.boardBackground(
          defaultThemeName,
          Theme.of(context).brightness,
        ),
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
