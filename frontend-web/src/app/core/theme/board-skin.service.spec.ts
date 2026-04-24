import { DOCUMENT } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { beforeEach, describe, expect, it } from 'vitest';
import {
  BoardSkinService,
  DefaultBoardSkinService,
} from './board-skin.service';

function setup(stored: string | null = null) {
  localStorage.clear();
  if (stored !== null) localStorage.setItem('gomoku:board-skin', stored);
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

  it('ignores a stored skin that is not registered, falls back to default', () => {
    const { svc } = setup('bamboo');
    expect(svc.skinName()).toBe('wood');
  });

  it('activate() switches the attribute and persists', () => {
    const { svc, doc } = setup();
    svc.activate('classic');
    expect(svc.skinName()).toBe('classic');
    expect(doc.documentElement.dataset['boardSkin']).toBe('classic');
    expect(localStorage.getItem('gomoku:board-skin')).toBe('classic');
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
      lastMove: { ring: '#f00' },
    });
    expect(svc.availableSkins()).toContain('bamboo');
    svc.activate('bamboo');
    expect(svc.skinName()).toBe('bamboo');
  });
});
