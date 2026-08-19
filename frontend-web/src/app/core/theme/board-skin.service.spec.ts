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

  it('register() allows new skins to be enumerated and activated', () => {
    const { svc } = setup();
    svc.register('bamboo', {
      board: { bg: '#c8a66b', line: '#000', star: '#000', radius: '0', shadow: 'none' },
      stones: {
        blackFill: '#000',
        blackShadow: 'none',
        whiteFill: '#fff',
        whiteRim: '#ccc',
        whiteShadow: 'none',
      },
      pieces: { bg: '#f3e3c0', red: '#b3261e', black: '#241d16' },
      // 这份 fixture 在 `cards` / `felt` 加进 BoardSkinTokens 的那一刻编译不过了 ——
      // 那正是机制在工作:一个新皮肤**不可能**漏掉扑克牌与桌面的 token。
      // add-web-xiangqi 加 `pieces` 时是同一处红的。
      cards: {
        face: '#fff',
        faceEdge: '#ccc',
        red: '#c00',
        black: '#111',
        back: '#369',
        backEdge: '#eee',
      },
      felt: { bg: '#1d5333', edge: '#6b4423', radius: '0', shadow: 'none', text: '#fff', textMuted: '#ccc' },
      lastMove: { ring: '#f00' },
    });
    expect(svc.availableSkins()).toContain('bamboo');
    svc.activate('bamboo');
    expect(svc.skinName()).toBe('bamboo');
  });
});
