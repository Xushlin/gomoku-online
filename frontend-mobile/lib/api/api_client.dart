/// Everything that talks HTTP to the server.
///
/// One place, like the web client's `core/api/` rule — so "which paths are the
/// server's" and "who attaches the token" each have a single answer.
library;

import 'dart:convert';

import 'package:http/http.dart' as http;

class ApiException implements Exception {
  ApiException(this.status, this.code, this.body);

  final int status;

  /// The server's stable kebab-case error code when it sent one.
  final String code;
  final String body;

  @override
  String toString() => 'ApiException($status, $code)';
}

/// Reads and writes the tokens; the storage backend is injected so tests do not
/// need a keystore.
abstract class TokenStore {
  Future<String?> readRefresh();
  Future<void> writeRefresh(String? token);
  String? get access;
  set access(String? value);
}

class ApiClient {
  ApiClient({required this.baseUrl, required this.tokens, http.Client? client})
    : _client = client ?? http.Client();

  final String baseUrl;
  final TokenStore tokens;
  final http.Client _client;

  /// Endpoints where a token is irrelevant or *is* the credential.
  ///
  /// Same list as the web client's interceptor, and matched on the **path** for the
  /// same reason: the base URL makes these absolute, and `startsWith('/api/auth')`
  /// on a full URL is simply false — which would attach a token to refresh and then
  /// retry refresh with the very credential that failed.
  static const _noAuth = <String>['/api/auth/login', '/api/auth/register', '/api/auth/refresh'];

  static bool needsNoAuth(String path) => _noAuth.any(path.startsWith);

  Future<dynamic> get(String path) => _send('GET', path, null);

  Future<dynamic> post(String path, [Object? body]) => _send('POST', path, body);

  Future<dynamic> _send(String method, String path, Object? body, {bool retried = false}) async {
    final request = http.Request(method, Uri.parse('$baseUrl$path'))
      ..headers['content-type'] = 'application/json';
    if (!needsNoAuth(path) && tokens.access != null) {
      request.headers['authorization'] = 'Bearer ${tokens.access}';
    }
    if (body != null) request.body = jsonEncode(body);

    final response = await http.Response.fromStream(await _client.send(request));

    // One silent refresh, one retry — never a loop. A loop here turns an expired
    // session into a request storm against the login endpoint.
    if (response.statusCode == 401 && !retried && !needsNoAuth(path)) {
      if (await refresh()) return _send(method, path, body, retried: true);
    }

    if (response.statusCode >= 400) {
      throw ApiException(response.statusCode, _codeOf(response.body), response.body);
    }
    return response.body.isEmpty ? null : jsonDecode(response.body);
  }

  /// Exchanges the refresh token for a new pair.
  ///
  /// **The refresh token rotates.** The old one is dead the moment this succeeds, so
  /// the new one must be stored. The web client learned this by landing on the login
  /// page every second start.
  Future<bool> refresh() async {
    final current = await tokens.readRefresh();
    if (current == null || current.isEmpty) return false;
    try {
      final result = await post('/api/auth/refresh', {'refreshToken': current});
      await _adopt(result as Map<String, dynamic>);
      return true;
    } on ApiException {
      await tokens.writeRefresh(null);
      tokens.access = null;
      return false;
    }
  }

  Future<Map<String, dynamic>> login(String email, String password) async {
    final result = await post('/api/auth/login', {'email': email, 'password': password});
    return _adopt(result as Map<String, dynamic>);
  }

  Future<Map<String, dynamic>> register(String email, String username, String password) async {
    final result = await post('/api/auth/register', {
      'email': email,
      'username': username,
      'password': password,
    });
    return _adopt(result as Map<String, dynamic>);
  }

  Future<Map<String, dynamic>> _adopt(Map<String, dynamic> auth) async {
    tokens.access = auth['accessToken'] as String?;
    await tokens.writeRefresh(auth['refreshToken'] as String?);
    return auth;
  }

  /// Pulls the server's code out of a ProblemDetails-ish body.
  ///
  /// Text is never matched — the web client shipped keyword matching once and it
  /// worked only in Development, where detailed errors are on.
  static String _codeOf(String body) {
    if (body.isEmpty) return '';
    try {
      final parsed = jsonDecode(body);
      if (parsed is Map<String, dynamic>) {
        for (final key in const ['code', 'errorCode', 'title', 'detail']) {
          final value = parsed[key];
          if (value is String && value.isNotEmpty) return value;
        }
      }
    } catch (_) {
      // Not JSON. Returning empty is honest; guessing from prose is not.
    }
    return '';
  }
}
