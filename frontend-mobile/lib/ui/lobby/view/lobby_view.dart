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
    if (id != null && mounted) context.go(roomRoute(id));
  }

  @override
  Widget build(BuildContext context) {
    final vm = context.watch<LobbyViewModel>();
    final t = context.read<Translations>();

    return Scaffold(
      appBar: AppBar(
        title: Text(t.t('games.gomoku.title')),
        actions: [
          IconButton(onPressed: vm.load, icon: const Icon(Icons.refresh)),
          IconButton(onPressed: vm.signOut, icon: const Icon(Icons.logout)),
        ],
      ),
      floatingActionButton: FloatingActionButton.extended(
        onPressed: () => _open(vm.create),
        icon: const Icon(Icons.add),
        label: Text(t.t('lobby.create.submit')),
      ),
      body: RefreshIndicator(
        onRefresh: vm.load,
        child: switch ((vm.loading, vm.errorKey, vm.rooms.isEmpty)) {
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

  @override
  Widget build(BuildContext context) => ListTile(
    title: Text(room.name),
    // `totalSeats` is how many seats the game HAS; `takenSeats` is how many are
    // filled. The web client conflated those in five places.
    subtitle: Text('${room.status.wire} · ${room.takenSeats}/${room.totalSeats}'),
    trailing: const Icon(Icons.chevron_right),
    onTap: onTap,
  );
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
        Center(child: TextButton(onPressed: onRetry, child: const Text('↻'))),
      ],
    );
  }
}
