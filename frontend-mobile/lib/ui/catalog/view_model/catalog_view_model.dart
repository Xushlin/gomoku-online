import '../../../data/models/models.dart';
import '../../../data/repositories/auth_repository.dart';
import '../../../data/repositories/game_catalog_repository.dart';
import '../../game/board_registry.dart';
import '../../view_model.dart';

/// One row of the catalogue.
class CatalogEntry {
  const CatalogEntry({required this.descriptor, required this.playable});

  final GameDescriptor descriptor;

  /// Whether this client has a board for it. **Read from the board registry**, which
  /// is the only source of that fact — not from a list kept beside the catalogue.
  final bool playable;

  String get gameKey => descriptor.gameKey;
  String get titleKey => 'games.$gameKey.title';
  String get descriptionKey => 'games.$gameKey.description';
}

class CatalogViewModel extends ViewModel {
  CatalogViewModel({required this._catalog, required this._auth});

  final GameCatalogRepository _catalog;
  final AuthRepository _auth;

  List<CatalogEntry> entries = const [];
  bool loading = true;
  String? errorKey;

  /// Loads the catalogue.
  ///
  /// [hasCopy] answers "is there a title for this game in the active locale". It is
  /// passed in rather than read here because a ViewModel has no `Translations` — and
  /// more to the point, **it is the one filter this screen is allowed to apply.**
  ///
  /// Why filter at all: `GET /api/games` returns every *registered* game, and
  /// `xiangqi-endgame` is registered but is not a browsable game anywhere — it has no
  /// title or description in either locale, and the web client does not list it either
  /// (it is reached from the 象棋 manual pages). Rendering it unfiltered puts a literal
  /// `games.xiangqi-endgame.title` on a shipped screen.
  ///
  /// **This is not a second game table.** The criterion is derived from the same i18n
  /// artefact the web client ships, and `test/shared_sync_test.dart` already fails if
  /// that artefact drifts. Measured when written: 7 served, 6 with copy, 1 playable.
  Future<void> load({required bool Function(String key) hasCopy}) async {
    loading = true;
    errorKey = null;
    notifyIfAlive();
    try {
      final games = await _catalog.load();
      entries = [
        for (final g in games)
          if (hasCopy('games.${g.gameKey}.title'))
            CatalogEntry(descriptor: g, playable: rendererFor(g.gameKey) != null),
      ];
    } on CatalogFailure {
      errorKey = 'lobby.errors.generic';
    } catch (_) {
      errorKey = 'auth.errors.network';
    } finally {
      loading = false;
      notifyIfAlive();
    }
  }

  Future<void> signOut() => _auth.logout();
}
