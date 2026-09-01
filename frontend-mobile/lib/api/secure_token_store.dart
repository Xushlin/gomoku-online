/// The real [TokenStore]: refresh token in the platform keystore, access token in
/// memory only.
///
/// The access token is short-lived and is re-obtained on every start, so persisting
/// it would add exposure for nothing.
library;

import 'package:flutter_secure_storage/flutter_secure_storage.dart';

import 'api_client.dart';

class SecureTokenStore implements TokenStore {
  SecureTokenStore([FlutterSecureStorage? storage])
    : _storage = storage ?? const FlutterSecureStorage();

  static const _refreshKey = 'gewu.refresh';

  final FlutterSecureStorage _storage;

  @override
  String? access;

  @override
  Future<String?> readRefresh() => _storage.read(key: _refreshKey);

  @override
  Future<void> writeRefresh(String? token) =>
      token == null ? _storage.delete(key: _refreshKey) : _storage.write(key: _refreshKey, value: token);
}

/// In-memory store for tests and for the hub probe.
class MemoryTokenStore implements TokenStore {
  String? _refresh;

  @override
  String? access;

  @override
  Future<String?> readRefresh() async => _refresh;

  @override
  Future<void> writeRefresh(String? token) async => _refresh = token;
}
