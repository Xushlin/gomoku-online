import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../i18n/translations.dart';
import '../view_model/settings_view_model.dart';

/// Theme, dark mode, and the way out.
class SettingsView extends StatelessWidget {
  const SettingsView({super.key});

  @override
  Widget build(BuildContext context) {
    final vm = context.watch<SettingsViewModel>();
    final t = context.read<Translations>();

    return Scaffold(
      appBar: AppBar(title: Text(t.t('header.settings.label'))),
      body: ListView(
        padding: const EdgeInsets.symmetric(vertical: 8),
        children: [
          _SectionLabel(text: t.t('header.theme.label')),
          // **One row per theme in the synced artefact.** Nothing here names them.
          RadioGroup<String>(
            groupValue: vm.themeName,
            onChanged: (chosen) => chosen == null ? null : vm.chooseTheme(chosen),
            child: Column(
              children: [
                for (final name in vm.themes)
                  RadioListTile<String>(
                    value: name,
                    title: Text(t.t(vm.themeLabelKey(name))),
                  ),
              ],
            ),
          ),
          const Divider(height: 24),
          SwitchListTile(
            value: vm.isDark,
            onChanged: vm.setDark,
            title: Text(t.t('header.theme.dark-toggle')),
            subtitle: Text(
              t.t(vm.isDark ? 'header.theme.dark-on' : 'header.theme.dark-off'),
            ),
          ),
          const Divider(height: 24),
          _SectionLabel(text: t.t('header.language.label')),
          // **One row per locale file that ships.** Nothing here names them.
          RadioGroup<String>(
            groupValue: vm.locale,
            onChanged: (chosen) => chosen == null ? null : vm.chooseLocale(chosen),
            child: Column(
              children: [
                for (final locale in vm.locales)
                  RadioListTile<String>(
                    value: locale,
                    title: Text(t.t(vm.localeLabelKey(locale))),
                  ),
              ],
            ),
          ),
          const Divider(height: 24),
          _SectionLabel(text: t.t('header.board-skin.label')),
          // **One row per skin in the synced artefact.** Nothing here names them —
          // the same rule as the themes above, and for the same reason.
          RadioGroup<String>(
            groupValue: vm.skinName,
            onChanged: (chosen) => chosen == null ? null : vm.chooseSkin(chosen),
            child: Column(
              children: [
                for (final name in vm.skins)
                  RadioListTile<String>(
                    value: name,
                    title: Text(t.t(vm.skinLabelKey(name))),
                  ),
              ],
            ),
          ),
          const Divider(height: 24),
          SwitchListTile(
            value: vm.soundOn,
            onChanged: vm.setSoundOn,
            title: Text(t.t('header.sound.label')),
            subtitle: Text(t.t(vm.soundOn ? 'header.sound.on' : 'header.sound.off')),
          ),
          const Divider(height: 24),
          ListTile(
            leading: const Icon(Icons.logout),
            title: Text(t.t('header.auth.logout')),
            onTap: () => confirmSignOut(context, vm.signOut),
          ),
        ],
      ),
    );
  }
}

/// Asks first, then signs out.
///
/// **Shared by every entry point on purpose.** Two copies of this rule would diverge,
/// and the way divergence shows up is one path quietly stopping asking.
///
/// The copy is assembled from keys that already exist — a mobile-only key would make
/// `shared_sync_test` red, and that walk exists precisely to forbid a second set of
/// translations.
Future<void> confirmSignOut(BuildContext context, Future<void> Function() signOut) async {
  final t = context.read<Translations>();
  final confirmed = await showDialog<bool>(
    context: context,
    builder: (context) => AlertDialog(
      title: Text(t.t('header.auth.logout')),
      actions: [
        TextButton(
          onPressed: () => Navigator.of(context).pop(false),
          child: Text(t.t('lobby.ai-game.cancel')),
        ),
        TextButton(
          onPressed: () => Navigator.of(context).pop(true),
          child: Text(t.t('header.auth.logout')),
        ),
      ],
    ),
  );
  if (confirmed != true) return;

  // No navigation here: signing out flips `AuthRepository.signedIn`, and the router's
  // `redirect` takes it from there. A `context.go` as well would be a second answer to
  // the same question.
  await signOut();
}

class _SectionLabel extends StatelessWidget {
  const _SectionLabel({required this.text});

  final String text;

  @override
  Widget build(BuildContext context) => Padding(
    padding: const EdgeInsets.fromLTRB(16, 8, 16, 4),
    child: Text(text, style: Theme.of(context).textTheme.labelLarge),
  );
}
