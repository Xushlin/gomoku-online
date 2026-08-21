import { DOCUMENT, inject, Injectable, signal, type Signal } from '@angular/core';

const SKIN_STORAGE_KEY = 'gewu:board-skin';
const DEFAULT_SKIN = 'wood';

/**
 * Cross-cutting API for the board's visual skin.
 *
 * Painting is driven entirely by CSS: `<html data-board-skin="...">` selects
 * which `--board-*` / `--stones-*` variables cascade into `.board-grid` and
 * `.board-stone`. **The registry holds names only.**
 *
 * It used to mirror each skin's token values in TypeScript and validate them at
 * registration — and the values were never read, only `has()` and `keys()`.
 * `drop-theme-token-mirrors` made the same argument one directory over; this is
 * the sibling that change failed to grep for, and the bundle measurement found
 * it rather than the reasoning did. Completeness is asserted where the values
 * live: `scripts/check-styles.mjs` takes the default skin's variable set from
 * `board-skins.css` as the baseline and requires every other skin block to
 * declare exactly the same set, with the skin list derived from the
 * `register('…')` calls below.
 *
 * Injection goes through this abstract class as the DI token so tests can
 * supply a stub via `{ provide: BoardSkinService, useValue: ... }`.
 */
export abstract class BoardSkinService {
  abstract readonly skinName: Signal<string>;
  abstract register(name: string): void;
  abstract activate(name: string): void;
  abstract availableSkins(): readonly string[];
}

@Injectable()
export class DefaultBoardSkinService extends BoardSkinService {
  private readonly doc = inject(DOCUMENT);
  private readonly _skinName = signal<string>(DEFAULT_SKIN);
  private readonly skins = new Set<string>();

  readonly skinName: Signal<string> = this._skinName.asReadonly();

  constructor() {
    super();
    this.register('wood');
    this.register('classic');
    this.register('midnight');
    this.apply(this.resolveInitial());
  }

  register(name: string): void {
    this.skins.add(name);
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
    return Array.from(this.skins);
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

  private warn(message: string): void {
    if (typeof console !== 'undefined' && typeof console.warn === 'function') {
      console.warn(`[BoardSkinService] ${message}`);
    }
  }
}
