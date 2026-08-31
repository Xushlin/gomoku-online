import { join, resolve } from 'node:path';
import { describe, expect, it } from 'vitest';
import { isInsideRoot, resolveAsset } from './resolve-asset';

const ROOT = resolve('C:', 'app', 'dist', 'gewu-web', 'browser');
const ENTRY = join(ROOT, 'index.html');

/** Only these two files exist — used where the fallback is the thing under test. */
const sparse = new Set([ENTRY, join(ROOT, 'main-ABC123.js')]);

/**
 * Every file exists.
 *
 * **The traversal group must use this one**, and that is the whole lesson of this
 * file: the first draft used the sparse predicate, so an escaping path fell back to
 * `index.html` *because the fake filesystem said it was missing*, not because it was
 * refused. Deleting the boundary check left all ten tests green.
 */
const everything = () => true;

const at = (url: string, exists: (p: string) => boolean = (p) => sparse.has(p)) =>
  resolveAsset(ROOT, url, exists);

describe('resolveAsset', () => {
  describe('serving the bundle', () => {
    it('returns a real file when the request names one', () => {
      const got = at('app://gewu/main-ABC123.js');

      expect(got.path).toBe(join(ROOT, 'main-ABC123.js'));
      expect(got.isFallback).toBe(false);
    });

    it('serves the entry at the root', () => {
      expect(at('app://gewu/').path).toBe(ENTRY);
    });
  });

  describe('deep links are routes, not files', () => {
    /**
     * 应用是**路径路由**。`/g/idiom-guess/levels/0` 在磁盘上不存在,而它是一条真路由 ——
     * 这里返回 404 会让每一个深链和每一次刷新都白屏。
     */
    it('falls back to index.html for a path that is a route', () => {
      const got = at('app://gewu/g/idiom-guess/levels/0');

      expect(got.path).toBe(ENTRY);
      expect(got.isFallback).toBe(true);
    });
  });

  describe('nothing escapes the bundle', () => {
    const SHAPES = [
      'app://gewu/../../../../Windows/System32/config/SAM',
      'app://gewu/..%2f..%2f..%2fWindows%2fwin.ini',
      'app://gewu/assets/../../../../secret.txt',
      'app://gewu/C:/Windows/win.ini',
      'app://gewu//server/share/x',
    ];

    /**
     * **`everything` 是关键。** 用稀疏的 `exists` 时,越界路径会因为「文件不存在」
     * 回落到入口 —— 断言绿,而边界检查删掉照样绿。这里让**每个文件都存在**,
     * 于是唯一能阻止它交出越界路径的东西,就是代码里那道边界。
     */
    it.each(SHAPES)('%s stays inside the bundle', (url) => {
      const got = at(url, everything);

      expect(isInsideRoot(ROOT, got.path) || got.path === ENTRY, `resolved to ${got.path}`).toBe(
        true,
      );
    });

    /**
     * **与上面那组同时存在。** 少了这一条,一个「一律回落到入口」的实现
     * 也能通过整组穿越测试 —— 而那样的壳一个 chunk 都加载不出来。
     */
    it('still serves a legitimate file, so "refuse everything" does not pass', () => {
      expect(at('app://gewu/main-ABC123.js').isFallback).toBe(false);
    });
  });

  describe('malformed input does not crash the window', () => {
    it('falls back when the url will not parse', () => {
      expect(at('not a url at all').path).toBe(ENTRY);
    });

    it('falls back on a bad percent-escape', () => {
      expect(at('app://gewu/%E0%A4%A').path).toBe(ENTRY);
    });
  });
});

/**
 * 边界检查本身。
 *
 * 单独测,是因为**经由 `resolveAsset` 递不进去一个真正越界的路径** —— `join` 已经把
 * 一切夹在 root 里(实测五种形状全部如此)。只走那扇门的测试等于什么都没验,
 * 而这个文件的第一版正是那样:检查删掉,十条全绿。
 */
describe('isInsideRoot', () => {
  it('accepts a path within the bundle', () => {
    expect(isInsideRoot(ROOT, join(ROOT, 'assets', 'x.png'))).toBe(true);
  });

  it('rejects a path that really does escape', () => {
    // `resolve` 会这样收场 —— 把实现里的 `join` 换成 `resolve` 就会产生这个值。
    expect(isInsideRoot(ROOT, resolve('C:', 'Windows', 'win.ini'))).toBe(false);
  });

  it('rejects the sibling directory that shares a prefix', () => {
    // `<root>-evil` 以 `<root>` 开头 —— 少了分隔符就会被当成在里面。
    expect(isInsideRoot(ROOT, ROOT + '-evil')).toBe(false);
  });
});
