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
import { readdirSync, readFileSync } from 'node:fs';

const CSS = 'src/styles/board-skins.css';
const SERVICE = 'src/app/core/theme/board-skin.service.ts';
const CARD_CSS = 'src/app/games/cards/card-table/card-table.css';
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
 * **牌桌的样式表里不许有 `url()`。** 花色是自绘的 SVG path(`card-art.ts`,`fill="currentColor"`),
 * 所以这份样式表不需要任何素材;而这条断言守的是它不再需要 —— 上一版为了绕开「测试构建没有
 * .png 的 loader」,带着一条 `--ddz-pip` 绑定、一条惰性 glob 的存在性测试、和一条「绑定没被清洗
 * 掉」的断言。三样东西一起删掉了,而这一行是它们不会悄悄回来的理由。
 */
for (const m of cardCss.matchAll(/url\(/g)) {
  errors.push(`${CARD_CSS}:${m.index}: references an asset; suit shapes are SVG paths in card-art.ts`);
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

/*
 * 牌桌的 `:host` 必须占满宽度。房间页的容器是 `flex-col items-center`,而 `items-center` 让子元素
 * shrink-to-fit —— 少了这一行,整张桌子按内容收窄(量到 felt 从 ~730px 变成 ~430px)。
 * 这一条是因为我为了压 4 kB 的预算把它删过一次,而只有截图看得见。
 */
if (!/:host\s*\{[^}]*width:\s*100%/.test(cardCss)) {
  errors.push(`${CARD_CSS}: :host must set width: 100% (the room page centres its children)`);
}

/* ============================================================================
 * 主题 token 的完整性 + 「装饰层是加上去的,没有改动现有主题」
 *
 * 两条检查,回答两个不同的问题:
 *
 *   1. **每套主题都声明了每一个 token 吗** —— 名单从 `@theme` 与 tokens.css 的
 *      选择器推导,MUST NOT 手写主题名。`ThemeService.validateTokens` 也查这个,
 *      但它只在**运行时 warn**,而一个 warn 在 CI 里不会让任何东西变红。
 *
 *   2. **装饰层对现有三套主题是中性的吗** —— 这是 extend-theme-tokens 唯一的成败
 *      判据。中性的定义在 theme.tokens.ts 的 NEUTRAL_DECORATION 里,而这里**从那个
 *      文件解析**,不重抄一份:抄一份中性值再拿它去校验中性,等于自己给自己打分。
 *
 * 每一条都带一个「否则会空过」的守卫。一个解析不到东西的检查和一个通过的检查,
 * 输出一模一样。
 * ========================================================================== */
const TOKENS_CSS = 'src/styles/tokens.css';
const TAILWIND_CSS = 'src/styles/tailwind.css';
const CONTRACT = 'src/app/core/theme/theme.tokens.ts';

/*
 * 这三套主题在装饰层之前就存在,所以它们必须保持中性。名单是**有意手写**的,
 * 而这是本仓库那条「手写名单冒充注册表」规则的一个例外 —— 它记的不是「有哪些主题」
 * (那个从 CSS 推导),而是「哪些主题早于这一层」,那是一个历史事实,不会因为
 * 加一套主题而变。新主题不该进这个名单,而 qq-game 正是第一个。
 */
const PRE_EXISTING_THEMES = ['material', 'system', 'ink'];

const tokensCss = stripComments(readFileSync(TOKENS_CSS, 'utf8'));
const tailwindCss = stripComments(readFileSync(TAILWIND_CSS, 'utf8'));
const contract = readFileSync(CONTRACT, 'utf8');

/** 每个 [data-theme] 块声明的变量,按 `主题|模式` 索引。 */
const themeBlocks = new Map();
for (const m of tokensCss.matchAll(/\[data-theme='([a-z-]+)'\](\.dark)?\s*\{([^}]*)\}/g)) {
  const vars = new Map();
  for (const v of m[3].matchAll(/(--[a-z-]+):\s*([^;]+);/g)) vars.set(v[1], v[2].trim());
  themeBlocks.set(`${m[1]}|${m[2] ? 'dark' : 'light'}`, vars);
}
const themeNames = [...new Set([...themeBlocks.keys()].map((k) => k.split('|')[0]))];
if (themeNames.length === 0) {
  errors.push(`${TOKENS_CSS}: no [data-theme] blocks found — every check below would pass vacuously`);
}

/*
 * 契约 = `@theme` 里声明的那一套。它是「哪些 token 有对应 utility」的真源,
 * 所以一套主题该声明的正是它。
 */
const declared = new Set();
const themeAt = tailwindCss.match(/@theme\s*\{([^}]*)\}/);
if (!themeAt) {
  errors.push(`${TAILWIND_CSS}: no @theme block — cannot derive the token contract`);
} else {
  for (const v of themeAt[1].matchAll(/(--[a-z-]+):/g)) declared.add(v[1]);
}
if (declared.size === 0) {
  errors.push(`${TAILWIND_CSS}: @theme declares 0 tokens — the parity check would pass vacuously`);
}

for (const theme of themeNames) {
  const light = themeBlocks.get(`${theme}|light`) ?? new Map();
  const dark = themeBlocks.get(`${theme}|dark`) ?? new Map();
  /*
   * 判据是**并集**,不是逐块相等:`[data-theme='x']` 在暗色下同样匹配,所以
   * `.dark` 只需覆盖会变的那些(圆角本来就是这个约定)。要求每块都全列,
   * 会逼出一堆重复,而重复迟早分叉。
   */
  const union = new Set([...light.keys(), ...dark.keys()]);
  for (const name of declared) {
    if (!union.has(name)) errors.push(`${TOKENS_CSS}: theme '${theme}' never declares ${name}`);
  }
  for (const name of union) {
    if (!declared.has(name)) {
      errors.push(
        `${TOKENS_CSS}: theme '${theme}' declares ${name}, which @theme does not — ` +
          `a typo here is invisible, because no utility reads it`,
      );
    }
  }
}

/* 中性值从契约文件解析 —— 一份真源。 */
const neutralBlock = contract.match(/NEUTRAL_DECORATION\s*=\s*\{([\s\S]*?)\n\} as const;/);
if (!neutralBlock) {
  errors.push(`${CONTRACT}: NEUTRAL_DECORATION not found — the neutrality check would pass vacuously`);
} else {
  const stems = {
    surfaces: 'surface',
    controls: 'control',
    shadows: 'shadow',
    grounds: 'ground',
  };
  const cssName = (group, field) => {
    const stem = stems[group];
    if (!stem) return null;
    if (group === 'accents' && field === 'color') return '--accent';
    return `--${stem}-${field.replace(/[A-Z]/g, (c) => '-' + c.toLowerCase())}`;
  };
  const neutral = new Map();
  for (const g of neutralBlock[1].matchAll(/(\w+):\s*\{([^}]*)\}/g)) {
    for (const f of g[2].matchAll(/(\w+):\s*'([^']*)'/g)) {
      const name = cssName(g[1], f[1]);
      if (name) neutral.set(name, f[2]);
    }
  }
  if (neutral.size === 0) {
    errors.push(`${CONTRACT}: parsed 0 neutral values — the neutrality check would pass vacuously`);
  }

  for (const theme of PRE_EXISTING_THEMES) {
    const light = themeBlocks.get(`${theme}|light`);
    if (!light) {
      errors.push(`${TOKENS_CSS}: '${theme}' is listed as pre-existing but has no light block`);
      continue;
    }
    for (const [name, want] of neutral) {
      const got = light.get(name);
      if (got !== want) {
        errors.push(
          `${TOKENS_CSS}: '${theme}' must keep ${name} neutral (${want}) but has ${got ?? '(missing)'} — ` +
            `extending the token vocabulary must not change how an existing theme paints`,
        );
      }
    }
    /* --radius-control 没有绝对中性值:它中性 <=> 等于 --radius-card。 */
    const control = light.get('--radius-control');
    const card = light.get('--radius-card');
    if (control !== card) {
      errors.push(
        `${TOKENS_CSS}: '${theme}' has --radius-control ${control} != --radius-card ${card}; ` +
          `every control used the card radius before that token existed`,
      );
    }
  }
}

/* 角色 utility 里不许有色值字面量 —— 与「不许硬编码花色路径」同族。 */
const roles = [...tailwindCss.matchAll(/@utility\s+([a-z-]+)\s*\{([^}]*)\}/g)];
if (roles.length === 0) {
  errors.push(`${TAILWIND_CSS}: no @utility roles found — the literal-colour check would pass vacuously`);
}
for (const [, name, body] of roles) {
  const literal = body.match(/#[0-9a-fA-F]{3,8}\b|\brgba?\(|\bhsla?\(/);
  if (literal) {
    errors.push(
      `${TAILWIND_CSS}: @utility ${name} hardcodes ${literal[0]}; a role must only reference var(--…)`,
    );
  }
}

/*
 * `shadow-elevated` MUST NOT 作为 class 出现在模板里 —— 而这是量出来的,不是洁癖。
 *
 * Tailwind v4 的 `shadow-*` utility 不发 `box-shadow: var(--shadow-elevated)`,它走
 * `@property` 注册的 `--tw-shadow`,并在**构建期把 `@theme` 的占位值内联进去**。于是
 * 运行时的 `[data-theme]` 覆盖永远到不了它。
 *
 * 后果量过:改这一层之前,六种(主题 x 明暗)组合声明了**六个不同的** --shadow-elevated,
 * 而全部画出同一个 `rgba(0,0,0,0.12) 0 4px 12px` —— material 浅色那份占位值。三套主题
 * 各自写的阴影从主题系统上线起就没生效过,包括 ink 那句「阴影重,活字是有厚度的」。
 *
 * 角色 utility 直接写 `var(--shadow-elevated)`,所以它们是活的。任何一处退回用
 * `shadow-elevated` 这个 class 的地方,都会**静默地**回到那个死值。
 */
const templateFiles = [];
{
  const walk = (dir) => {
    for (const entry of readdirSync(dir, { withFileTypes: true })) {
      const full = `${dir}/${entry.name}`;
      if (entry.isDirectory()) walk(full);
      else if (/\.(html|ts)$/.test(entry.name) && !entry.name.endsWith('.spec.ts')) templateFiles.push(full);
    }
  };
  walk('src/app');
}
if (templateFiles.length === 0) {
  errors.push('src/app: walked 0 template files — the shadow-elevated check would pass vacuously');
}
for (const file of templateFiles) {
  const text = readFileSync(file, 'utf8');
  for (const m of text.matchAll(/class="([^"]*shadow-elevated[^"]*)"/g)) {
    errors.push(
      `${file}: uses the shadow-elevated class, which is frozen to the @theme placeholder ` +
        `and ignores [data-theme] — use a role utility (panel) instead`,
    );
  }
}

/*
 * 前景色对比度:量**组件真正写出来的那一对**,而不是量 token 的名字。
 *
 * 上一版校验取的是「每个前景 token 落在它自己的面上」,而 31 个 `control-primary`
 * 按钮里有 25 个把 `--color-bg`(一个**背景** token)当字色用。那个配对从来不在校验
 * 的定义域里,所以校验一直是绿的 —— 而浏览器里量到 qq-game 浅色 3.57、暗色 **1.58**
 * (近黑的青落在深红上)。**检查量的是对的东西,应用做的是另一件事。**
 *
 * 所以配对 SHALL 从 `class` 属性推导,两条规则:
 *
 *   1. 同一个状态里,任一候选前景 x 任一候选填充 >= 4.5:1,渐变取**最差那一档**;
 *   2. 一个状态里 MUST NOT 出现两个不同的前景色 utility —— 它们同特异性,谁画取决于
 *      样式表顺序。**量过:回放页选中的速度胶囊上 `text-text` 赢了,作者写的
 *      `text-bg` 一次都没生效过**(2.07:1 的标签,而模板上那个 class 看起来是对的)。
 *      一个被静默覆盖的 class 和一个生效的长得一模一样,所以这条规则比「量那一对」
 *      更早:先让状态里只有一个前景,再谈它够不够亮。
 */
const MIN_RATIO = 4.5;

/** `@theme` 里的颜色 token 名(去掉 `--color-` 前缀)—— 用它把 `text-danger` 和 `text-sm` 分开。 */
const colourTokens = new Set([...declared].map((n) => /^--color-(.+)$/.exec(n)?.[1]).filter(Boolean));
if (colourTokens.size === 0) {
  errors.push(`${TAILWIND_CSS}: @theme declares 0 --color-* tokens — the pairing check would pass vacuously`);
}

/*
 * 每个角色的填充从**它自己的定义**里读:`background-color: var(--x)` 加可选的
 * `background-image: var(--y)`。没有自己填充的角色(`cell` 只有边)读不出背景,
 * 所以不参与 —— 它身后是什么由父级决定,静态推不出来。
 */
const fillOf = new Map();
for (const [, name, body] of roles) {
  const colour = body.match(/background-color:\s*var\((--[a-z-]+)\)/);
  if (!colour) continue;
  const image = body.match(/background-image:\s*var\((--[a-z-]+)\)/);
  fillOf.set(name, { colour: colour[1], image: image ? image[1] : null });
}
/* `bg-<token>` 是同一件事的另一种写法(胶囊选中态用的是它,不是角色)。 */
for (const t of colourTokens) fillOf.set(`bg-${t}`, { colour: `--color-${t}`, image: null });

const srgb = (c) => (c <= 0.04045 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4);
function luminance(hex) {
  let h = hex.replace('#', '');
  if (h.length === 3) h = [...h].map((c) => c + c).join('');
  const [r, g, b] = [0, 2, 4].map((i) => parseInt(h.slice(i, i + 2), 16) / 255);
  return 0.2126 * srgb(r) + 0.7152 * srgb(g) + 0.0722 * srgb(b);
}
function contrast(a, b) {
  const [hi, lo] = [luminance(a), luminance(b)].sort((x, y) => y - x);
  return (hi + 0.05) / (lo + 0.05);
}
/** 主题里 token 的最终值,跟着 `var(--other)` 走(`--color-well: var(--color-bg)` 就是这样写的)。 */
function resolveVar(vars, name, depth = 0) {
  const raw = vars.get(name);
  if (!raw || depth > 4) return null;
  const ref = raw.match(/^var\((--[a-z-]+)\)$/);
  return ref ? resolveVar(vars, ref[1], depth + 1) : raw;
}
/** 一套主题 x 一种模式下生效的全部变量:暗色块只覆盖会变的那些。 */
function varsFor(theme, mode) {
  const merged = new Map(themeBlocks.get(`${theme}|light`) ?? []);
  if (mode === 'dark') for (const [k, v] of themeBlocks.get(`${theme}|dark`) ?? []) merged.set(k, v);
  return merged;
}

/*
 * 元素级解析:静态 `class="…"` 加上同一个标签里的 `[class.X]="expr"`。
 * 状态 = 静态 ∪ (某一个 guard 的那些 class);两个**不同** guard 同时为真的组合
 * 不建模 —— 已知的空档,写在这里而不是假装它不存在。
 */
const tagRe = /<[a-zA-Z][\w-]*((?:[^>"']|"[^"]*"|'[^']*')*)>/g;
let pairsMeasured = 0;
let opaqueClassBindings = 0;
const seenPairs = new Set();

for (const file of templateFiles) {
  const text = readFileSync(file, 'utf8');
  opaqueClassBindings += [...text.matchAll(/\[class\]=/g)].length;

  for (const tag of text.matchAll(tagRe)) {
    const attrs = tag[1];
    const staticClasses = (attrs.match(/(?:^|\s)class="([^"]*)"/)?.[1] ?? '').split(/\s+/).filter(Boolean);
    const guards = new Map();
    for (const g of attrs.matchAll(/\[class\.([A-Za-z0-9_-]+)\]="([^"]*)"/g)) {
      if (!guards.has(g[2])) guards.set(g[2], []);
      guards.get(g[2]).push(g[1]);
    }

    const states = [staticClasses, ...[...guards.values()].map((extra) => [...staticClasses, ...extra])];
    for (const state of states) {
      const fgs = [...new Set(state.filter((c) => /^text-/.test(c) && colourTokens.has(c.slice(5))))];
      if (fgs.length === 0) continue;
      if (fgs.length > 1) {
        errors.push(
          `${file}: one element can carry ${fgs.join(' + ')} at the same time — same specificity, so ` +
            `which one paints depends on stylesheet order (measured: text-text beat text-bg, and the ` +
            `text-bg was dead). Make the states mutually exclusive.`,
        );
        continue;
      }
      const fg = fgs[0].slice(5);
      const fills = [...new Set(state.filter((c) => fillOf.has(c)))];
      for (const fillClass of fills) {
        const { colour, image } = fillOf.get(fillClass);
        for (const theme of themeNames) {
          for (const mode of ['light', 'dark']) {
            const vars = varsFor(theme, mode);
            const label = resolveVar(vars, `--color-${fg}`);
            const flat = resolveVar(vars, colour);
            if (!label || !flat || !label.startsWith('#')) continue;
            const img = image ? resolveVar(vars, image) : null;
            const stops =
              img && img !== 'none' ? [...img.matchAll(/#[0-9a-fA-F]{3,8}/g)].map((m) => m[0]) : [];
            const candidates = (stops.length ? stops : [flat]).filter((c) => c.startsWith('#'));
            for (const stop of candidates) {
              pairsMeasured += 1;
              const ratio = contrast(label, stop);
              if (ratio < MIN_RATIO) {
                const key = `${fillClass}|${fgs[0]}|${theme}.${mode}`;
                if (seenPairs.has(key)) continue;
                seenPairs.add(key);
                errors.push(
                  `${theme}.${mode}: ${fgs[0]} on ${fillClass} measures ${ratio.toFixed(2)}:1 at stop ` +
                    `${stop} (needs ${MIN_RATIO}) — first seen in ${file}`,
                );
              }
            }
          }
        }
      }
    }
  }
}
if (pairsMeasured === 0) {
  errors.push('src/app: measured 0 foreground/fill pairs — the contrast check would pass vacuously');
}

if (errors.length) {
  console.error(`style check failed (${errors.length}):`);
  for (const e of errors) console.error(`  - ${e}`);
  process.exit(1);
}
console.log(
  `style check: ${skins.length} skins x ${baseline.size} skin variables, ` +
    `${themeNames.length} themes x ${declared.size} tokens, ${roles.length} roles, ` +
    `${pairsMeasured} fg/fill contrast readings` +
    `${opaqueClassBindings ? ` (${opaqueClassBindings} opaque [class] bindings not modelled)` : ''}` +
    `, card table asset-free`,
);
