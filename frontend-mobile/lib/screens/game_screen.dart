import 'package:flutter/material.dart';

import '../api/api_client.dart';
import '../app.dart';
import '../hub/match_hub.dart';
import '../theme/app_theme.dart';
import '../widgets/gomoku_board.dart';

class GameScreen extends StatefulWidget {
  const GameScreen({
    super.key,
    required this.services,
    required this.roomId,
    required this.onLeave,
  });

  final AppServices services;
  final String roomId;
  final VoidCallback onLeave;

  @override
  State<GameScreen> createState() => _GameScreenState();
}

class _GameScreenState extends State<GameScreen> {
  MatchHub? _hub;
  Map<String, dynamic>? _rest;
  String? _error;
  bool _sending = false;

  @override
  void initState() {
    super.initState();
    _open();
  }

  String _t(String key, [Map<String, Object?> params = const {}]) =>
      widget.services.strings.t(key, params);

  Future<void> _open() async {
    try {
      // REST first: the snapshot is the authoritative recovery source, and the hub
      // only pushes *changes*. Relying on the hub alone leaves an empty board until
      // somebody moves.
      _rest = await widget.services.api.get('/api/rooms/${widget.roomId}')
          as Map<String, dynamic>?;

      final hub = MatchHub(
        serverAddress: widget.services.api.baseUrl,
        accessToken: () => widget.services.tokens.access ?? '',
      );
      await hub.joinRoom(widget.roomId);
      hub.state.addListener(_onPush);
      if (!mounted) return;
      setState(() => _hub = hub);
    } on ApiException catch (e) {
      setState(() => _error = '${_t('game.errors.generic')} (${e.code})');
    } catch (e) {
      setState(() => _error = _t('game.errors.network'));
    }
  }

  void _onPush() {
    final pushed = _hub?.state.value;
    if (pushed != null && mounted) setState(() => _rest = pushed.raw);
  }

  @override
  void dispose() {
    _hub?.state.removeListener(_onPush);
    _hub?.dispose();
    super.dispose();
  }

  /// Sends the move. **No legality check here** — the server owns that (design D2).
  Future<void> _place(int row, int col) async {
    if (_sending || _hub == null) return;
    setState(() => _sending = true);
    try {
      await _hub!.makeMove(widget.roomId, row, col);
    } catch (e) {
      // The hub delivers the server's code inside the exception text; the mapping
      // table is the web client's `hub-error.mapper`, which this slice does not
      // duplicate — it shows the generic string plus the raw code, so the reason is
      // never invented.
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('${_t('game.errors.invalid-move')} — $e')),
        );
      }
    } finally {
      if (mounted) setState(() => _sending = false);
    }
  }

  List<List<int>> get _stones {
    final moves = ((_rest?['game'] as Map<String, dynamic>?)?['moves'] as List<dynamic>?) ??
        const [];
    return [
      for (final move in moves)
        [
          (move as Map<String, dynamic>)['row'] as int,
          move['col'] as int,
          move['seat'] as int? ?? 0,
        ],
    ];
  }

  @override
  Widget build(BuildContext context) {
    final status = '${_rest?['status'] ?? ''}';
    final currentSeat = (_rest?['game'] as Map<String, dynamic>?)?['currentSeat'];

    return Scaffold(
      appBar: AppBar(
        title: Text('${_rest?['name'] ?? _t('games.gomoku.title')}'),
        leading: IconButton(icon: const Icon(Icons.arrow_back), onPressed: widget.onLeave),
      ),
      body: Column(
        children: [
          Padding(
            padding: const EdgeInsets.all(12),
            child: Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Text(_statusLabel(status)),
                Text(
                  currentSeat == null
                      ? ''
                      : _t('game.turn.seat-turn', {'seat': (currentSeat as int) + 1}),
                ),
              ],
            ),
          ),
          if (_error != null)
            Padding(
              padding: const EdgeInsets.all(12),
              child: Text(_error!, style: TextStyle(color: Theme.of(context).colorScheme.error)),
            ),
          Expanded(
            child: Center(
              child: Padding(
                padding: const EdgeInsets.all(8),
                child: GomokuBoard(
                  stones: _stones,
                  background: AppTheme.boardBackground(
                    defaultThemeName,
                    Theme.of(context).brightness,
                  ),
                  onTap: _place,
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }

  String _statusLabel(String status) => switch (status) {
    'Waiting' => _t('game.room.status-waiting'),
    'Playing' => _t('game.room.status-playing'),
    'Finished' => _t('game.room.status-finished'),
    _ => status,
  };
}
