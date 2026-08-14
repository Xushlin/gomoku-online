import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';

/**
 * The movable-type tray. Presentational: it renders tiles and emits taps.
 *
 * Tiles are shown individually rather than grouped with a count — the
 * prototype's behaviour, and duplicates carry real information (「一心一意」
 * needs two 一, and seeing both is part of reading the puzzle).
 */
@Component({
  selector: 'app-crossword-tray',
  standalone: true,
  imports: [TranslocoPipe],
  templateUrl: './tray.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Tray {
  readonly tiles = input.required<readonly string[]>();
  readonly used = input.required<ReadonlySet<number>>();

  readonly tileTap = output<number>();

  protected onTap(index: number): void {
    if (this.used().has(index)) return;
    this.tileTap.emit(index);
  }
}
