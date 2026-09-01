import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../data/models/models.dart';
import '../../../i18n/translations.dart';
import '../../../theme/app_theme.dart';
import '../view_model/game_view_model.dart';
import 'gomoku_board.dart';

class GameView extends StatefulWidget {
  const GameView({super.key});

  @override
  State<GameView> createState() => _GameViewState();
}

class _GameViewState extends State<GameView> {
  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted) context.read<GameViewModel>().open();
    });
  }

  @override
  Widget build(BuildContext context) {
    final vm = context.watch<GameViewModel>();
    final t = context.read<Translations>();
    final room = vm.room;

    return Scaffold(
      appBar: AppBar(
        // No `leading:` override. `AppBar` shows a back button exactly when
        // `Navigator.canPop()` is true, so the on-screen arrow and the system back
        // button are now the same mechanism instead of two that can disagree — and
        // before this route table they did: the arrow worked, the system back exited
        // the app.
        title: Text(room?.name ?? t.t('games.gomoku.title')),
      ),
      body: Column(
        children: [
          Padding(
            padding: const EdgeInsets.all(12),
            child: Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Text(_statusLabel(t, room?.status)),
                Text(
                  room?.game.currentSeat == null
                      ? ''
                      : t.t('game.turn.seat-turn', {'seat': room!.game.currentSeat! + 1}),
                ),
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
          Expanded(
            child: Center(
              child: Padding(
                padding: const EdgeInsets.all(8),
                child: GomokuBoard(
                  stones: [
                    for (final m in vm.moves) [m.row, m.col, m.seat],
                  ],
                  background: AppTheme.boardBackground(
                    defaultThemeName,
                    Theme.of(context).brightness,
                  ),
                  onTap: vm.place,
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }

  String _statusLabel(Translations t, RoomStatus? status) => switch (status) {
    RoomStatus.waiting => t.t('game.room.status-waiting'),
    RoomStatus.playing => t.t('game.room.status-playing'),
    RoomStatus.finished => t.t('game.room.status-finished'),
    _ => '',
  };
}
