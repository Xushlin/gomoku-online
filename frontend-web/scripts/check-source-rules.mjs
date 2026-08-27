/*
 * Source-level rules that no type or test can express.
 *
 * These are the ones where **forgetting looks exactly like remembering**: the
 * code compiles, the tests pass, and the defect only shows up on one path in a
 * real browser. They live at lint time, next to `check-styles.mjs`, because
 * they read source text rather than run it.
 */
import { readdirSync, readFileSync } from 'node:fs';

const errors = [];

/*
 * 房间页里所有 `router.navigate*` 只许出现在 `leaveTo` 里。
 *
 * `leaveTo` 先置「正在退出」再导航,而 `leaveWarningKey()` 在退出中返回 null ——
 * 于是确认过一次之后守卫不会再问第二次。绕过它写一处直接导航,表现是**弹两次框**:
 * 第二次问的时候 `rooms.leave()` 已经发出去了,座位已经让出去了,而那是在问一件
 * 已经发生的事。要在浏览器里刚好走到那条路径才看得见。
 */
{
  const file = 'src/app/pages/rooms/room-page/room-page.ts';
  const source = readFileSync(file, 'utf8');
  const calls = [...source.matchAll(/this\.router\.navigate\w*\(/g)];
  const leaveToAt = source.indexOf('private leaveTo(');

  if (leaveToAt < 0) {
    errors.push(`${file}: no private leaveTo(...) — the rule below has nothing to anchor on`);
  } else if (calls.length === 0) {
    errors.push(`${file}: no router call at all; this check would pass vacuously`);
  } else {
    for (const call of calls) {
      if (call.index < leaveToAt) {
        const line = source.slice(0, call.index).split('\n').length;
        errors.push(
          `${file}:${line}: ${call[0]} outside leaveTo() — a deliberate exit must set the ` +
            `exiting flag first, or the leave guard asks a second time after the seat is gone`,
        );
      }
    }
  }
}

/*
 * header 不许静态依赖 `@angular/cdk`,而那一组控件只许出现在 `@defer` 块里。
 *
 * 量出来的账:cdk 在首屏曾经是 **77.13 kB**,而我们自己全部的代码 52.12 kB —— header 是
 * shell 的一部分,所以从来不点那个菜单的人也在付。挪进 `@defer` 之后首屏 477.83 → 402.62 kB,
 * 归因里 cdk 是 **0**。
 *
 * 而「它在 @defer 里所以是懒的」这句话**不能当判据** —— 判据是构建产物的归因。这两条
 * 规则不是判据,是**围栏**:有人把 `<app-appearance-menus>` 从 defer 块里挪出来、或者直接
 * 在 header 里 import cdk,都会在这里变红,而不必等下一次有人去跑归因脚本。
 */
{
  const ts = readFileSync('src/app/shell/header/header.ts', 'utf8');
  if (/from '@angular\/cdk/.test(ts)) {
    errors.push(
      "src/app/shell/header/header.ts: imports @angular/cdk — that puts 77 kB back into the " +
        'initial bundle. The menus live in appearance-menus, behind @defer.',
    );
  }

  const html = readFileSync('src/app/shell/header/header.html', 'utf8');
  const uses = [...html.matchAll(/<app-appearance-menus/g)];
  const deferAt = html.indexOf('@defer (');
  if (uses.length === 0) {
    errors.push('src/app/shell/header/header.html: no <app-appearance-menus> — this check would pass vacuously');
  } else if (deferAt < 0) {
    errors.push('src/app/shell/header/header.html: <app-appearance-menus> is not inside a @defer block');
  } else {
    for (const use of uses) {
      if (use.index < deferAt) {
        const line = html.slice(0, use.index).split('\n').length;
        errors.push(
          `src/app/shell/header/header.html:${line}: <app-appearance-menus> outside the @defer block — ` +
            '@angular/cdk goes back into the initial bundle',
        );
      }
    }
  }
}

/*
 * `@prefetch` 不是 Angular 的块 —— prefetch 是 `@defer (...)` 括号里的触发器。
 *
 * 写成 `} @prefetch (on idle)` 编译器不报错,它把这一段当**字面文本**渲染出来。代价是
 * 两笔:每一页的 header 里多出一行 " @prefetch (on idle) ",而预取从来没有配上。它躲过了
 * 整套单元测试,因为那些断言按 `aria-label` / `role` 取元素,没有一条看整段文本;也躲过了
 * 浏览器验收,因为那一趟查的是「按钮有没有 aria-haspopup」。
 *
 * 所以这条规则扫**所有**模板,而不只是 header:同样的手滑在任何一个模板里都是同样的表现。
 * 注释里出现不算 —— 先把 `<!-- -->` 去掉再找。
 */
{
  const templates = [];
  const walk = (dir) => {
    for (const entry of readdirSync(dir, { withFileTypes: true })) {
      const full = `${dir}/${entry.name}`;
      if (entry.isDirectory()) walk(full);
      else if (entry.name.endsWith('.html')) templates.push(full);
    }
  };
  walk('src/app');

  // 正面对照:一个空集合会让下面的循环恒真。
  if (templates.length === 0) {
    errors.push('src/app: no .html templates found — the @prefetch rule would pass vacuously');
  }
  if (!templates.some((f) => readFileSync(f, 'utf8').includes('@defer ('))) {
    errors.push('src/app: no template uses @defer — the @prefetch rule has nothing to protect');
  }

  for (const file of templates) {
    const body = readFileSync(file, 'utf8').replace(/<!--[\s\S]*?-->/g, '');
    const at = body.indexOf('@prefetch');
    if (at >= 0) {
      const line = body.slice(0, at).split('\n').length;
      errors.push(
        `${file}:${line}: @prefetch is not a block — put \`prefetch on …\` inside the ` +
          '@defer (…) trigger list. As written it renders as literal text and never prefetches',
      );
    }
  }
}

/*
 * scrubber 只许有一份。
 *
 * 回放页与古谱学习页共用 `app-move-scrubber`;任何一边自己再画一个进度条,就是第二份
 * scrubber,而两份会各自漂 —— 边界禁用、到末尾自动停、切倍速不重建计时器这几条在
 * 组件的 spec 里有断言钉着,而复制品的那几条不会跟着红。
 */
{
  const consumers = [
    'src/app/pages/replay/replay-page/replay-page.html',
    'src/app/games/xiangqi/manual/manual-study/manual-study.html',
  ];
  for (const file of consumers) {
    const html = readFileSync(file, 'utf8');
    if (!html.includes('<app-move-scrubber')) {
      errors.push(`${file}: does not use <app-move-scrubber> — the shared control is the point`);
    }
    if (html.includes('type="range"')) {
      errors.push(
        `${file}: writes its own type="range" — that is a second scrubber, and the two will drift`,
      );
    }
  }
}

/*
 * 象棋棋盘从 `state()` 里读的字段 —— **恰好两个**。
 *
 * 古谱学习页据此敢把 `host` / `seats` / 时间戳留空:古谱没有对手,也没有下棋的时间,
 * 而给它们编一份**看起来和真的一模一样**的假数据是这个仓库反复付账的形状。
 *
 * 所以这条钉的是那个前提。哪天棋盘开始读第三个字段,这里红,并且指向学习页 ——
 * 而不是让页面上出现一个空白的用户名。
 */
{
  const files = [
    'src/app/games/xiangqi/board/xiangqi-board.ts',
    'src/app/games/xiangqi/board/xiangqi-board.html',
  ];
  const allowed = new Set(['game', 'status']);
  const seen = new Set();
  for (const file of files) {
    const text = readFileSync(file, 'utf8');
    for (const m of text.matchAll(/state\(\)\s*\??\.\s*([a-zA-Z]+)/g)) {
      seen.add(m[1]);
      if (!allowed.has(m[1])) {
        const line = text.slice(0, m.index).split('\n').length;
        errors.push(
          `${file}:${line}: the xiangqi board now reads state().${m[1]}. ` +
            'manual-study synthesises a RoomState with that field left empty on purpose — ' +
            'either give it a real value there or stop reading it here.',
        );
      }
    }
  }
  // 正面对照:一个都没匹配到说明模式坏了,而那会让上面的循环空过。
  if (seen.size === 0) {
    errors.push(
      'src/app/games/xiangqi/board: matched no state() reads — the allow-list check would pass vacuously',
    );
  }
}

/*
 * **古谱只做研习:界面不许出现任何「你解对了」的判定,也不许有「接着自己走」的入口。**
 *
 * 理由不是工期,是两个硬缺口:领域里**没有任何重复局面 / 长将 / 长捉规则**,而**和棋的
 * 定义就在那里**(六辑残局里和棋 391 局);而 `IBoardGameAi.SelectMove` 只收走子历史,
 * 从残局出发 AI 会按标准开局重建棋盘。
 *
 * **一个判错的「解对了」比没有判定更糟** —— 它教错棋,而错的样子和对的样子在界面上
 * 完全一样,没有任何断言会红。所以这条规则守的是「不要先上一个会骗人的功能」。
 *
 * 拆除条件:长将 / 重复局面规则落地 **且** AI 能从给定局面走棋。见 `CLAUDE.md` 的延期表。
 */
{
  const dir = 'src/app/games/xiangqi/manual';
  const files = [];
  // 目录不在时**报错而不是抛**:一个未捕获的异常也算失败,但它给出的是一段栈,
  // 而这里该说的是「这条规则没有东西可守了」。
  const walk = (d) => {
    let entries;
    try {
      entries = readdirSync(d, { withFileTypes: true });
    } catch {
      return;
    }
    for (const entry of entries) {
      const full = `${d}/${entry.name}`;
      if (entry.isDirectory()) walk(full);
      else if (entry.name.endsWith('.html') || entry.name.endsWith('.ts')) files.push(full);
    }
  };
  walk(dir);

  // 正面对照:目录空了的话下面的循环恒真。
  if (files.length === 0) {
    errors.push(`${dir}: no manual sources found — the study-only rule would pass vacuously`);
  }

  // 判定类的 i18n 键与文案。**匹配的是键**,不是中文散文 —— 模板里不许硬编码显示串,
  // 所以判定只可能以键的形式出现。
  const banned = /manual\.(solved|correct|wrong|success|failed|verdict-check)|\bplay-from-here\b/;
  for (const file of files) {
    if (file.endsWith('.spec.ts')) continue;
    const source = readFileSync(file, 'utf8');
    const hit = banned.exec(source);
    if (hit) {
      const line = source.slice(0, hit.index).split('\n').length;
      errors.push(
        `${file}:${line}: ${hit[0]} — the manual pages are study-only. There are no repetition / ` +
          'perpetual-check rules yet, so a draw cannot be judged, and the AI cannot start from a ' +
          'given position. A verdict that is wrong looks exactly like one that is right.',
      );
    }
  }
}

if (errors.length) {
  console.error(`source rule check failed (${errors.length}):`);
  for (const e of errors) console.error(`  - ${e}`);
  process.exit(1);
}
console.log(
  'source rules: room page routes only through leaveTo; header keeps @angular/cdk behind @defer; ' +
    'no template writes @prefetch as a block; one scrubber, two consumers; ' +
    'the xiangqi board reads exactly {game, status}; the manual pages stay study-only',
);
