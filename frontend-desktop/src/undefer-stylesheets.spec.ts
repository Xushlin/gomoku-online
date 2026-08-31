import { existsSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { describe, expect, it } from 'vitest';
import { hasDeferredStylesheet, undeferStylesheets } from './undefer-stylesheets';

/** The real thing Angular emits, copied from the build output. */
const REAL =
  '<link rel="stylesheet" href="styles-V3YRPR3K.css" media="print" onload="this.media=\'all\'">' +
  '<noscript><link rel="stylesheet" href="styles-V3YRPR3K.css"></noscript>';

describe('undeferStylesheets', () => {
  it('removes the print media and the inline handler', () => {
    const out = undeferStylesheets(REAL);

    expect(out).toContain('<link rel="stylesheet" href="styles-V3YRPR3K.css">');
    expect(out).not.toContain('media="print"');
    expect(out).not.toContain('onload=');
  });

  it('keeps the href, which is the whole point', () => {
    expect(undeferStylesheets(REAL)).toContain('styles-V3YRPR3K.css');
  });

  it('leaves the noscript copy alone', () => {
    // 它本来就无害 —— JS 开着时不会被解析。改它只会增加出错的面。
    expect(undeferStylesheets(REAL)).toContain('<noscript><link rel="stylesheet"');
  });

  it('is idempotent', () => {
    const once = undeferStylesheets(REAL);

    expect(undeferStylesheets(once)).toBe(once);
  });

  it('leaves html without the pattern untouched, byte for byte', () => {
    const plain = '<html><head><link rel="stylesheet" href="a.css"></head><body>hi</body></html>';

    expect(undeferStylesheets(plain)).toBe(plain);
  });

  it('does not touch a script tag that merely mentions media', () => {
    const tricky = '<script>const media="print";</script>';

    expect(undeferStylesheets(tricky)).toBe(tricky);
  });
});

describe('hasDeferredStylesheet', () => {
  it('spots the pattern', () => {
    expect(hasDeferredStylesheet(REAL)).toBe(true);
  });

  it('is false once undeferred', () => {
    expect(hasDeferredStylesheet(undeferStylesheets(REAL))).toBe(false);
  });
});

/**
 * **The positive control, and the reason the unit tests above are not enough.**
 *
 * Every assertion so far runs against a string I typed. If Angular changes what it
 * emits — a different attribute order, single quotes, an added `fetchpriority` — the
 * regex stops matching and **every test above still passes**, while the shipped app
 * loses all its styling again. Exactly how this bug reached the user.
 *
 * So this reads the **real build output** and asserts the transform actually bites.
 * It skips when there is no build present (CI installs but does not always build the
 * web app), and says so rather than passing quietly.
 */
describe('against the real build output', () => {
  const indexHtml = join(
    __dirname,
    '..',
    '..',
    'frontend-web',
    'dist',
    'gewu-web',
    'browser',
    'index.html',
  );

  it.skipIf(!existsSync(indexHtml))('the shipped index.html needs undeferring, and gets it', () => {
    const html = readFileSync(indexHtml, 'utf8');

    // 前一半是**对产物的断言**:Angular 仍然在发那个延迟技巧。它哪天不发了,
    // 这一条会红,而那正是该来看一眼的时刻 —— 而不是让转换悄悄变成空操作。
    expect(hasDeferredStylesheet(html), 'Angular no longer defers — re-check this transform').toBe(
      true,
    );

    const fixed = undeferStylesheets(html);

    expect(hasDeferredStylesheet(fixed)).toBe(false);
    expect(fixed).not.toContain('media="print"');
    // 样式表仍然被引着 —— 少了这一条,一个把整个 link 删掉的实现也能通过。
    expect(fixed).toMatch(/<link rel="stylesheet" href="styles-[^"]+\.css">/);
  });
});
