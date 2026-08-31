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
