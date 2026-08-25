/*
 * Source-level rules that no type or test can express.
 *
 * These are the ones where **forgetting looks exactly like remembering**: the
 * code compiles, the tests pass, and the defect only shows up on one path in a
 * real browser. They live at lint time, next to `check-styles.mjs`, because
 * they read source text rather than run it.
 */
import { readFileSync } from 'node:fs';

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

if (errors.length) {
  console.error(`source rule check failed (${errors.length}):`);
  for (const e of errors) console.error(`  - ${e}`);
  process.exit(1);
}
console.log('source rules: room page routes only through leaveTo; header keeps @angular/cdk behind @defer');
