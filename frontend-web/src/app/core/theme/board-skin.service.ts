import { DOCUMENT, inject, Injectable, signal, type Signal } from '@angular/core';
import type { BoardSkinTokens } from './board-skin.tokens';
import { classicSkin } from './skins/classic';
import { woodSkin } from './skins/wood';

const SKIN_STORAGE_KEY = 'gomoku:board-skin';
const DEFAULT_SKIN = 'wood';

/**
 * Cross-cutting API for the board's visual skin.
 *
 * Painting is driven entirely by CSS: `<html data-board-skin="...">` selects
 * which `--board-*` / `--stones-*` variables cascade into `.board-grid` and
 * `.board-stone`. The TypeScript registry mirrors those values so new skins
 * can be `register`ed without touching any component, matching the same
 * pattern used by ThemeService.
 *
 * Injection goes through this abstract class as the DI token so tests can
 * supply a stub via `{ provide: BoardSkinService, useValue: ... }`.
 */
export abstract class BoardSkinService {
  abstract readonly skinName: Signal<string>;
  abstract register(name: string, tokens: BoardSkinTokens): void;
  abstract activate(name: string): void;
  abstract availableSkins(): readonly string[];
}

@Injectable()
export class DefaultBoardSkinService extends BoardSkinService {
  private readonly doc = inject(DOCUMENT);
  private readonly _skinName = signal<string>(DEFAULT_SKIN);
  private readonly skins = new Map<string, BoardSkinTokens>();

  readonly skinName: Signal<string> = this._skinName.asReadonly();

  constructor() {
    super();
    this.register('wood', woodSkin);
    this.register('classic', classicSkin);
    this.apply(this.resolveInitial());
  }

  register(name: string, tokens: BoardSkinTokens): void {
    this.validate(name, tokens);
    this.skins.set(name, tokens);
  }

  activate(name: string): void {
    if (!this.skins.has(name)) {
      this.warn(`activate('${name}'): skin not registered; ignoring.`);
      return;
    }
    this.apply(name);
    this.persist(name);
  }

  availableSkins(): readonly string[] {
    return Array.from(this.skins.keys());
  }

  private apply(name: string): void {
    this.doc.documentElement.dataset['boardSkin'] = name;
    this._skinName.set(name);
  }

  private resolveInitial(): string {
    const stored = this.read();
    if (stored && this.skins.has(stored)) return stored;
    if (stored) this.persist(DEFAULT_SKIN);
    return DEFAULT_SKIN;
  }

  private read(): string | null {
    try {
      return this.doc.defaultView?.localStorage.getItem(SKIN_STORAGE_KEY) ?? null;
    } catch {
      return null;
    }
  }

  private persist(name: string): void {
    try {
      this.doc.defaultView?.localStorage.setItem(SKIN_STORAGE_KEY, name);
    } catch {
      // best-effort — private mode / quota errors ignored
    }
  }

  private validate(name: string, tokens: BoardSkinTokens): void {
    if (!tokens?.board || !tokens.stones || !tokens.lastMove) {
      this.warn(`register('${name}'): missing board/stones/lastMove section.`);
    }
  }

  private warn(message: string): void {
    if (typeof console !== 'undefined' && typeof console.warn === 'function') {
      console.warn(`[BoardSkinService] ${message}`);
    }
  }
}
