/// Authentication. **The only place auth JSON becomes a model.**
library;

import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';

import '../models/models.dart';
import '../services/token_store.dart';

/// A failure the UI can act on: a stable server code, never prose.
///
/// The web client shipped keyword-matching on English error text once, and it worked
/// only in Development — production replaces the message. Codes survive both.
class AuthFailure implements Exception {
  const AuthFailure(this.code);

  final String code;

  @override
  String toString() => 'AuthFailure($code)';
}

class AuthRepository {
  AuthRepository({required this._dio, required this._tokens});

  final Dio _dio;
  final TokenStore _tokens;

  AuthUser? currentUser;

  /// Whether a session is live. **A value, not an event, and that is load-bearing.**
  ///
  /// The router takes this as its `refreshListenable`, and the silent resume at
  /// startup is fired before the router exists. An event would be lost in that gap;
  /// a value is simply read as soon as the first `redirect` runs.
  ValueListenable<bool> get signedIn => _signedIn;
  final _signedIn = ValueNotifier<bool>(false);

  Future<AuthUser> login(String email, String password) =>
      _authenticate('/api/auth/login', {'email': email, 'password': password});

  Future<AuthUser> register(String email, String username, String password) =>
      _authenticate('/api/auth/register', {
        'email': email,
        'username': username,
        'password': password,
      });

  /// Exchanges the stored refresh token for a new pair.
  ///
  /// **The refresh token rotates**: the old one is dead the moment this succeeds, so
  /// the new one must be stored. The web client learned this by landing on the login
  /// page every second start.
  Future<bool> refresh() async {
    final stored = await _tokens.readRefresh();
    if (stored == null || stored.isEmpty) return false;
    try {
      final response = await _dio.post<dynamic>(
        '/api/auth/refresh',
        data: {'refreshToken': stored},
      );
      if (response.statusCode != null && response.statusCode! >= 400) {
        await _forget();
        return false;
      }
      await _adopt(response.data as Map<String, dynamic>);
      return true;
    } on DioException {
      await _forget();
      return false;
    }
  }

  Future<void> logout() => _forget();

  Future<AuthUser> _authenticate(String path, Map<String, Object?> body) async {
    final Response<dynamic> response;
    try {
      response = await _dio.post<dynamic>(path, data: body);
    } on DioException catch (e) {
      throw AuthFailure(_codeOf(e.response?.data) ?? 'network');
    }
    if (response.statusCode != null && response.statusCode! >= 400) {
      throw AuthFailure(_codeOf(response.data) ?? 'generic');
    }
    return _adopt(response.data as Map<String, dynamic>);
  }

  Future<AuthUser> _adopt(Map<String, dynamic> json) async {
    final result = AuthResult.fromJson(json);
    _tokens.access = result.tokens.access;
    await _tokens.writeRefresh(result.tokens.refresh);
    currentUser = result.user;
    _signedIn.value = true;
    return result.user;
  }

  Future<void> _forget() async {
    _tokens.access = null;
    currentUser = null;
    await _tokens.writeRefresh(null);
    // Last: everything downstream reacts to this, and reacting to a half-cleared
    // session is how a redirect races the state it is redirecting on.
    _signedIn.value = false;
  }

  /// Pulls the server's code out of a ProblemDetails-ish body. Never guesses from prose.
  static String? _codeOf(Object? body) {
    if (body is! Map) return null;
    for (final key in const ['code', 'errorCode', 'title', 'detail']) {
      final value = body[key];
      if (value is String && value.isNotEmpty) return value;
    }
    return null;
  }
}
