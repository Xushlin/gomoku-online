import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { describe, expect, it } from 'vitest';
import { GAME_REGISTRY } from './index';
import { emblemNode, type EmblemShape } from './game-emblem';
import { GameEmblem } from './emblem/game-emblem';

/**
 * 纹章的两件事:**每个棋种都有一张能画出来的形状表**,以及**渲染器独占作图系统**。
 *
 * 走查的数据全部来自 `GAME_REGISTRY` —— 本仓库修过五次「手写清单冒充注册表」,
 * 而一份手抄的棋种名单会在第十个棋种落地那天悄悄地不覆盖它。
 */

@Component({
  standalone: true,
  imports: [GameEmblem],
  template: `<app-game-emblem [shapes]="shapes()" [size]="size()" />`,
})
class Host {
  readonly shapes = signal<readonly EmblemShape[]>([]);
  readonly size = signal(30);
}

function mount(shapes: readonly EmblemShape[], size = 30) {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({ imports: [Host] });
  const fixture = TestBed.createComponent(Host);
  fixture.componentInstance.shapes.set(shapes);
  fixture.componentInstance.size.set(size);
  fixture.detectChanges();
  return fixture;
}

const svgOf = (f: ReturnType<typeof mount>): SVGSVGElement =>
  f.nativeElement.querySelector('svg');

describe('game emblems', () => {
  it('every registered game declares a non-empty emblem', () => {
    expect(GAME_REGISTRY.length).toBeGreaterThan(0);

    for (const game of GAME_REGISTRY) {
      expect(game.emblem.length, `${game.key} has no shapes`).toBeGreaterThan(0);
    }
  });

  it('every shape of every game maps to an element the renderer draws', () => {
    // `emblemNode` 的 default 分支参数是 `never`,所以漏掉一种形状**编译不过**。
    // 这一条补的是另一半:形状表里的每一项都真的落到一个元素上。
    let checked = 0;

    for (const game of GAME_REGISTRY) {
      const fixture = mount(game.emblem);
      const drawn = svgOf(fixture).querySelectorAll('line, circle, rect, text, path');

      expect(drawn.length, `${game.key} drew ${drawn.length} of ${game.emblem.length}`).toBe(
        game.emblem.length,
      );
      checked += game.emblem.length;
    }

    // 正面控制:一条只走空表的遍历会在上面全绿。
    expect(checked).toBeGreaterThan(20);
  });

  it('the sample covers more than one shape kind, or the walk proves nothing', () => {
    // 「每一项都落到元素上」在只有一种形状时几乎是恒真的。
    const kinds = new Set(GAME_REGISTRY.flatMap((g) => g.emblem.map((s) => s.k)));

    expect(kinds.size).toBeGreaterThan(2);
    expect(kinds).toContain('r');
    expect(kinds).toContain('t'); // 帥 / 王 —— 唯一走 <text> 的两个
  });

  it('the renderer owns the grid, the stroke and the caps — not the shapes', () => {
    // 这是「十个纹章读起来像一套」的机制。任何一张形状表都无权指定这些。
    const svg = svgOf(mount(GAME_REGISTRY[0].emblem));

    expect(svg.getAttribute('viewBox')).toBe('0 0 24 24');
    expect(svg.getAttribute('stroke-width')).toBe('1.6');
    expect(svg.getAttribute('stroke-linecap')).toBe('round');
    expect(svg.getAttribute('stroke')).toBe('currentColor');
  });

  it('only the box scales, never the grid', () => {
    const small = svgOf(mount(GAME_REGISTRY[0].emblem, 26));
    const large = svgOf(mount(GAME_REGISTRY[0].emblem, 34));

    expect(small.getAttribute('width')).toBe('26');
    expect(large.getAttribute('width')).toBe('34');
    // 少了这一条,一个把 size 写进 viewBox 的实现在上面两条下也是绿的。
    expect(small.getAttribute('viewBox')).toBe(large.getAttribute('viewBox'));
  });

  it('no shape table carries a literal colour', () => {
    // 颜色一律来自 currentColor,由牌面给出身份色。一个写死颜色的纹章在换主题时不跟着变。
    const json = JSON.stringify(GAME_REGISTRY.map((g) => g.emblem));

    expect(json).not.toMatch(/#[0-9a-fA-F]{3,8}/);
    expect(json).not.toMatch(/rgba?\(/);
  });

  it('no glyph is sized past the measured limit for its container', () => {
    /*
     * 这一条钉的是**真的发生过的那个回归**:帥 原来是 9.5、王 原来是 9,两个都撑破了容器,
     * 而用户在截图里一眼看到了。
     *
     * 限制是量出来的,不是估的 —— 把字渲进 canvas 采样墨迹像素:
     *   - 最紧的容器是象棋那个描边的内圈,半径 7、线宽 1.6,所以墨迹可用半径是 **6.2**;
     *   - 帥 在 7.5 时墨迹半对角 **5.36**(过),在 9.5 时 **6.79**(不过)。
     *
     * **而这条断言弱于它想守的东西,这一点必须说清:** 「字形合不合容器」只有在真浏览器里
     * 量墨迹才能回答,而 jsdom 没有布局、没有 getBBox、也画不了 SVG 文字。所以这里守的是
     * 那个**字号上界** —— 它会在有人把字号调回去时变红,但它不会在有人把圆圈改小时变红。
     * 后者需要一次浏览器里的重新测量,而规则记在 `game-emblem.ts` 的注释里。
     */
    const GLYPH_LIMIT = 7.5;
    const glyphs = GAME_REGISTRY.flatMap((g) =>
      g.emblem.filter((s) => s.k === 't').map((s) => ({ key: g.key, size: (s as { c: number }).c })),
    );

    expect(glyphs.length, 'no glyph shapes — this check would pass vacuously').toBeGreaterThan(0);
    for (const { key, size } of glyphs) {
      expect(size, `${key} glyph is ${size}, past the measured ${GLYPH_LIMIT}`).toBeLessThanOrEqual(
        GLYPH_LIMIT,
      );
    }
  });

  it('no glyph sits on top of a filled shape', () => {
    // 猜成语的中间那格原来是填充的,而 `?` 也是 currentColor —— **它是隐形的**。
    // 同色画在同色上,不会报错、不会变红,只会看不见。
    for (const game of GAME_REGISTRY) {
      const hasGlyph = game.emblem.some((s) => s.k === 't');
      if (!hasGlyph) continue;
      const filled = game.emblem.filter((s) => s.k !== 't' && 'f' in s && s.f === 1);
      expect(filled, `${game.key} draws a glyph over ${filled.length} filled shape(s)`).toEqual([]);
    }
  });

  it('a filled shape asks for currentColor, an unfilled one asks for none', () => {
    // 两个方向都要:一个恒填充或恒不填充的实现只会在一边红。
    expect(emblemNode({ k: 'c', a: 1, b: 1, c: 1, f: 1 }).attrs['fill']).toBe('currentColor');
    expect(emblemNode({ k: 'c', a: 1, b: 1, c: 1 }).attrs['fill']).toBe('none');
  });

  it('the emblem is decorative, because the game name is rendered beside it', () => {
    const svg = svgOf(mount(GAME_REGISTRY[0].emblem));

    expect(svg.getAttribute('aria-hidden')).toBe('true');
    expect(svg.getAttribute('focusable')).toBe('false');
  });
});
