/// The HTTP transport. **Nothing outside `data/repositories` knows this exists.**
library;

import 'package:dio/dio.dart';

import 'token_store.dart';

/// Endpoints where a token is irrelevant or *is* the credential.
const noAuthPaths = <String>[
  '/api/auth/login',
  '/api/auth/register',
  '/api/auth/refresh',
];

/// The **path** of a URL, whether it arrived relative or absolute.
///
/// This exists because the base URL makes every request absolute, and
/// `'https://server/api/auth/login'.startsWith('/api/auth/login')` is simply
/// **false**. The consequence is not a crash: it is an `Authorization` header on
/// login/register/refresh, and a silent refresh retried on the very request that
/// *is* the credential.
///
/// **Both the web client and the desktop shell shipped this bug**, so it is an
/// inherited lesson rather than a new discovery.
String pathOf(String url) {
  final scheme = url.indexOf('://');
  if (scheme == -1) return url;
  final slash = url.indexOf('/', scheme + 3);
  return slash == -1 ? '/' : url.substring(slash);
}

bool isNoAuth(String url) {
  final path = pathOf(url);
  return noAuthPaths.any(path.startsWith);
}

/// Attaches the access token to everything except the credential endpoints.
class AuthInterceptor extends Interceptor {
  AuthInterceptor(this._tokens);

  final TokenStore _tokens;

  @override
  void onRequest(RequestOptions options, RequestInterceptorHandler handler) {
    final token = _tokens.access;
    if (token != null && token.isNotEmpty && !isNoAuth(options.uri.toString())) {
      options.headers['Authorization'] = 'Bearer $token';
    }
    handler.next(options);
  }
}

/// On 401: refresh once, retry once. **Never a loop.**
///
/// A loop here turns an expired session into a request storm against the login
/// endpoint, which is a much worse failure than one rejected request.
///
/// **It hooks `onResponse`, not `onError`, and that is not a style choice.**
/// `validateStatus` below admits everything under 500 so repositories can inspect
/// status codes instead of catching, which means a 401 arrives as a *successful*
/// response and `onError` never fires. The first version of this interceptor put the
/// logic in `onError` and was simply dead — no refresh, no retry, and the only
/// symptom was a request that failed exactly as it would have anyway. A test caught
/// it; reading the code did not.
class RefreshInterceptor extends Interceptor {
  RefreshInterceptor({required this.dio, required this.tokens, required this.refresh});

  final Dio dio;
  final TokenStore tokens;

  /// Injected rather than called directly so this interceptor does not depend on a
  /// repository, which would invert the layering it lives underneath.
  final Future<bool> Function() refresh;

  static const _retriedKey = 'gewu.retried';

  @override
  Future<void> onResponse(
    Response<dynamic> response,
    ResponseInterceptorHandler handler,
  ) async {
    final request = response.requestOptions;
    final alreadyRetried = request.extra[_retriedKey] == true;

    if (response.statusCode != 401 ||
        alreadyRetried ||
        isNoAuth(request.uri.toString())) {
      return handler.next(response);
    }

    if (!await refresh()) return handler.next(response);

    try {
      final retry = await dio.request<dynamic>(
        request.path,
        data: request.data,
        queryParameters: request.queryParameters,
        options: Options(method: request.method, headers: {...request.headers})
          ..extra = {...request.extra, _retriedKey: true},
      );
      return handler.resolve(retry);
    } on DioException catch (e) {
      // The retry itself blew up at the transport level. Hand back the original 401
      // rather than a confusing second error.
      return handler.next(e.response ?? response);
    }
  }
}

/// Builds the configured client.
///
/// `refresh` is passed in because the refresh call itself goes through this same
/// client — wiring it as a constructor argument would be circular.
Dio buildDio({
  required String baseUrl,
  required TokenStore tokens,
  required Future<bool> Function() refresh,
  HttpClientAdapter? adapter,
}) {
  final dio = Dio(
    BaseOptions(
      baseUrl: baseUrl,
      connectTimeout: const Duration(seconds: 10),
      receiveTimeout: const Duration(seconds: 20),
      contentType: 'application/json',
      // Let interceptors see 401 instead of Dio throwing before they run.
      validateStatus: (status) => status != null && status < 500,
    ),
  );
  if (adapter != null) dio.httpClientAdapter = adapter;

  dio.interceptors.add(AuthInterceptor(tokens));
  dio.interceptors.add(RefreshInterceptor(dio: dio, tokens: tokens, refresh: refresh));
  return dio;
}
