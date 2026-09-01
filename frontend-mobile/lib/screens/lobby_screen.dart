import 'package:flutter/material.dart';

import '../api/api_client.dart';
import '../app.dart';

/// Gomoku's lobby. One game this slice — the other nine are a declared non-goal,
/// and a game picker with one entry would be a picker pretending to be a platform.
const gameKey = 'gomoku';

class LobbyScreen extends StatefulWidget {
  const LobbyScreen({
    super.key,
    required this.services,
    required this.onOpenRoom,
    required this.onSignedOut,
  });

  final AppServices services;
  final void Function(String roomId) onOpenRoom;
  final VoidCallback onSignedOut;

  @override
  State<LobbyScreen> createState() => _LobbyScreenState();
}

class _LobbyScreenState extends State<LobbyScreen> {
  List<dynamic> _rooms = const [];
  bool _loading = true;
  String? _error;

  @override
  void initState() {
    super.initState();
    _load();
  }

  String _t(String key, [Map<String, Object?> params = const {}]) =>
      widget.services.strings.t(key, params);

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final result = await widget.services.api.get('/api/rooms?gameKey=$gameKey');
      setState(() => _rooms = (result as List<dynamic>?) ?? const []);
    } on ApiException {
      setState(() => _error = _t('lobby.errors.load-failed'));
    } catch (_) {
      setState(() => _error = _t('auth.errors.network'));
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  Future<void> _create() async {
    try {
      final room = await widget.services.api.post('/api/rooms', {
        'name': '${widget.services.username ?? 'mobile'}-${DateTime.now().minute}',
        'gameKey': gameKey,
      });
      widget.onOpenRoom((room as Map<String, dynamic>)['id'] as String);
    } on ApiException catch (e) {
      _snack('${_t('lobby.errors.create-failed')} (${e.code})');
    }
  }

  Future<void> _join(String roomId) async {
    try {
      await widget.services.api.post('/api/rooms/$roomId/join');
    } on ApiException catch (e) {
      // Already seated is not a failure worth blocking on — opening the room is
      // still the right outcome, and the server is the one that decides seats.
      if (e.status != 409) {
        _snack('${_t('lobby.errors.join-failed')} (${e.code})');
        return;
      }
    }
    widget.onOpenRoom(roomId);
  }

  void _snack(String message) {
    if (!mounted) return;
    ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(message)));
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: Text(_t('games.gomoku.title')),
        actions: [
          IconButton(onPressed: _load, icon: const Icon(Icons.refresh)),
          IconButton(onPressed: widget.onSignedOut, icon: const Icon(Icons.logout)),
        ],
      ),
      floatingActionButton: FloatingActionButton.extended(
        onPressed: _create,
        icon: const Icon(Icons.add),
        label: Text(_t('lobby.create.submit')),
      ),
      body: RefreshIndicator(
        onRefresh: _load,
        child: switch ((_loading, _error, _rooms.isEmpty)) {
          (true, _, _) => const Center(child: CircularProgressIndicator()),
          (_, final String error, _) => _Message(text: error, onRetry: _load),
          (_, _, true) => _Message(text: _t('lobby.rooms.empty'), onRetry: _load),
          _ => ListView.separated(
            itemCount: _rooms.length,
            separatorBuilder: (_, _) => const Divider(height: 1),
            itemBuilder: (context, index) {
              final room = _rooms[index] as Map<String, dynamic>;
              final seats = (room['seats'] as List<dynamic>?) ?? const [];
              final taken = seats.where((s) => (s as Map)['player'] != null).length;
              return ListTile(
                title: Text('${room['name']}'),
                subtitle: Text(
                  '${room['status']} · $taken/${room['seatCount'] ?? seats.length}',
                ),
                trailing: const Icon(Icons.chevron_right),
                onTap: () => _join(room['id'] as String),
              );
            },
          ),
        },
      ),
    );
  }
}

class _Message extends StatelessWidget {
  const _Message({required this.text, required this.onRetry});

  final String text;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    // Inside a RefreshIndicator the child must scroll, or pull-to-refresh cannot
    // start — an empty state that silently disables the gesture is worse than none.
    return ListView(
      children: [
        const SizedBox(height: 80),
        Center(child: Text(text)),
        const SizedBox(height: 12),
        Center(child: TextButton(onPressed: onRetry, child: Text('↻'))),
      ],
    );
  }
}
