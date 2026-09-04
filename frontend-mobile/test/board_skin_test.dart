// Board skins: a fourth axis, and the first one whose values come from a CSS file.
//
// **The criterion is the painted board, not the stored string.** `add-mobile-settings`
// shipped a theme that was stored perfectly and painted nowhere, because every
// assertion asked the token bag instead of the screen. So every claim here that matters
// is checked by rendering and reading pixels back.
import 'dart:convert';
import 'dart:io';
import 'dart:ui' as ui;

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:gewu_mobile/data/repositories/settings_repository.dart';
import 'package:gewu_mobile/data/services/preferences_store.dart';
import 'package:gewu_mobile/theme/app_theme.dart';
import 'package:gewu_mobile/theme/board_skin.dart';
import 'package:gewu_mobile/ui/game/board_registry.dart';
import 'package:gewu_mobile/ui/game/view/board_geometry.dart';

Map<String, String> flatten(Map<String, dynamic> json, [String prefix = '']) {
  final out = <String, String>{};
  json.forEach((key, value) {
    final path = prefix.isEmpty ? key : '$prefix.$key';
    if (value is Map<String, dynamic>) {
      out.addAll(flatten(value, path));
    } else {
      out[path] = '$value';
    }
  });
  return out;
}

/// Paints a whole 五子棋 board with [skin] and returns a pixel reader.
Future<Color Function(int, int)> paintBoard(BoardSkin skin, {int size = 240}) async {
  final recorder = ui.PictureRecorder();
  final canvas = Canvas(recorder);
  final g = BoardGeometry.fit(
    rows: 15,
    cols: 15,
    canvas: Size(size.toDouble(), size.toDouble()),
  );
  final renderer = rendererFor(gomokuGameKey)!;
  skin.paintGround(canvas, Rect.fromLTWH(g.originDx, g.originDy, g.width, g.height));
  renderer.paintDecoration(canvas, g, skin);
  renderer.paintOccupants(canvas, g, const [], null, skin);

  final image = await recorder.endRecording().toImage(size, size);
  final data = (await image.toByteData(format: ui.ImageByteFormat.rawRgba))!;
  return (int x, int y) {
    final i = (y * size + x) * 4;
    return Color.fromARGB(
      data.getUint8(i + 3),
      data.getUint8(i),
      data.getUint8(i + 1),
      data.getUint8(i + 2),
    );
  };
}

BoardSkin skin(String name, {String theme = defaultThemeName, Brightness b = Brightness.dark}) =>
    BoardSkin.resolve(skinName: name, themeName: theme, brightness: b);

void main() {
  group('the skin list is derived from the artefact', () {
    test('every skin has a name, in both locales', () {
      // Derived from `skinTokens.keys`: the day the web side adds a fourth skin, this
      // goes red rather than the settings page showing a raw key.
      final skins = BoardSkin.available;
      expect(skins, isNotEmpty, reason: 'an empty walk asserts nothing');
      expect(skins.length, greaterThan(1), reason: 'a one-item radio group is not a choice');
      expect(skins, contains(BoardSkin.defaultSkinName));

      for (final locale in const ['zh-CN', 'en']) {
        final copy = flatten(
          jsonDecode(File('assets/i18n/$locale.json').readAsStringSync())
              as Map<String, dynamic>,
        );
        expect(
          [for (final s in skins) if (!copy.containsKey('header.board-skin.$s')) s],
          equals(<String>[]),
          reason: locale,
        );
      }
    });

    test('and it is sorted, so the rows do not reorder between launches', () {
      expect(BoardSkin.available, orderedEquals(List.of(BoardSkin.available)..sort()));
    });
  });

  group('the three skins paint three different boards', () {
    test('their grounds differ, measured off the canvas', () async {
      // **Rendered, not resolved.** Comparing `skin.background` would pass an
      // implementation that resolves three colours and paints one.
      //
      // Positive control: make `paintGround` ignore `backgroundLayers` and use one
      // fixed colour, and this goes red.
      final samples = <Color>{};
      for (final name in BoardSkin.available) {
        final at = await paintBoard(skin(name));
        samples.add(at(60, 50));
      }
      expect(samples, hasLength(BoardSkin.available.length));
    });

    test('and so do their stones', () async {
      // The stones used to be two literals in the renderer; if they still were, every
      // skin would paint the same two circles over three different grounds.
      Future<Color> stoneOf(String name) async {
        final s = skin(name);
        final recorder = ui.PictureRecorder();
        final canvas = Canvas(recorder);
        const box = Rect.fromLTWH(0, 0, 40, 40);
        canvas.drawRect(box, s.stonePaint(box, black: false));
        final image = await recorder.endRecording().toImage(40, 40);
        final data = (await image.toByteData(format: ui.ImageByteFormat.rawRgba))!;
        return Color.fromARGB(
          data.getUint8(3),
          data.getUint8(0),
          data.getUint8(1),
          data.getUint8(2),
        );
      }

      final stones = <Color>{};
      for (final name in BoardSkin.available) {
        stones.add(await stoneOf(name));
      }
      expect(stones, hasLength(BoardSkin.available.length));
    });

    test('only the theme-following skin follows the theme', () async {
      // **Both directions.** `classic` is written in `var(--color-*)`, so it must change
      // with the theme; `wood` is written in literals, so it must NOT. Asserting only
      // the first would pass an implementation that ignores the skin and always uses
      // the theme — which is what this client did before this change.
      final classicInk = (await paintBoard(skin('classic', theme: 'ink')))(60, 50);
      final classicQq = (await paintBoard(skin('classic', theme: 'qq-game')))(60, 50);
      expect(classicInk, isNot(classicQq), reason: 'classic follows the theme');

      final woodInk = (await paintBoard(skin('wood', theme: 'ink')))(60, 50);
      final woodQq = (await paintBoard(skin('wood', theme: 'qq-game')))(60, 50);
      expect(woodInk, woodQq, reason: 'wood is written in literals and must not');
    });
  });

  group('the renderers own no colours', () {
    test('no Color(0x…) survives in any renderer', () {
      // **Code only, not prose** — the same rule `layering_test.dart` had to learn: a
      // checker that fires on the comment explaining the rule is worse than none.
      final offenders = <String>[];
      for (final entity in Directory('lib/ui/game/view').listSync()) {
        if (entity is! File || !entity.path.endsWith('renderer.dart')) continue;
        final code = entity.readAsLinesSync().where((l) {
          final t = l.trimLeft();
          return !t.startsWith('//') && !t.startsWith('*') && !t.startsWith('/*');
        }).join(' ');
        if (RegExp(r'Color\(0x').hasMatch(code)) offenders.add(entity.path);
      }
      expect(offenders, equals(<String>[]));

      // Non-vacuity: the walk found the renderers at all.
      final scanned = Directory('lib/ui/game/view')
          .listSync()
          .whereType<File>()
          .where((f) => f.path.endsWith('renderer.dart'));
      expect(scanned, hasLength(greaterThanOrEqualTo(2)));
    });
  });

  group('the fourth axis', () {
    test('choosing a skin disturbs nothing else', () async {
      final settings = SettingsRepository(MemoryPreferencesStore());
      await settings.setDark(false);
      await settings.setSoundOn(false);
      await settings.setTheme('material');

      await settings.setSkin('midnight');
      final now = settings.current.value;
      expect(now.skinName, 'midnight');
      expect(now.isDark, isFalse, reason: 'brightness untouched');
      expect(now.soundOn, isFalse, reason: 'sound untouched');
      expect(now.themeName, 'material', reason: 'theme untouched');
    });

    test('and nothing else disturbs it', () async {
      final settings = SettingsRepository(MemoryPreferencesStore());
      await settings.setSkin('midnight');
      await settings.setTheme('material');
      await settings.setDark(true);
      await settings.setSoundOn(true);
      expect(settings.current.value.skinName, 'midnight');
    });

    test('it survives a restart, and an unknown one falls back', () async {
      final store = MemoryPreferencesStore();
      await SettingsRepository(store).setSkin('midnight');
      expect(SettingsRepository(store).current.value.skinName, 'midnight');

      // Skins come from web; one being deleted there is real, and the phone would
      // still have the old name written down.
      store.values['gewu.skin'] = 'a-skin-from-2024';
      expect(
        SettingsRepository(store).current.value.skinName,
        BoardSkin.defaultSkinName,
      );
    });

    test('an unknown skin is refused rather than stored', () async {
      final store = MemoryPreferencesStore();
      final settings = SettingsRepository(store);
      await settings.setSkin('midnight');
      await settings.setSkin('not-a-skin');
      expect(settings.current.value.skinName, 'midnight');
      expect(store.values['gewu.skin'], 'midnight');
    });
  });

  group('the skin reaches the painted ThemeData', () {
    test('a different skin gives a different BoardColors', () {
      // The same shape as the theme's own regression: stored perfectly, painted never.
      final wood = AppTheme.build(defaultThemeName, Brightness.dark, skinName: 'wood');
      final midnight =
          AppTheme.build(defaultThemeName, Brightness.dark, skinName: 'midnight');

      expect(wood.extension<BoardColors>()!.skin.name, 'wood');
      expect(midnight.extension<BoardColors>()!.skin.name, 'midnight');
      expect(
        wood.extension<BoardColors>()!.skin.background,
        isNot(midnight.extension<BoardColors>()!.skin.background),
      );
    });
  });
}
