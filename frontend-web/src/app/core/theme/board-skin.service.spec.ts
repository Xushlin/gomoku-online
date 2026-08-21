import { DOCUMENT } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { beforeEach, describe, expect, it } from 'vitest';
import {
  BoardSkinService,
  DefaultBoardSkinService,
} from './board-skin.service';

function setup(stored: string | null = null) {
  localStorage.clear();
  if (stored !== null) localStorage.setItem('gewu:board-skin', stored);
  // Reset the <html> attribute between tests
  document.documentElement.removeAttribute('data-board-skin');
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    providers: [{ provide: BoardSkinService, useClass: DefaultBoardSkinService }],
  });
  return {
    svc: TestBed.inject(BoardSkinService),
    doc: TestBed.inject(DOCUMENT),
  };
}

describe('DefaultBoardSkinService', () => {
  beforeEach(() => {
    localStorage.clear();
    document.documentElement.removeAttribute('data-board-skin');
  });

  it('defaults to wood on first boot', () => {
    const { svc, doc } = setup();
    expect(svc.skinName()).toBe('wood');
    expect(doc.documentElement.dataset['boardSkin']).toBe('wood');
  });

  it('restores a valid stored skin', () => {
    const { svc, doc } = setup('classic');
    expect(svc.skinName()).toBe('classic');
    expect(doc.documentElement.dataset['boardSkin']).toBe('classic');
  });

  it('registers midnight as a built-in skin', () => {
    const { svc } = setup();
    expect(svc.availableSkins()).toContain('midnight');
  });

  it('activate(midnight) sets the attribute and persists', () => {
    const { svc, doc } = setup();
    svc.activate('midnight');
    expect(doc.documentElement.dataset['boardSkin']).toBe('midnight');
    expect(localStorage.getItem('gewu:board-skin')).toBe('midnight');
    const { svc: next } = setup('midnight');
    expect(next.skinName()).toBe('midnight');
  });

  it('ignores a stored skin that is not registered, falls back to default', () => {
    const { svc } = setup('bamboo');
    expect(svc.skinName()).toBe('wood');
  });

  it('activate() switches the attribute and persists', () => {
    const { svc, doc } = setup();
    svc.activate('classic');
    expect(svc.skinName()).toBe('classic');
    expect(doc.documentElement.dataset['boardSkin']).toBe('classic');
    expect(localStorage.getItem('gewu:board-skin')).toBe('classic');
  });

  it('activate() on an unregistered name is a no-op', () => {
    const { svc } = setup();
    svc.activate('bamboo');
    expect(svc.skinName()).toBe('wood');
  });

  it('register() takes a name, and a name is all a skin needs on this side', () => {
    const { svc } = setup();

    svc.register('bamboo');

    expect(svc.availableSkins()).toContain('bamboo');
    svc.activate('bamboo');
    expect(svc.skinName()).toBe('bamboo');
  });

  /*
   * 这条测试原来传一整份 token fixture,而它的注释记着一件真事:那份 fixture 在
   * `pieces`(add-web-xiangqi)与 `cards` / `felt` 加进 `BoardSkinTokens` 的那两刻
   * **编译不过**,于是「一个新皮肤不可能漏掉这些 token」是被编译器兜住的。
   *
   * `drop-board-skin-mirrors` 删掉了那个保证,所以这里要写清它换到哪去了 ——
   * 而这不是等价替换:
   *
   *   - 那个编译错误响在**一份测试假皮肤 + 三份 TS 副本**上。真正画画的是
   *     `board-skins.css`,而一份 TS 副本齐全、CSS 块缺一项的皮肤照样编译通过。
   *   - 现在守它的是 `scripts/check-styles.mjs`:以默认皮肤的变量集作基准,要求
   *     每个皮肤块声明**完全相同**的集合,皮肤名单从 `register('…')` 调用推导。
   *     漏一个会红并点名,多一个拼错的也会红,而它跑在 CI 里。
   *
   * 换句话说:保证从「TS 副本必须完整」变成「**画画的那份**必须完整」。位置更对,
   * 而时机更晚(lint 而非编译)。**这是一次有取舍的交换,不是纯粹的清理。**
   */
});
