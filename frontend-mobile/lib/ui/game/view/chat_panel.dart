import 'package:flutter/material.dart';

import '../../../data/models/models.dart';
import '../../../i18n/translations.dart';
import '../view_model/game_view_model.dart';

/// The room's conversation.
///
/// **A bottom sheet, not a column beside the board.** At 375 px the board already fills
/// the width; a side panel there is either unreadable or squeezes the board, and the
/// board is what the screen is for.
///
/// **The channel tabs appear only for somebody who can reach both channels.** The
/// spectator channel is sent to spectators and only spectators, so a player shown that
/// tab would be looking at a permanently empty one — which looks exactly like a broken
/// one. The criterion is therefore "who can reach this channel", not "does this client
/// support spectating": the second one becomes true for everybody the day spectating
/// ships, and puts the empty tab in front of players.
class ChatPanel extends StatefulWidget {
  const ChatPanel({super.key, required this.vm, required this.strings});

  final GameViewModel vm;
  final Translations strings;

  @override
  State<ChatPanel> createState() => _ChatPanelState();
}

class _ChatPanelState extends State<ChatPanel> {
  final _input = TextEditingController();

  @override
  void dispose() {
    _input.dispose();
    super.dispose();
  }

  Future<void> _send() async {
    final text = _input.text;
    if (text.trim().isEmpty) return;
    await widget.vm.sendChat(text);
    if (!mounted) return;
    // Cleared only after the send returns: a field that empties itself before the
    // server has accepted throws the message away on a refusal.
    if (widget.vm.chatErrorKey == null) _input.clear();
  }

  @override
  Widget build(BuildContext context) {
    final t = widget.strings;
    final vm = widget.vm;
    final channels = vm.chatChannels;
    final messages = vm.chatMessages
        .where((m) => m.channel == vm.chatChannel)
        .toList();

    return Padding(
      padding: EdgeInsets.only(bottom: MediaQuery.of(context).viewInsets.bottom),
      child: SafeArea(
        child: SizedBox(
          height: MediaQuery.of(context).size.height * 0.6,
          child: Column(
            children: [
              ListTile(
                title: Text(t.t('game.chat.title')),
                trailing: IconButton(
                  icon: const Icon(Icons.close),
                  onPressed: () => Navigator.of(context).pop(),
                ),
              ),
              if (channels.length > 1)
                Padding(
                  padding: const EdgeInsets.symmetric(horizontal: 12),
                  child: SegmentedButton<ChatChannel>(
                    segments: [
                      for (final c in channels)
                        ButtonSegment<ChatChannel>(
                          value: c,
                          label: Text(t.t(
                            c == ChatChannel.spectator
                                ? 'game.chat.tab-spectator'
                                : 'game.chat.tab-room',
                          )),
                        ),
                    ],
                    selected: {vm.chatChannel},
                    onSelectionChanged: (s) => vm.chooseChatChannel(s.first),
                  ),
                ),
              const Divider(height: 1),
              Expanded(
                child: messages.isEmpty
                    ? Center(child: Text(t.t('game.chat.empty')))
                    : ListView.builder(
                        reverse: true,
                        itemCount: messages.length,
                        itemBuilder: (context, i) {
                          // Newest at the bottom, which `reverse` gives us by walking
                          // the list backwards.
                          final m = messages[messages.length - 1 - i];
                          return ListTile(
                            dense: true,
                            title: Text(m.senderUsername),
                            subtitle: Text(m.content),
                          );
                        },
                      ),
              ),
              if (vm.chatErrorKey != null)
                Padding(
                  padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 4),
                  child: Text(
                    t.t(vm.chatErrorKey!),
                    style: TextStyle(color: Theme.of(context).colorScheme.error),
                  ),
                ),
              Padding(
                padding: const EdgeInsets.fromLTRB(12, 4, 12, 12),
                child: Row(
                  children: [
                    Expanded(
                      child: TextField(
                        controller: _input,
                        // An input affordance, **not a legality judgement**: the server
                        // decides whether a message is acceptable, and a second copy of
                        // that rule would drift.
                        maxLength: 500,
                        decoration: InputDecoration(
                          hintText: t.t('game.chat.placeholder'),
                          counterText: '',
                          border: const OutlineInputBorder(),
                          isDense: true,
                        ),
                        onSubmitted: (_) => _send(),
                      ),
                    ),
                    const SizedBox(width: 8),
                    FilledButton(
                      onPressed: vm.sendingChat ? null : _send,
                      child: Text(t.t('game.chat.send')),
                    ),
                  ],
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
