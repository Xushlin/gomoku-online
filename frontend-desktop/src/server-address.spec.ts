import { describe, expect, it } from 'vitest';
import { configDirectory, DEFAULT_SERVER, serverAddress } from './server-address';

const noFile = () => null;

describe('serverAddress', () => {
  it('falls back to the local dev api so a fresh clone runs', () => {
    expect(serverAddress({}, noFile)).toBe(DEFAULT_SERVER);
  });

  it('prefers the environment', () => {
    expect(serverAddress({ GEWU_SERVER: 'https://a.test' }, noFile)).toBe('https://a.test');
  });

  it('reads the config file when the environment is silent', () => {
    expect(serverAddress({}, () => '{"server":"https://b.test"}')).toBe('https://b.test');
  });

  it('lets the environment win over the config file', () => {
    expect(serverAddress({ GEWU_SERVER: 'https://a.test' }, () => '{"server":"https://b.test"}')).toBe(
      'https://a.test',
    );
  });

  /**
   * `API_BASE_URL` 的文档写着「没有结尾斜杠」,而 `'https://x/' + '/api/rooms'`
   * 是 `https://x//api/rooms` —— 多数服务器照答,直到有一台不答,
   * 那时的表现是**某一条路由 404**,而别的都好。
   */
  it('strips a trailing slash from either source', () => {
    expect(serverAddress({ GEWU_SERVER: 'https://a.test/' }, noFile)).toBe('https://a.test');
    expect(serverAddress({}, () => '{"server":"https://b.test/"}')).toBe('https://b.test');
  });

  describe('a broken config must not stop the app starting', () => {
    it.each([
      ['not json at all'],
      ['{"server":123}'],
      ['{"server":"   "}'],
      ['{}'],
      ['null'],
    ])('falls back for %s', (raw) => {
      expect(serverAddress({}, () => raw)).toBe(DEFAULT_SERVER);
    });
  });

  it('ignores an empty environment variable rather than using it', () => {
    // 一个空的 GEWU_SERVER 是「没设」,不是「服务器在空字符串」——
    // 后者会让每个请求都打到 app:// 自己身上,而症状是全部 404。
    expect(serverAddress({ GEWU_SERVER: '  ' }, () => '{"server":"https://b.test"}')).toBe(
      'https://b.test',
    );
  });
});

/**
 * 便携版从临时目录启动 —— 这一组是打包并**双击**之后才发现要写的。
 */
describe('configDirectory', () => {
  const INSTALLED = String.raw`C:\Program Files\Gewu`;
  const PORTABLE = String.raw`D:\Games\Gewu`;
  // 实测的那个临时目录,原样抄下来。
  const TEMP_COPY = String.raw`C:\Users\Michael\AppData\Local\Temp\3IfVAEDHBEkVmiFuhcTB1PVHZjb`;

  it('uses the exe directory when nothing says otherwise', () => {
    expect(configDirectory({}, INSTALLED)).toBe(INSTALLED);
  });

  /**
   * 实测:便携 exe 跑在 `…\AppData\Local\Temp\3IfVAEDHBEkVmiFuhcTB1PVHZjb\Gewu.exe`。
   * 配置文件在用户放 exe 的地方,而 `app.getPath('exe')` 指向那个临时目录 ——
   * 于是配置**永远读不到**,应用安静地连去默认服务器,屏幕上没有任何提示。
   */
  it('prefers the portable directory, because the exe runs from a temp copy', () => {
    expect(configDirectory({ PORTABLE_EXECUTABLE_DIR: PORTABLE }, TEMP_COPY)).toBe(PORTABLE);
  });

  it('ignores an empty portable dir rather than resolving to nothing', () => {
    expect(configDirectory({ PORTABLE_EXECUTABLE_DIR: '   ' }, INSTALLED)).toBe(INSTALLED);
  });
});
