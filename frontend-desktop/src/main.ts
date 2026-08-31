import { existsSync, readFileSync } from 'node:fs';
import { join, resolve } from 'node:path';
import { pathToFileURL } from 'node:url';
import { app, BrowserWindow, shell, protocol, net } from 'electron';
import { resolveAsset } from './resolve-asset';
import { serverAddress } from './server-address';

/**
 * 格物 / Gewu desktop shell.
 *
 * It wraps the **built Angular app** — the same `dist/gewu-web/browser` the website
 * serves. Ten games, four themes, two locales and 1053 tests come along unchanged,
 * and game eleven still gets written once.
 *
 * The two decisions worth knowing before reading:
 *
 * - **`app://`, not `file://`.** `index.html` carries `<base href="/" />`, and under
 *   `file://` that resolves to the *filesystem root*, so every chunk 404s and the
 *   window is blank. A custom protocol also gives a real origin, which is what makes
 *   `localStorage` (the refresh token lives there) partition sanely and lets a CSP
 *   be stated at all.
 * - **The renderer gets no Node.** `contextIsolation` on, `sandbox` on,
 *   `nodeIntegration` off, and the preload exposes exactly one read-only string.
 */

const APP_SCHEME = 'app';
const APP_ORIGIN = `${APP_SCHEME}://gewu`;

/** The built Angular app. Packaged builds carry it next to the compiled main. */
function webRoot(): string {
  const candidates = [
    join(app.getAppPath(), 'web'),
    resolve(app.getAppPath(), '..', 'frontend-web', 'dist', 'gewu-web', 'browser'),
  ];
  return candidates.find((c) => existsSync(join(c, 'index.html'))) ?? candidates[0];
}

/** `gewu.config.json` beside the executable, if there is one. */
function readConfigFile(): string | null {
  const path = join(app.getPath('exe'), '..', 'gewu.config.json');
  try {
    return existsSync(path) ? readFileSync(path, 'utf8') : null;
  } catch {
    return null;
  }
}

// A custom scheme must be declared **before** `app.ready`, and it has to be marked
// `standard` — without that it gets an opaque origin and `localStorage` throws, which
// shows up as a login screen that cannot remember anything.
protocol.registerSchemesAsPrivileged([
  {
    scheme: APP_SCHEME,
    privileges: { standard: true, secure: true, supportFetchAPI: true, corsEnabled: true },
  },
]);

app.whenReady().then(() => {
  const root = webRoot();

  protocol.handle(APP_SCHEME, async (request) => {
    const asset = resolveAsset(root, request.url, existsSync);
    const response = await net.fetch(pathToFileURL(asset.path).toString());
    // CSP 在这里给,而不是在 index.html 里 —— 那份 HTML 是 Web 端共用的产物,
    // 桌面壳的策略不该渗进浏览器那一份。
    response.headers.set(
      'Content-Security-Policy',
      [
        "default-src 'self'",
        "script-src 'self'",
        // Angular 在运行时注入样式,所以这一条必须放开 —— 说清楚比默默宽松好。
        "style-src 'self' 'unsafe-inline'",
        "img-src 'self' data:",
        "font-src 'self' data:",
        // 服务器可以在别的源上,所以 connect 放开到 https/http/ws。
        'connect-src *',
      ].join('; '),
    );
    return response;
  });

  const server = serverAddress(process.env, readConfigFile);

  const win = new BrowserWindow({
    width: 1280,
    height: 860,
    minWidth: 375, // 平台的硬规则是 375 px 起,窗口不该能缩到规则之外
    show: false,
    autoHideMenuBar: true,
    webPreferences: {
      preload: join(__dirname, 'preload.js'),
      contextIsolation: true,
      nodeIntegration: false,
      sandbox: true,
      additionalArguments: [`--gewu-server=${server}`],
    },
  });

  win.once('ready-to-show', () => win.show());

  // 一个游戏厅没有任何理由在自己的窗口里打开外站,而一个能被导航走的壳
  // 等于把用户的 token 交给了那一页。外链交给系统浏览器。
  win.webContents.setWindowOpenHandler(({ url }) => {
    if (url.startsWith('http://') || url.startsWith('https://')) void shell.openExternal(url);
    return { action: 'deny' };
  });

  win.webContents.on('will-navigate', (event, url) => {
    if (!url.startsWith(APP_ORIGIN)) {
      event.preventDefault();
      if (url.startsWith('http://') || url.startsWith('https://')) void shell.openExternal(url);
    }
  });

  void win.loadURL(`${APP_ORIGIN}/`);
});

app.on('window-all-closed', () => {
  // macOS 的惯例是留在 dock 里;其余平台退出。
  if (process.platform !== 'darwin') app.quit();
});
