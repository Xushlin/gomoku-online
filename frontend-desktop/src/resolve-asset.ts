import { join, normalize, resolve, sep } from 'node:path';

/**
 * Turns an `app://…` request into a file inside the built Angular app.
 *
 * **This function is the entire attack surface of the shell**, which is why it
 * takes no Electron types and touches no Electron API: it is a pure mapping from
 * a URL to a path, and it can be pinned without launching anything.
 *
 * ## Three layers stop a traversal, and only the first two do any work today
 *
 * That ordering was measured, not assumed, and it matters because the first
 * version of the tests here **passed with the boundary check deleted**:
 *
 * 1. `new URL(...).pathname` **already collapses `..`** — `app://x/../../y` arrives
 *    as `/y`. The dots never reach this code.
 * 2. `join(root, normalize(p))` **clamps**: `join` treats its second argument as
 *    relative, so even `/../../../../Windows/win.ini` lands on
 *    `<root>\Windows\win.ini`. Measured on all five shapes in the spec file.
 * 3. {@link isInsideRoot} — **unreachable while (2) holds**.
 *
 * Layer 3 stays because layer 2 is one edit away from vanishing: swapping `join`
 * for `resolve` looks like a tidy-up and makes **every one of those five shapes
 * escape** (`C:\Windows\win.ini`, `\\server\share\x`, …). The spec pins exactly
 * that: with the check present a `resolve`-based implementation is still safe,
 * and with it deleted the same mutation walks straight out of the bundle.
 *
 * ## And a fallback, which is not a security matter but a routing one
 *
 * The app uses path routing, so `/g/idiom-guess/levels/0` is a real route and not
 * a file. Returning 404 there would break every deep link and every reload.
 */

/** A request that resolved to something safe to serve. */
export interface ResolvedAsset {
  /** Absolute path on disk. */
  readonly path: string;
  /** True when the request did not name a real file and fell back to the SPA entry. */
  readonly isFallback: boolean;
}

/**
 * Is `candidate` inside `root`?
 *
 * Exported so it can be tested against a path that **really does** escape. Through
 * {@link resolveAsset} it cannot currently be handed one (see layer 2 above), so a
 * test that only went through that door would be asserting nothing — which is
 * precisely how the first draft of this file shipped a green, empty check.
 */
export function isInsideRoot(root: string, candidate: string): boolean {
  const base = resolve(root);
  const withSep = base.endsWith(sep) ? base : base + sep;
  return resolve(candidate).startsWith(withSep);
}

/**
 * @param root Absolute path of the built app (the directory holding `index.html`).
 * @param requestUrl The `app://…` URL the renderer asked for.
 * @param exists Predicate for "this file is on disk" — injected so the rules can be
 *   tested without a filesystem.
 */
export function resolveAsset(
  root: string,
  requestUrl: string,
  exists: (path: string) => boolean,
): ResolvedAsset {
  const entry = join(root, 'index.html');

  let pathname: string;
  try {
    pathname = new URL(requestUrl).pathname;
  } catch {
    // 一个连 URL 都不是的请求 —— 回落到入口,而不是抛。渲染进程里一个坏的相对地址
    // 不该让整个窗口白屏。
    return { path: entry, isFallback: true };
  }

  // 先解码再判断:`%2e%2e%2f` 与 `../` 必须走同一条路,否则检查的是编码而不是路径。
  let decoded: string;
  try {
    decoded = decodeURIComponent(pathname);
  } catch {
    return { path: entry, isFallback: true };
  }

  if (decoded === '/' || decoded === '') {
    return { path: entry, isFallback: true };
  }

  // `join`,不是 `resolve` —— 见上面第 2 层。改成 `resolve` 会让五种形状全部逃出去。
  const candidate = resolve(join(root, normalize(decoded)));

  if (!isInsideRoot(root, candidate)) {
    // 越界。**不报错,回落到入口** —— 报错会把「有人在探路」变成一个用户看得见的
    // 崩溃页,而回落既安全又什么都没泄露。
    return { path: entry, isFallback: true };
  }

  return exists(candidate)
    ? { path: candidate, isFallback: false }
    : // 磁盘上没有这个文件 —— 那它多半是一条前端路由(`/g/idiom-guess/levels/0`)。
      { path: entry, isFallback: true };
}
