import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:gewu_mobile/data/services/dio_client.dart';
import 'package:gewu_mobile/data/services/token_store.dart';

const base = 'https://gewu.test';

/// A hand-rolled adapter that records every request and replies from a script.
///
/// Written rather than pulled from a mocking package on purpose: the first attempt
/// used one whose route matching consumed each stub once, so the **retry** — the
/// exact behaviour under test — found no route and the test failed for a reason that
/// had nothing to do with the code. Fifteen lines of adapter has no such semantics
/// to be surprised by.
class ScriptedAdapter implements HttpClientAdapter {
  ScriptedAdapter(this.reply);

  /// Called with the request count so far (1 for the first), returns a status code.
  final int Function(int call, RequestOptions options) reply;

  final List<RequestOptions> requests = [];

  @override
  Future<ResponseBody> fetch(
    RequestOptions options,
    Stream<Uint8List>? requestStream,
    Future<void>? cancelFuture,
  ) async {
    requests.add(options);
    final status = reply(requests.length, options);
    return ResponseBody.fromString(
      jsonEncode(status == 200 ? {'ok': true} : {'code': 'unauthorized'}),
      status,
      headers: {
        Headers.contentTypeHeader: [Headers.jsonContentType],
      },
    );
  }

  @override
  void close({bool force = false}) {}
}

({Dio dio, ScriptedAdapter adapter, MemoryTokenStore tokens, List<String> refreshes}) harness(
  int Function(int call, RequestOptions options) reply, {
  bool refreshSucceeds = true,
}) {
  final tokens = MemoryTokenStore()..access = 'first-token';
  final refreshes = <String>[];
  final adapter = ScriptedAdapter(reply);

  final dio = buildDio(
    baseUrl: base,
    tokens: tokens,
    adapter: adapter,
    refresh: () async {
      refreshes.add('called');
      if (!refreshSucceeds) return false;
      tokens.access = 'second-token';
      return true;
    },
  );
  return (dio: dio, adapter: adapter, tokens: tokens, refreshes: refreshes);
}

void main() {
  group('the token goes on everything except the credential endpoints', () {
    test('an ordinary request carries it', () async {
      final h = harness((_, _) => 200);

      await h.dio.get<dynamic>('/api/rooms');

      expect(h.adapter.requests.single.headers['Authorization'], 'Bearer first-token');
    });

    /// **与上一条同时存在。** 少了它,一个「从不带 token」的实现也能通过。
    ///
    /// `startsWith('/api/auth/refresh')` 对**绝对地址**恒假 —— 这个 bug 在
    /// web 端与桌面壳各自出现过一次,所以它是继承来的教训。
    test('refresh does NOT carry it, even though the url is absolute', () async {
      final h = harness((_, _) => 200);

      await h.dio.post<dynamic>('/api/auth/refresh', data: {});

      expect(h.adapter.requests.single.headers.containsKey('Authorization'), isFalse);
    });
  });

  group('401 refreshes once and retries once — never a loop', () {
    test('a 401 that refresh fixes is retried and succeeds', () async {
      final h = harness((call, _) => call == 1 ? 401 : 200);

      final response = await h.dio.get<dynamic>('/api/rooms');

      expect(response.statusCode, 200);
      expect(h.adapter.requests, hasLength(2), reason: 'original + one retry');
      expect(h.refreshes, hasLength(1));
      // The retry must carry the NEW token, or refreshing bought nothing.
      expect(h.adapter.requests.last.headers['Authorization'], 'Bearer second-token');
    });

    /// **这是这组存在的理由。** 成环会把一个过期会话变成对登录端点的请求风暴,
    /// 比一个被拒的请求严重得多。
    test('a 401 that survives refresh stops, it does not spin', () async {
      final h = harness((_, _) => 401);

      final response = await h.dio.get<dynamic>('/api/rooms');

      expect(response.statusCode, 401);
      expect(h.adapter.requests, hasLength(2), reason: 'original + exactly one retry');
      expect(h.refreshes, hasLength(1), reason: 'refresh is not attempted again');
    });

    test('when refresh itself fails, nothing is retried', () async {
      final h = harness((_, _) => 401, refreshSucceeds: false);

      await h.dio.get<dynamic>('/api/rooms');

      expect(h.adapter.requests, hasLength(1));
      expect(h.refreshes, hasLength(1));
    });

    test('a 401 from the credential endpoints is never retried', () async {
      final h = harness((_, _) => 401);

      await h.dio.post<dynamic>('/api/auth/login', data: {});

      expect(h.adapter.requests, hasLength(1));
      expect(h.refreshes, isEmpty, reason: 'refreshing on a failed login is nonsense');
    });
  });
}
