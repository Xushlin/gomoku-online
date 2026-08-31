/**
 * Where the desktop shell should look for the server.
 *
 * Kept as a pure function for the same reason {@link resolveAsset} is: it can be
 * pinned without launching Electron, and it is small enough that a reader can see
 * the whole precedence at once.
 *
 * Precedence, most specific first:
 *
 * 1. `GEWU_SERVER` in the environment — how a developer points the shell at a
 *    local API without editing anything.
 * 2. `server` in a `gewu.config.json` beside the executable — how an install is
 *    configured without rebuilding.
 * 3. `http://localhost:5145` — the local dev API, so a fresh clone runs.
 *
 * **A trailing slash is stripped.** `API_BASE_URL` is documented as having none,
 * and `'https://x/' + '/api/rooms'` is `https://x//api/rooms` — which most servers
 * answer, right up until one does not, and then the failure is a 404 on one route.
 */
export const DEFAULT_SERVER = 'http://localhost:5145';

/**
 * Which directory `gewu.config.json` is expected to sit in.
 *
 * **A portable build does not run from where the user put it.** electron-builder's
 * portable target unpacks itself into a temp directory and launches from there —
 * measured: `C:\Users\…\AppData\Local\Temp\3IfVAEDHBEkVmiFuhcTB1PVHZjb\Gewu.exe`.
 * So `dirname(app.getPath('exe'))` is that temp directory, and a config file placed
 * beside the exe the user actually double-clicked is never found.
 *
 * The failure is quiet: the app starts, shows a login page, and talks to the default
 * server instead of the configured one. Nothing on screen says why.
 *
 * electron-builder sets `PORTABLE_EXECUTABLE_DIR` to the real location for exactly
 * this reason, so it wins when present. Installed (NSIS) builds do not set it and
 * genuinely run from their install directory, where `exeDir` is correct.
 *
 * **This was found by packaging and double-clicking, not by any test** — in
 * development `app.getPath('exe')` is the Electron binary in `node_modules`, and the
 * config file is not there either, so the fallback looked like normal behaviour.
 */
export function configDirectory(
  env: Record<string, string | undefined>,
  exeDir: string,
): string {
  return env['PORTABLE_EXECUTABLE_DIR']?.trim() || exeDir;
}

export function serverAddress(
  env: Record<string, string | undefined>,
  configFile: () => string | null,
): string {
  const fromEnv = env['GEWU_SERVER']?.trim();
  if (fromEnv) return strip(fromEnv);

  const raw = configFile();
  if (raw) {
    try {
      const parsed: unknown = JSON.parse(raw);
      const value =
        typeof parsed === 'object' && parsed !== null
          ? (parsed as { server?: unknown }).server
          : undefined;
      if (typeof value === 'string' && value.trim()) return strip(value.trim());
    } catch {
      // 配置文件坏了就当没有 —— 一个打错的 JSON 不该让应用开不起来,
      // 而回落到默认值至少能让人看见登录页并意识到连的是哪台。
    }
  }

  return DEFAULT_SERVER;
}

function strip(url: string): string {
  return url.endsWith('/') ? url.slice(0, -1) : url;
}
