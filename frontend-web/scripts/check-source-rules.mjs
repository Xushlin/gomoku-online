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

if (errors.length) {
  console.error(`source rule check failed (${errors.length}):`);
  for (const e of errors) console.error(`  - ${e}`);
  process.exit(1);
}
console.log('source rules: room page routes only through leaveTo');
