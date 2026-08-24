import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { emblemNode, type EmblemNode, type EmblemShape } from '../game-emblem';

/**
 * Draws a game's emblem from its shape table.
 *
 * **This component owns the drawing system, and that is its reason for
 * existing.** The 24×24 grid, the 1.6 stroke, the round caps and joins live
 * here and nowhere else, so nine emblems cannot each pick their own — which is
 * what would turn a set into nine drawings.
 *
 * Colour comes entirely from `currentColor`, so the tile sets the identity hue
 * and the emblem inherits it. No literal colour appears in this component or in
 * any shape table; `check-styles.mjs` already forbids literals in the role
 * utilities and the same rule applies here by construction.
 *
 * Decorative: the game's name is always rendered beside it, so the SVG is
 * `aria-hidden` rather than carrying a duplicate label a screen reader would
 * read twice.
 */
@Component({
  selector: 'app-game-emblem',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <svg
      [attr.width]="size()"
      [attr.height]="size()"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      stroke-width="1.6"
      stroke-linecap="round"
      stroke-linejoin="round"
      aria-hidden="true"
      focusable="false"
    >
      @for (n of nodes(); track $index) {
        @switch (n.tag) {
          @case ('line') {
            <svg:line
              [attr.x1]="n.attrs['x1']"
              [attr.y1]="n.attrs['y1']"
              [attr.x2]="n.attrs['x2']"
              [attr.y2]="n.attrs['y2']"
            />
          }
          @case ('circle') {
            <svg:circle
              [attr.cx]="n.attrs['cx']"
              [attr.cy]="n.attrs['cy']"
              [attr.r]="n.attrs['r']"
              [attr.fill]="n.attrs['fill']"
            />
          }
          @case ('rect') {
            <svg:rect
              [attr.x]="n.attrs['x']"
              [attr.y]="n.attrs['y']"
              [attr.width]="n.attrs['width']"
              [attr.height]="n.attrs['height']"
              [attr.rx]="n.attrs['rx']"
              [attr.fill]="n.attrs['fill']"
            />
          }
          @case ('text') {
            <svg:text
              [attr.x]="n.attrs['x']"
              [attr.y]="n.attrs['y']"
              [attr.font-size]="n.attrs['font-size']"
              [attr.text-anchor]="n.attrs['text-anchor']"
              [attr.dominant-baseline]="n.attrs['dominant-baseline']"
              [attr.fill]="n.attrs['fill']"
              [attr.stroke]="n.attrs['stroke']"
              >{{ n.text }}</svg:text
            >
          }
          @case ('path') {
            <svg:path [attr.d]="n.attrs['d']" [attr.fill]="n.attrs['fill']" />
          }
        }
      }
    </svg>
  `,
})
export class GameEmblem {
  /** The game's shape table, straight from its manifest. */
  readonly shapes = input.required<readonly EmblemShape[]>();

  /** Rendered box in px. The grid is fixed; only the box scales. */
  readonly size = input<number>(30);

  protected readonly nodes = computed<readonly EmblemNode[]>(() =>
    this.shapes().map(emblemNode),
  );
}
