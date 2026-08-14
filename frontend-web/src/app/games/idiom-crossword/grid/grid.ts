import {
  ChangeDetectionStrategy,
  Component,
  computed,
  ElementRef,
  inject,
  input,
  OnDestroy,
  output,
  signal,
} from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import type { CrosswordLayout } from '../../../core/api/models/puzzle.model';
import { cellKey } from '../crossword-state';
import { cellSizeFor, gapFor } from './geometry';

/**
 * The crossword board. Presentational: it renders what it is given and emits
 * taps; it owns no game state.
 *
 * Cell size is a `computed()` over a `ResizeObserver`-backed width signal
 * rather than the prototype's `window.resize` listener. That reacts to
 * *container* changes (orientation, layout shifts), cannot leak a listener
 * across route changes, and keeps the value in the same reactive graph as
 * everything else — so no manual repaint is needed.
 */
@Component({
  selector: 'app-crossword-grid',
  standalone: true,
  imports: [TranslocoPipe],
  templateUrl: './grid.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Grid implements OnDestroy {
  private readonly host = inject(ElementRef<HTMLElement>);

  readonly layout = input.required<CrosswordLayout>();
  /** Character shown in each cell, keyed `"row,col"`. */
  readonly chars = input.required<ReadonlyMap<string, string> | Map<string, string>>();
  readonly locked = input.required<ReadonlySet<string>>();
  readonly given = input.required<ReadonlyMap<string, string>>();
  readonly selected = input<string | null>(null);
  readonly shaking = input<ReadonlySet<string>>(new Set<string>());

  readonly cellTap = output<string>();

  private readonly containerWidth = signal(0);
  private readonly observer = new ResizeObserver((entries) => {
    const width = entries[0]?.contentRect.width ?? 0;
    if (width > 0) this.containerWidth.set(width);
  });

  protected readonly gap = computed(() => gapFor(this.layout().cols));

  protected readonly cellSize = computed(() =>
    cellSizeFor(this.containerWidth(), this.layout().cols, this.gap()),
  );

  /** Every position in the bounding box; positions with no cell render as voids. */
  protected readonly rows = computed(() => {
    const layout = this.layout();
    const present = new Set(layout.cells.map((c) => cellKey(c.row, c.col)));
    return Array.from({ length: layout.rows }, (_, row) =>
      Array.from({ length: layout.cols }, (_, col) => {
        const key = cellKey(row, col);
        return { key, row, col, present: present.has(key) };
      }),
    );
  });

  constructor() {
    this.observer.observe(this.host.nativeElement);
  }

  ngOnDestroy(): void {
    this.observer.disconnect();
  }

  protected charOf(key: string): string {
    return this.chars().get(key) ?? '';
  }

  protected isGiven(key: string): boolean {
    return this.given().has(key);
  }

  protected isLocked(key: string): boolean {
    return this.locked().has(key);
  }

  protected onTap(key: string, present: boolean): void {
    if (!present) return;
    this.cellTap.emit(key);
  }
}
