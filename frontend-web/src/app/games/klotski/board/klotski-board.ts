import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import {
  legalTargets,
  roleOf,
  type KlotskiLayout,
  type KlotskiPiece,
  type KlotskiPositions,
  type KlotskiRole,
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
 * 华容道 board — 每个棋子是一个 `transform: translate()` 到格坐标上的矩形。
 *
 * 位置**不由 `grid-area` 表达**:grid 的行列线不可动画,上一版因此瞬移(而样式表里那句
 * 「browsers animate as a layout change」是假的)。现在坐标经 `--kt-r` / `--kt-c` 交给 CSS,
 * 由 `transform` 落位,于是它真的滑。格距的单位是 `cqw` 而不是 `%`——见 `global.css`。
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

  /**
   * 一个落点标记的大小 —— 它画的是**选中那个棋子**将要占的矩形,不是一个格子。
   *
   * 选中项缺席时退回 1x1:那是「没有选中」的形状,而落点列表在没有选中时是空的,
   * 所以这个默认值只在渲染顺序的缝隙里出现。
   */
  private readonly selectedPiece = computed<KlotskiPiece | null>(() => {
    const id = this.selected();
    return id === null ? null : (this.layout().pieces.find((p) => p.id === id) ?? null);
  });

  protected selectedWidth(): number {
    return this.selectedPiece()?.width ?? 1;
  }

  protected selectedHeight(): number {
    return this.selectedPiece()?.height ?? 1;
  }

  /** 角色从几何形状推,见 `model.ts` —— 模板按它挑面。 */
  protected roleOf(piece: KlotskiPiece): KlotskiRole {
    return roleOf(piece);
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
