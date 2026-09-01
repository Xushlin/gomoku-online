import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../../i18n/translations.dart';
import '../../router.dart';
import '../view_model/catalog_view_model.dart';

/// The game catalogue — the first screen after signing in.
class CatalogView extends StatefulWidget {
  const CatalogView({super.key});

  @override
  State<CatalogView> createState() => _CatalogViewState();
}

class _CatalogViewState extends State<CatalogView> {
  @override
  void initState() {
    super.initState();
    // After the first frame: the ViewModel notifies during load, and notifying while
    // the tree is still building is an error Flutter reports at runtime.
    WidgetsBinding.instance.addPostFrameCallback((_) => _load());
  }

  void _load() {
    if (!mounted) return;
    final t = context.read<Translations>();
    // The ViewModel holds no `Translations` (rule 4), so the "is there copy for this"
    // question is answered here and handed in. `t.t` returns the key itself when it is
    // missing, which is what makes the hole detectable rather than silent.
    context.read<CatalogViewModel>().load(hasCopy: (key) => t.t(key) != key);
  }

  @override
  Widget build(BuildContext context) {
    final vm = context.watch<CatalogViewModel>();
    final t = context.read<Translations>();

    return Scaffold(
      appBar: AppBar(
        title: Text(t.t('catalog.title')),
        actions: [
          IconButton(onPressed: _load, icon: const Icon(Icons.refresh)),
          IconButton(onPressed: vm.signOut, icon: const Icon(Icons.logout)),
        ],
      ),
      body: switch ((vm.loading, vm.errorKey, vm.entries.isEmpty)) {
        (true, _, _) => const Center(child: CircularProgressIndicator()),
        (_, final String key, _) => _Message(text: t.t(key), onRetry: _load),
        // An empty catalogue is not a normal state — the server always returns
        // games — so it reads as a failure rather than getting its own copy.
        (_, _, true) => _Message(text: t.t('lobby.errors.generic'), onRetry: _load),
        _ => ListView.separated(
          padding: const EdgeInsets.all(12),
          itemCount: vm.entries.length,
          separatorBuilder: (_, _) => const SizedBox(height: 8),
          itemBuilder: (context, index) {
            final entry = vm.entries[index];
            return _GameCard(
              title: t.t(entry.titleKey),
              description: t.t(entry.descriptionKey),
              rated: entry.descriptor.isRated,
              playable: entry.playable,
              comingSoon: t.t('catalog.coming-soon'),
              onTap: entry.playable ? () => context.go(lobbyRouteFor(entry.gameKey)) : null,
            );
          },
        ),
      },
    );
  }
}

class _GameCard extends StatelessWidget {
  const _GameCard({
    required this.title,
    required this.description,
    required this.rated,
    required this.playable,
    required this.comingSoon,
    this.onTap,
  });

  final String title;
  final String description;
  final bool rated;
  final bool playable;
  final String comingSoon;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;

    return Card(
      child: Opacity(
        // Disabled, not hidden: the platform has more games than this client can draw,
        // and hiding them would be a client-side filter pretending the server said so.
        opacity: playable ? 1 : 0.55,
        child: ListTile(
          enabled: playable,
          onTap: onTap,
          title: Text(title),
          subtitle: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              const SizedBox(height: 4),
              Text(description),
              const SizedBox(height: 6),
              Wrap(
                spacing: 6,
                runSpacing: 4,
                children: [
                  // No seat-count chip: there is no copy for "N seats" in the shared
                  // i18n artefact, and inventing a mobile-only key would break
                  // `shared_sync_test`. `seatCount` is still parsed — 斗地主 and 挖坑
                  // need it — it just has nothing to render into yet.
                  if (rated) _Chip(label: '★'),
                  if (!playable) _Chip(label: comingSoon, tone: scheme.tertiary),
                ],
              ),
            ],
          ),
          trailing: playable ? const Icon(Icons.chevron_right) : null,
        ),
      ),
    );
  }
}

class _Chip extends StatelessWidget {
  const _Chip({required this.label, this.tone});

  final String label;
  final Color? tone;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
      decoration: BoxDecoration(
        color: (tone ?? scheme.secondary).withValues(alpha: 0.16),
        borderRadius: BorderRadius.circular(10),
      ),
      child: Text(label, style: Theme.of(context).textTheme.labelSmall),
    );
  }
}

class _Message extends StatelessWidget {
  const _Message({required this.text, required this.onRetry});

  final String text;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) => Center(
    child: Column(
      mainAxisAlignment: MainAxisAlignment.center,
      children: [
        Text(text, textAlign: TextAlign.center),
        const SizedBox(height: 8),
        TextButton(onPressed: onRetry, child: const Text('↻')),
      ],
    ),
  );
}
