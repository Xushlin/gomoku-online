/// The versus-game catalogue. **The only place game descriptors come from.**
library;

import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';

import '../models/models.dart';

class CatalogFailure implements Exception {
  const CatalogFailure(this.code);

  final String code;

  @override
  String toString() => 'CatalogFailure($code)';
}

/// Reads `GET /api/games` once and keeps it.
///
/// **Kept, not re-fetched per screen**, because the catalogue is the answer to "how big
/// is this board" and that question is asked on every room open. It is a projection of
/// the server's rules registry, so it only changes when the server is redeployed.
class GameCatalogRepository {
  GameCatalogRepository(this._dio);

  final Dio _dio;

  /// The catalogue, or null before the first successful load.
  ValueListenable<List<GameDescriptor>?> get games => _games;
  final _games = ValueNotifier<List<GameDescriptor>?>(null);

  Future<List<GameDescriptor>> load() async {
    final cached = _games.value;
    if (cached != null) return cached;

    final Response<dynamic> response;
    try {
      response = await _dio.get<dynamic>('/api/games');
    } on DioException {
      // **Translated, not passed through.** `validateStatus` admits everything under
      // 500, so a 5xx or a dead socket arrives here as a `DioException` — and letting
      // that escape puts a transport type above the repository boundary, where the
      // only thing a caller can do with it is a bare `catch`. A test asking for a
      // `CatalogFailure` on a 500 is what found this.
      throw const CatalogFailure('network');
    }

    final status = response.statusCode ?? 0;
    if (status >= 400) throw CatalogFailure(status == 401 ? 'unauthorized' : 'generic');

    final list = [
      for (final g in (response.data as List<dynamic>? ?? const []))
        GameDescriptor.fromJson(g as Map<String, dynamic>),
    ];
    _games.value = list;
    return list;
  }

  /// The descriptor for [gameKey], or null when it is not in the catalogue.
  ///
  /// Reads the loaded catalogue and never guesses: a caller that gets null must show a
  /// loading or an error state, not a default board. **A default board size is how
  /// 10×9 gets painted as 15×15** — and that mistake looks like a rendering bug rather
  /// than a missing fetch.
  GameDescriptor? of(String gameKey) {
    // Hand-rolled rather than `firstOrNull`: that lives in `package:collection`, which
    // this project does not declare — using a transitive dependency is how a build
    // breaks when something upstream drops it.
    for (final g in _games.value ?? const <GameDescriptor>[]) {
      if (g.gameKey == gameKey) return g;
    }
    return null;
  }
}
