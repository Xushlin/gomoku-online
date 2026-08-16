import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import {
  legalTargets,
  type KlotskiLayout,
  type KlotskiPiece,
  type KlotskiPositions,
} from '../model';

export interface SlideTarget {
  readonly row: number;
  readonly col: number;
  readonly dr: number;
  readonly dc: number;
}

interface PlacedPiece {
  readonly piece: KlotskiPiece;
  readonly row: number;
  readonly col: number;
}

/**
 * 华容道 board — absolutely positioned rectangles over a `rows × cols` grid.
 *
 * Purely presentational: no services, no router, no HTTP. It is handed the layout,
 * the current positions and the selection; it emits intent.
 *
 * It **does** mark the legal destinations of the selected piece, which is the
 * opposite of what the xiangqi board does — see `model.ts` for why the two
 * opposite choices are both right.
 */
@Component({
  selector: 'app-klotski-board',
  standalone: true,
  imports: [TranslocoPipe],
  templateUrl: './klotski-board.html',
  styles: [':host { display: block; width: 100%; }'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KlotskiBoard {
  readonly layout = input.required<KlotskiLayout>();
  readonly positions = input.required<KlotskiPositions>();
  readonly selected = input<string | null>(null);
  readonly = input<boolean>(false);

  readonly pick = output<string>();
  readonly slide = output<SlideTarget>();

  protected readonly placed = computed<readonly PlacedPiece[]>(() => {
    const positions = this.positions();
    return this.layout()
      .pieces.map((piece) => {
        const at = positions[piece.id];
        return at ? { piece, row: at.row, col: at.col } : null;
      })
      .filter((p): p is PlacedPiece => p !== null);
  });

  protected readonly targets = computed<readonly SlideTarget[]>(() => {
    const id = this.selected();
    if (id === null || this.readonly()) return [];
    return legalTargets(this.layout(), this.positions(), id);
  });

  protected isSelected(id: string): boolean {
    return this.selected() === id;
  }

  /** A CSS grid area — 1-based, end-exclusive. */
  protected area(p: PlacedPiece): string {
    return `${p.row + 1} / ${p.col + 1} / ${p.row + 1 + p.piece.height} / ${p.col + 1 + p.piece.width}`;
  }

  protected targetArea(t: SlideTarget): string {
    const piece = this.layout().pieces.find((p) => p.id === this.selected());
    const height = piece?.height ?? 1;
    const width = piece?.width ?? 1;
    return `${t.row + 1} / ${t.col + 1} / ${t.row + 1 + height} / ${t.col + 1 + width}`;
  }

  protected handlePick(id: string): void {
    if (this.readonly()) return;
    this.pick.emit(id);
  }

  protected handleSlide(target: SlideTarget): void {
    if (this.readonly()) return;
    this.slide.emit(target);
  }
}
