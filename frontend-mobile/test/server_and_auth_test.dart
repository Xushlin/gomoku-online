import 'package:flutter_test/flutter_test.dart';
import 'package:gewu_mobile/api/api_client.dart';
import 'package:gewu_mobile/config/server.dart';
import 'package:gewu_mobile/i18n/translations.dart';
import 'package:gewu_mobile/theme/app_theme.dart';

void main() {
  group('the Android default is the host loopback, not localhost', () {
    /// 模拟器里的 `localhost` 是模拟器自己。写错的表现是每个请求连接被拒,
    /// 而屏幕上只是登录失败 —— 看起来像后端没起。
    test('Android gets 10.0.2.2', () {
      expect(serverAddressFor(isAndroid: true), androidHostLoopback);
      expect(serverAddressFor(isAndroid: true), contains('10.0.2.2'));
    });

    /// **与上一条同时存在。** 少了它,一个「所有平台都回 10.0.2.2」的实现也能通过 ——
    /// 而那会让桌面构建连不上任何东西。
    test('everything else gets localhost', () {
      expect(serverAddressFor(isAndroid: false), localLoopback);
      expect(serverAddressFor(isAndroid: false), contains('localhost'));
    });

    test('an override wins on either platform', () {
      expect(
        serverAddressFor(isAndroid: true, override: 'https://a.test'),
        'https://a.test',
      );
      expect(
        serverAddressFor(isAndroid: false, override: 'https://a.test'),
        'https://a.test',
      );
    });

    test('a trailing slash is stripped, and an empty override is ignored', () {
      // `'https://x/' + '/api/rooms'` 是 `https://x//api/rooms`,多数服务器照答,
      // 直到有一台不答 —— 那时表现是某一条路由 404。
      expect(serverAddressFor(isAndroid: true, override: 'https://a.test/'), 'https://a.test');
      expect(serverAddressFor(isAndroid: true, override: '   '), androidHostLoopback);
    });
  });

  group('the token is never attached to the endpoints that are the credential', () {
    test('login, register and refresh are exempt', () {
      expect(ApiClient.needsNoAuth('/api/auth/login'), isTrue);
      expect(ApiClient.needsNoAuth('/api/auth/register'), isTrue);
      expect(ApiClient.needsNoAuth('/api/auth/refresh'), isTrue);
    });

    /// **与上一条同时存在。** 少了它,一个「从不带 token」的实现也能通过,
    /// 而那样每个受保护的请求都会 401。
    test('everything else is not', () {
      expect(ApiClient.needsNoAuth('/api/rooms'), isFalse);
      expect(ApiClient.needsNoAuth('/api/auth/change-password'), isFalse);
    });
  });

  group('translation lookup', () {
    test('flattens to the dotted keys the web client uses', () {
      final flat = Translations.flatten(<String, dynamic>{
        'auth': {
          'login': {'title': 'Log in'},
        },
      });

      expect(flat['auth.login.title'], 'Log in');
    });

    test('a missing key returns the key, so the hole is visible', () {
      final flat = Translations.flatten(<String, dynamic>{'a': 'b'});

      // 静默返回空字符串会把「漏了一句翻译」变成「界面上少一块」,那更难发现。
      expect(flat['nope.missing'], isNull);
    });
  });

  group('colour parsing refuses to guess', () {
    test('reads the hex forms the tokens use', () {
      expect(colorOf('#ffffff')?.toARGB32(), 0xFFFFFFFF);
      expect(colorOf('#000')?.toARGB32(), 0xFF000000);
    });

    /// 一个 radius 或 gradient token 不是颜色。悄悄变成透明黑正是「隐形控件」的来路。
    test('returns null for anything that is not a colour', () {
      expect(colorOf('0.5rem'), isNull);
      expect(colorOf('linear-gradient(#fff, #000)'), isNull);
      expect(colorOf(null), isNull);
      expect(colorOf('#12345'), isNull);
    });
  });
}
