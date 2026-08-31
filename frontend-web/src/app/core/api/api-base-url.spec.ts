import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { afterEach, describe, expect, it } from 'vitest';
import { authInterceptor } from '../auth/auth.interceptor';
import { APP_INTERCEPTORS } from '../http/http-config';
import { AuthService } from '../auth/auth.service';
import {
  GAME_HUB_URL,
  GameHubService,
  DefaultGameHubService,
  SIGNALR_LOADER,
} from '../realtime/game-hub.service';
import { API_BASE_URL, apiBaseUrlInterceptor, hostApiBaseUrl, isServerPath, serverUrl } from './api-base-url';

const REMOTE = 'https://gewu.example';

function http(base?: string) {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(withInterceptors([apiBaseUrlInterceptor])),
      provideHttpClientTesting(),
      ...(base === undefined ? [] : [{ provide: API_BASE_URL, useValue: base }]),
    ],
  });
  return {
    client: TestBed.inject(HttpClient),
    ctrl: TestBed.inject(HttpTestingController),
  };
}

/** Drives the real hub service far enough to see the URL it hands SignalR. */
async function hubUrlWith(base?: string): Promise<string> {
  let seen = '';
  const stubModule = {
    HubConnectionBuilder: class {
      withUrl(url: string) {
        seen = url;
        return this;
      }
      withAutomaticReconnect() {
        return this;
      }
      configureLogging() {
        return this;
      }
      build() {
        return {
          state: 'Disconnected',
          start: () => Promise.resolve(),
          stop: () => Promise.resolve(),
          on: () => undefined,
          onreconnecting: () => undefined,
          onreconnected: () => undefined,
          onclose: () => undefined,
          invoke: () => Promise.resolve(),
        };
      }
    },
    HubConnectionState: { Connected: 'Connected', Disconnected: 'Disconnected' },
    LogLevel: { Warning: 3 },
  };

  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    providers: [
      { provide: GameHubService, useClass: DefaultGameHubService },
      { provide: SIGNALR_LOADER, useValue: () => Promise.resolve(stubModule) },
      {
        provide: AuthService,
        useValue: {
          accessToken: signal('jwt'),
          user: signal({ id: 'u-1', username: 'a', email: 'a@a' }),
          isAuthenticated: signal(true),
        },
      },
      ...(base === undefined ? [] : [{ provide: API_BASE_URL, useValue: base }]),
    ],
  });

  await (TestBed.inject(GameHubService) as DefaultGameHubService).joinRoom('r-1');
  return seen;
}

describe('API_BASE_URL', () => {
  describe('the default keeps the web app byte-for-byte unchanged', () => {
    it('leaves a REST url exactly as written', () => {
      const { client, ctrl } = http();
      client.get('/api/rooms').subscribe();

      // 不是 `toContain`,是 `expectOne` 的精确匹配 —— 与既有 1042 条测试同一种断言,
      // 而那批断言就是这条不变量的可执行形式。
      ctrl.expectOne('/api/rooms');
      ctrl.verify();
    });

    it('leaves the hub url exactly as written', async () => {
      expect(await hubUrlWith()).toBe(GAME_HUB_URL);
    });
  });

  describe('a host that is not same-origin can move both halves', () => {
    it('prefixes REST', () => {
      const { client, ctrl } = http(REMOTE);
      client.get('/api/rooms').subscribe();

      ctrl.expectOne(`${REMOTE}/api/rooms`);
      ctrl.verify();
    });

    /**
     * **这一条与上一条 MUST 同时存在。**
     *
     * 实时连接不走 `HttpClient`,所以那个拦截器碰不到它 —— 它是最容易被漏掉的一半。
     * 而漏掉的表现不是报错:REST 全部正常,登录成功,房间列表出得来,**只有棋盘再也不更新**。
     */
    it('prefixes the hub too', async () => {
      expect(await hubUrlWith(REMOTE)).toBe(`${REMOTE}${GAME_HUB_URL}`);
    });
  });

  describe('what counts as the server, and what does not', () => {
    it('treats /api/ and /hubs/ as the server', () => {
      expect(isServerPath('/api/rooms')).toBe(true);
      expect(isServerPath('/hubs/match')).toBe(true);
    });

    /**
     * i18n 是**应用自己的资源**,不是服务器的。
     *
     * 桌面壳里它就在包旁边。把它也前缀掉,应用会在服务器连不上时**连字都渲染不出来** ——
     * 那比「登录不了」严重得多,而且看起来像应用坏了而不是网络断了。
     */
    it('does not treat the locale files as the server', () => {
      expect(isServerPath('/i18n/zh-CN.json')).toBe(false);

      const { client, ctrl } = http(REMOTE);
      client.get('/i18n/zh-CN.json').subscribe();

      ctrl.expectOne('/i18n/zh-CN.json');
      ctrl.verify();
    });

    it('serverUrl leaves app assets alone even with a base set', () => {
      expect(serverUrl(REMOTE, '/api/x')).toBe(`${REMOTE}/api/x`);
      expect(serverUrl(REMOTE, '/i18n/en.json')).toBe('/i18n/en.json');
      expect(serverUrl('', '/api/x')).toBe('/api/x');
    });
  });

  /**
   * **生产链条本身的两条断言。**
   *
   * 这里跑的是 `APP_INTERCEPTORS` —— `app.config.ts` 用的**同一份**,而不是在测试里
   * 手写一遍。手写一遍等于 production 与测试各持一份,那两份迟早不一致。
   *
   * 为什么不直接用 `appConfig.providers`:那会启动 i18n 的 app-initializer,在拆卸时
   * 留下一个 `EmptyError` —— vitest 报「11 passed **和** 1 error」,并且明说
   * 「this might cause false positive tests」。`app.config.spec.ts` 的注释里already
   * 记着这道疤,试过一次,退回来了。
   *
   * **说清它守得住什么、守不住什么:** 它守得住「默认前缀是空的」和「链条里有那个
   * 拦截器」;它守不住「有人把 `app.config.ts` 改成不用 `APP_INTERCEPTORS`」——
   * 那需要启动整个 config,而代价是上面那个 false positive 风险。
   */
  describe('the production interceptor chain', () => {
    function withChain(base?: string) {
      TestBed.resetTestingModule();
      TestBed.configureTestingModule({
        providers: [
          provideHttpClient(withInterceptors([...APP_INTERCEPTORS])),
          provideHttpClientTesting(),
          provideRouter([]),
          ...(base === undefined ? [] : [{ provide: API_BASE_URL, useValue: base }]),
          {
            provide: AuthService,
            useValue: {
              accessToken: signal('jwt-token'),
              user: signal(null),
              isAuthenticated: signal(true),
              refresh: () => of('jwt-token'),
              logout: () => of(undefined),
            },
          },
        ],
      });
      return {
        client: TestBed.inject(HttpClient),
        ctrl: TestBed.inject(HttpTestingController),
      };
    }

    it('includes the base-url interceptor, first', () => {
      expect(APP_INTERCEPTORS[0]).toBe(apiBaseUrlInterceptor);
    });

    it('leaves both a server path and an app asset exactly as written', () => {
      const { client, ctrl } = withChain();
      client.get('/api/rooms').subscribe();
      client.get('/i18n/zh-CN.json').subscribe();

      // 精确匹配 —— 多一个前缀就红。这是「Web 端行为不变」唯一真正的守卫。
      ctrl.expectOne('/api/rooms');
      ctrl.expectOne('/i18n/zh-CN.json');
    });
  });

  /**
   * 宿主(Electron 壳 / 将来的手机壳 / 独立 API 域名的自托管)怎么给地址。
   */
  describe('the host global', () => {
    const hostful = globalThis as { gewuHost?: unknown };

    afterEach(() => {
      delete hostful.gewuHost;
    });

    it('uses what the host says', () => {
      hostful.gewuHost = Object.freeze({ apiBaseUrl: REMOTE });

      expect(hostApiBaseUrl()).toBe(REMOTE);
    });

    /**
     * **与上一条 MUST 同时存在。** 少了它,一个「永远返回宿主值」的实现在浏览器里
     * 会返回 `undefined`,而每个地址都变成 `undefined/api/rooms` —— 全部 404,
     * 且看起来像后端挂了。
     */
    it('is empty when there is no host, which is the browser', () => {
      expect(hostApiBaseUrl()).toBe('');
    });

    /**
     * 宿主是别人写的进程,而这个值在注入器构造期间被同步读走 —— 那时没有任何东西
     * 校验过它。给一个非字符串就当没有:否则每个请求前面会挂上 `[object Object]`。
     */
    it('treats a host that sets a non-string as absent', () => {
      hostful.gewuHost = { apiBaseUrl: { nope: 1 } };
      expect(hostApiBaseUrl()).toBe('');

      hostful.gewuHost = {};
      expect(hostApiBaseUrl()).toBe('');
    });

    /**
     * **`API_BASE_URL` 本身要读它 —— 而这一条是变异测试逼出来的。**
     *
     * 上面几条都直接调 `hostApiBaseUrl()`,所以把 token 的 factory 改回
     * `() => ''` 时**一条都不红**:它们验的是那个函数,不是那个 token 在用它。
     *
     * 而那正是桌面壳会死掉的方式,并且死得很难看:每个请求退回同源,变成
     * `app://gewu/api/…`,被壳自己的 SPA 回落当成路由,**返回 index.html 和一个 200**。
     * 没有报错、没有 404 —— 只是所有数据都不见了。
     */
    it('is what API_BASE_URL resolves to, not just a function nobody calls', () => {
      hostful.gewuHost = Object.freeze({ apiBaseUrl: REMOTE });

      TestBed.resetTestingModule();
      TestBed.configureTestingModule({ providers: [] });

      expect(TestBed.inject(API_BASE_URL)).toBe(REMOTE);
    });

    it('resolves to empty when there is no host', () => {
      TestBed.resetTestingModule();
      TestBed.configureTestingModule({ providers: [] });

      expect(TestBed.inject(API_BASE_URL)).toBe('');
    });
  });

  /**
   * **一个只在桌面壳里才会出现的缺陷,而它值得单独一组。**
   *
   * `authInterceptor` 原本用 `url.startsWith('/api/auth/login')` 判断「这条不带 token」。
   * base 一非空,地址就变成 `https://…/api/auth/login`,那个判断**直接是 false** ——
   * 于是登录、注册、刷新三条都会被挂上 `Authorization`,而刷新还会在 401 时拿
   * **它自己**去重试一遍。
   *
   * 浏览器里永远看不到:base 是空的,地址一直是相对的。只有桌面壳会踩,而那是最难
   * 归因的地方 —— 屏幕上没有任何东西提示原因。
   */
  describe('the token rule survives an absolute url', () => {
    function withAuth(base: string) {
      TestBed.resetTestingModule();
      TestBed.configureTestingModule({
        providers: [
          provideHttpClient(withInterceptors([apiBaseUrlInterceptor, authInterceptor])),
          provideHttpClientTesting(),
          provideRouter([]),
          { provide: API_BASE_URL, useValue: base },
          {
            provide: AuthService,
            useValue: {
              accessToken: signal('jwt-token'),
              user: signal(null),
              isAuthenticated: signal(true),
              refresh: () => of('jwt-token'),
              logout: () => of(undefined),
            },
          },
        ],
      });
      return {
        client: TestBed.inject(HttpClient),
        ctrl: TestBed.inject(HttpTestingController),
      };
    }

    it('does not attach a token to refresh, even when the url is absolute', () => {
      const { client, ctrl } = withAuth(REMOTE);
      client.post('/api/auth/refresh', {}).subscribe();

      const req = ctrl.expectOne(`${REMOTE}/api/auth/refresh`);
      expect(req.request.headers.has('Authorization')).toBe(false);
      ctrl.verify();
    });

    it('still attaches a token to an ordinary absolute request', () => {
      const { client, ctrl } = withAuth(REMOTE);
      client.get('/api/rooms').subscribe();

      const req = ctrl.expectOne(`${REMOTE}/api/rooms`);
      // 两条同时存在:少了这一条,一个**从不**挂 token 的实现也能通过上一条。
      expect(req.request.headers.get('Authorization')).toBe('Bearer jwt-token');
      ctrl.verify();
    });
  });
});
