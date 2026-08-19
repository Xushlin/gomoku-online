#!/usr/bin/env node
/*
 * 皮肤 CSS 变量集的完整性检查 —— **一个 Node 脚本,而不是一条 vitest 断言,而这是量出来的。**
 *
 * 想要的机制是:取默认皮肤块定义的变量集作基准,断言每个皮肤块定义的是同一个集合。漏一个会红,
 * 多一个拼错的也会红,而没有人需要记得去改一份抄在规格里的名单 —— `web-board-skins` 原来就抄了
 * 11 个,而 `--xq-piece-bg` / `--xq-red` / `--xq-black` 自 `add-web-xiangqi` 起就在每个块里、
 * 却从来不在那份名单上。
 *
 * 它进不了 vitest,是试过三条路之后的结论:
 *
 *   - `import css from '...css?raw'` —— 测试构建把它当 CSS 处理,先报 `Could not resolve
 *     "/cards/spade.png"`,把路径改成相对之后报 `No loader is configured for ".png"`,
 *     两次都是整个构建失败;
 *   - 去掉 url() 之后同一个 import 的默认导出是 `[]`(Angular 的 CSS-in-JS 壳),不是字符串;
 *   - `import.meta.glob(..., { query: '?raw' })` 同上;
 *   - `node:fs` 在 spec 的 tsconfig 里没有类型(`TS2307`,仓库没装 @types/node)。
 *
 * 于是它跑在 `npm run lint` 里 —— CI 已经跑 lint,而在 Node 里读文件不需要任何一个打包器同意。
 *
 * 皮肤名单是**推出来的**(从 service 的 register 调用),不是抄的:抄一份名单再写一个走名单的
 * 检查,正是这个仓库在后端修过三次的那个缺陷。
 */
import { readFileSync } from 'node:fs';

const CSS = 'src/styles/board-skins.css';
const SERVICE = 'src/app/core/theme/board-skin.service.ts';
const CARD_CSS = 'src/app/games/doudizhu/card-table/card-table.css';
const DEFAULT_SKIN = 'wood';

/**
 * 注释先剥掉 —— **第一版没剥,于是它红在我自己写的散文上**:classic 块的注释里有一句
 * 「NOT --color-surface: this skin paints the board...」,而 `--name:` 的模式认不出散文与声明的
 * 区别。`generalize-match-seats` 的源码级断言踩过同一个坑,记的也是同一句:检查要对注释视而不见。
 */
const stripComments = (text) => text.replace(/\/\*[\s\S]*?\*\//g, '');

const css = stripComments(readFileSync(CSS, 'utf8'));
const service = readFileSync(SERVICE, 'utf8');
const cardCss = stripComments(readFileSync(CARD_CSS, 'utf8'));

const errors = [];

/** 从 service 的 `this.register('<name>', ...)` 推出内置皮肤名单。 */
const skins = [...service.matchAll(/this\.register\('([a-z0-9-]+)'/g)].map((m) => m[1]);
if (skins.length < 2) {
  errors.push(`${SERVICE}: found ${skins.length} register() calls; expected the built-in skins`);
}
if (!skins.includes(DEFAULT_SKIN)) {
  errors.push(`${SERVICE}: default skin '${DEFAULT_SKIN}' is not registered`);
}

/** 一个选择器块里定义的全部自定义属性名。 */
function varsIn(selector) {
  const start = css.indexOf(`${selector} {`);
  if (start < 0) return null;
  const open = css.indexOf('{', start);
  let depth = 0;
  let end = -1;
  for (let i = open; i < css.length; i++) {
    if (css[i] === '{') depth++;
    else if (css[i] === '}' && --depth === 0) {
      end = i;
      break;
    }
  }
  const body = css.slice(open, end);
  return new Set([...body.matchAll(/(--[a-z0-9-]+)\s*:/g)].map((m) => m[1]));
}

const baseline = varsIn(`[data-board-skin='${DEFAULT_SKIN}']`);
if (!baseline) {
  errors.push(`${CSS}: no block for the default skin '${DEFAULT_SKIN}'`);
} else {
  // 基准本身要有底:一个空块会让下面每条比较都空过。
  const REQUIRED = [
    '--board-bg-color',
    '--stone-black-fill',
    '--xq-red',
    '--card-face',
    '--card-red',
    '--card-back',
    '--felt-bg',
    '--felt-text',
    '--last-move-ring',
  ];
  for (const name of REQUIRED) {
    if (!baseline.has(name)) errors.push(`${CSS}: baseline skin '${DEFAULT_SKIN}' lacks ${name}`);
  }

  for (const skin of skins) {
    const actual = varsIn(`[data-board-skin='${skin}']`);
    if (!actual) {
      errors.push(`${CSS}: skin '${skin}' is registered but has no block`);
      continue;
    }
    for (const name of baseline) {
      if (!actual.has(name)) errors.push(`${CSS}: skin '${skin}' is missing ${name}`);
    }
    for (const name of actual) {
      if (!baseline.has(name)) {
        errors.push(`${CSS}: skin '${skin}' defines ${name}, which no other skin has (typo?)`);
      }
    }

    // `.dark` override 只允许重定义基准里已有的变量。
    const dark = varsIn(`[data-board-skin='${skin}'].dark`);
    if (dark) {
      for (const name of dark) {
        if (!baseline.has(name)) {
          errors.push(`${CSS}: '${skin}'.dark introduces ${name}, absent from the base block`);
        }
      }
    }
  }
}

/*
 * 花色图的路径由组件绑成 `--ddz-pip`(见 card-art.ts —— 同一个 loader 限制)。
 * 这里守的是反面:样式表里 MUST NOT 再写一份路径,否则同一件事有两个来源。
 */
if (!cardCss.includes('var(--ddz-pip)')) {
  errors.push(`${CARD_CSS}: nothing consumes var(--ddz-pip)`);
}
for (const m of cardCss.matchAll(/^\s*--ddz-pip:\s*url\(/gm)) {
  errors.push(`${CARD_CSS}:${m.index}: hard-codes a pip path; the component binds it`);
}

/*
 * **发牌动画的横向散开量不能引用带百分比的变量,而这条是量出来的。**
 * 第一版用了 `--ddz-step`(里面有个 `100%`):百分比在**用它的地方**解算,在 `margin-left`
 * 里它对着容器,而在 `transform: translate()` 里它对着元素自己 —— `(34px - 34px) / 16 = 0`,
 * 于是整段横向位移静静地变成 0,牌只往下掉不散开,而动画照样在放。
 * jsdom 没有排版引擎,量不到这件事;所以钉的是源码:那段 keyframe 里不许出现 `--ddz-step`。
 */
const deal = cardCss.match(/@keyframes ddz-deal[\s\S]*?transform:[^;]+;/);
if (!deal) {
  errors.push(`${CARD_CSS}: no @keyframes ddz-deal`);
} else if (deal[0].includes('--ddz-step')) {
  errors.push(
    `${CARD_CSS}: @keyframes ddz-deal uses --ddz-step, whose 100% resolves against the card itself (spread becomes 0)`,
  );
}

if (errors.length) {
  console.error(`style check failed (${errors.length}):`);
  for (const e of errors) console.error(`  - ${e}`);
  process.exit(1);
}
console.log(
  `style check: ${skins.length} skins x ${baseline.size} variables, card art bound not hard-coded`,
);
