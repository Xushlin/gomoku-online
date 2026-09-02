import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../../data/models/models.dart';
import '../../../i18n/translations.dart';
import '../../router.dart';
import '../view_model/lobby_view_model.dart';

class LobbyView extends StatefulWidget {
  const LobbyView({super.key});

  @override
  State<LobbyView> createState() => _LobbyViewState();
}

class _LobbyViewState extends State<LobbyView> {
  @override
  void initState() {
    super.initState();
    // After the first frame: the ViewModel notifies during load, and notifying while
    // the tree is still building is an error Flutter reports at runtime.
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted) context.read<LobbyViewModel>().load();
    });
  }

  Future<void> _open(Future<String?> Function() action) async {
    final id = await action();
    // `go` and not `push`: the location is the truth, and `rooms/:id` is a child of
    // `/`, so go_router puts the lobby underneath. That stack is what makes the
    // system back button work.
    if (id != null && mounted) {
      context.go(roomRouteFor(context.read<LobbyViewModel>().gameKey, id));
    }
  }


  /// Picks a difficulty and a side, then creates the room.
  ///
  /// `showDialog` rather than a hand-rolled overlay: focus trapping, the barrier and
  /// back-button handling come with it.
  Future<void> _openAiDialog(LobbyViewModel vm) async {
    final t = context.read<Translations>();
    var difficulty = 'Medium';
    var side = 'Black';

    final start = await showDialog<bool>(
      context: context,
      builder: (context) => StatefulBuilder(
        builder: (context, setDialogState) => AlertDialog(
          title: Text(t.t('lobby.ai-game.dialog-title')),
          content: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(t.t('lobby.ai-game.difficulty-label')),
              const SizedBox(height: 6),
              // The three spellings are the server's own enum names.
              SegmentedButton<String>(
                segments: [
                  ButtonSegment(
                    value: 'Easy',
                    label: Text(t.t('lobby.ai-game.difficulty-easy')),
                  ),
                  ButtonSegment(
                    value: 'Medium',
                    label: Text(t.t('lobby.ai-game.difficulty-medium')),
                  ),
                  ButtonSegment(
                    value: 'Hard',
                    label: Text(t.t('lobby.ai-game.difficulty-hard')),
                  ),
                ],
                selected: {difficulty},
                onSelectionChanged: (v) => setDialogState(() => difficulty = v.first),
              ),
              const SizedBox(height: 16),
              Text(t.t('lobby.ai-game.side-label')),
              const SizedBox(height: 6),
              SegmentedButton<String>(
                segments: [
                  ButtonSegment(
                    value: 'Black',
                    label: Text(t.t('lobby.ai-game.side-black')),
                  ),
                  ButtonSegment(
                    value: 'White',
                    label: Text(t.t('lobby.ai-game.side-white')),
                  ),
                ],
                selected: {side},
                onSelectionChanged: (v) => setDialogState(() => side = v.first),
              ),
            ],
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.of(context).pop(false),
              child: Text(t.t('lobby.ai-game.cancel')),
            ),
            TextButton(
              onPressed: () => Navigator.of(context).pop(true),
              child: Text(t.t('lobby.ai-game.submit')),
            ),
          ],
        ),
      ),
    );

    if (start != true || !mounted) return;
    await _open(() => vm.createAiRoom(difficulty: difficulty, humanSide: side));
  }

  @override
  Widget build(BuildContext context) {
    final vm = context.watch<LobbyViewModel>();
    final t = context.read<Translations>();

    return Scaffold(
      appBar: AppBar(
        // The game's own title, from the shared i18n artefact. It used to be
        // hard-coded to 五子棋 because there was only one game.
        title: Text(t.t('games.${vm.gameKey}.title')),
        actions: [
          IconButton(onPressed: vm.load, icon: const Icon(Icons.refresh)),
        ],
      ),
      // **Both buttons are derived, and both are labelled.** `heroTag` has to differ:
      // two `FloatingActionButton`s sharing the default tag throw at runtime, and the
      // failure is a Hero conflict that reads nothing like "you have two FABs".
      floatingActionButton: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.end,
        children: [
          if (vm.canPlayAi)
            FloatingActionButton.extended(
              heroTag: 'ai-game',
              onPressed: () => _openAiDialog(vm),
              icon: const Icon(Icons.smart_toy_outlined),
              label: Text(t.t('lobby.ai-game.button')),
            ),
          if (vm.canPlayAi && vm.canCreateRoom) const SizedBox(height: 12),
          if (vm.canCreateRoom)
            FloatingActionButton.extended(
              heroTag: 'create-room',
              onPressed: () => _open(vm.create),
              icon: const Icon(Icons.add),
              label: Text(t.t('lobby.rooms.create-button')),
            ),
        ],
      ),
      body: RefreshIndicator(
        onRefresh: vm.load,
        child: switch ((vm.loading, vm.errorKey, vm.rooms.isEmpty)) {
          // A game with no human-vs-human mode has no room list to show, and offering
          // one would be offering a button the server answers 400 to.
          (false, null, _) when !vm.canCreateRoom => _AiOnly(
            title: t.t('lobby.game-lobby.unavailable.ai-only-title'),
            body: t.t('lobby.game-lobby.unavailable.ai-only-body'),
          ),
          (true, _, _) => const Center(child: CircularProgressIndicator()),
          (_, final String key, _) => _Message(text: t.t(key), onRetry: vm.load),
          (_, _, true) => _Message(text: t.t('lobby.rooms.empty'), onRetry: vm.load),
          _ => ListView.separated(
            itemCount: vm.rooms.length,
            separatorBuilder: (_, _) => const Divider(height: 1),
            itemBuilder: (context, index) => _RoomTile(
              room: vm.rooms[index],
              onTap: () => _open(() => vm.join(vm.rooms[index].id)),
            ),
          ),
        },
      ),
    );
  }
}

class _RoomTile extends StatelessWidget {
  const _RoomTile({required this.room, required this.onTap});

  final Room room;
  final VoidCallback onTap;

  /// **`status.wire` is the on-the-wire value, not something to show a person.**
  /// This tile used to print it, so a Chinese UI said `Playing` — visible on the very
  /// first real device it ran on, and invisible to every test, because "shows a raw
  /// English enum" is not a missing key.
  static String _statusKey(RoomStatus status) => switch (status) {
    RoomStatus.waiting => 'lobby.rooms.status-waiting',
    RoomStatus.playing => 'lobby.rooms.status-playing',
    RoomStatus.finished => 'lobby.rooms.status-finished',
    RoomStatus.unknown => 'lobby.rooms.status-waiting',
  };

  @override
  Widget build(BuildContext context) {
    final t = context.read<Translations>();
    return ListTile(
      title: Text(room.name),
      // `totalSeats` is how many seats the game HAS; `takenSeats` is how many are
      // filled. The web client conflated those in five places.
      subtitle: Text(
        '${t.t(_statusKey(room.status))} · ${room.takenSeats}/${room.totalSeats}',
      ),
      trailing: const Icon(Icons.chevron_right),
      onTap: onTap,
    );
  }
}

class _Message extends StatelessWidget {
  const _Message({required this.text, required this.onRetry});

  final String text;
  final Future<void> Function() onRetry;

  @override
  Widget build(BuildContext context) {
    // Inside a RefreshIndicator the child must scroll, or pull-to-refresh cannot
    // start — an empty state that silently disables the gesture is worse than none.
    return ListView(
      children: [
        const SizedBox(height: 80),
        Center(child: Text(text)),
        const SizedBox(height: 12),
        Center(
          child: TextButton(
            onPressed: onRetry,
            // A labelled button, not a bare glyph: `↻` says nothing to somebody who has
            // not seen the code, and the copy for it was already in the bundle.
            child: Text(context.read<Translations>().t('lobby.errors.retry')),
          ),
        ),
      ],
    );
  }
}

/// A game the platform only offers against the machine.
class _AiOnly extends StatelessWidget {
  const _AiOnly({required this.title, required this.body});

  final String title;
  final String body;

  @override
  Widget build(BuildContext context) {
    // Inside a RefreshIndicator the child must scroll, or pull-to-refresh cannot start.
    return ListView(
      padding: const EdgeInsets.all(24),
      children: [
        const SizedBox(height: 60),
        Text(title, style: Theme.of(context).textTheme.titleMedium, textAlign: TextAlign.center),
        const SizedBox(height: 12),
        Text(body, textAlign: TextAlign.center),
      ],
    );
  }
}
