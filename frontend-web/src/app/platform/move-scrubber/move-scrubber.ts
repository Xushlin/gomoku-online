import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  input,
  output,
  signal,
} from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';

const STEP_INTERVAL_MS = 700;

/** 播放倍速。 */
export type ScrubberSpeed = 0.5 | 1 | 2;

/**
 * 一条着法序列的播放控件 —— 上一/下一步、首/末、播放/暂停、倍速、进度条。
 *
 * **纯展示**:它不注入任何服务,也不知道着法从哪来。输入是「一共多少手」与「现在第几手」,
 * 输出是「请跳到第 N 手」;**当前半手的真源在页面上**,因为页面还要用它切招法喂棋盘。
 *
 * 它从 `ReplayPage` 里抽出来,理由是第二个消费者到了(古谱学习页)。**复制一份的代价是
 * 可测的**:边界禁用、到末尾自动停、切倍速不重建计时器这几条在回放页有断言钉着,而
 * 复制品的那几条不会跟着红。
 *
 * 播放的计时留在这里(它是这个控件自己的行为),而每一跳都是一次 `seek` ——
 * 所以页面不需要知道「正在播放」这件事。
 *
 * i18n 键仍然是 `replay.scrubber.*`:改名要动两份 locale 加双语对齐那条断言,而键名
 * 玩家看不见。**拆除条件:第三个消费者落地**,那时 `replay.` 这个前缀就真的误导了。
 */
@Component({
  selector: 'app-move-scrubber',
  standalone: true,
  imports: [TranslocoPipe],
  templateUrl: './move-scrubber.html',
  styles: [':host { display: block; width: 100%; }'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MoveScrubber {
  /** 一共多少半手。 */
  readonly totalMoves = input.required<number>();

  /** 现在停在第几手(0 = 起始局面)。 */
  readonly currentPly = input.required<number>();

  /** 请求跳到第 N 手。页面负责钳制与落地,这里只提意图。 */
  readonly seek = output<number>();

  protected readonly playing = signal(false);
  protected readonly speed = signal<ScrubberSpeed>(1);
  protected readonly speeds: readonly ScrubberSpeed[] = [0.5, 1, 2];

  protected readonly atStart = computed(() => this.currentPly() === 0);
  protected readonly atEnd = computed(() => this.currentPly() >= this.totalMoves());
  protected readonly playButtonKey = computed(() => {
    if (this.atEnd()) return 'replay.scrubber.replay';
    return this.playing() ? 'replay.scrubber.pause' : 'replay.scrubber.play';
  });

  constructor() {
    effect((onCleanup) => {
      if (!this.playing()) return;
      const speed = this.speed();
      /*
       * `currentPly()` **只在回调里读**,不在 effect 体里 —— 在体里读会让每走一手都
       * 重建一次计时器,而那正是「切倍速无 jitter」那条断言要挡的形状。
       */
      const id = setInterval(() => {
        const next = this.currentPly() + 1;
        if (next > this.totalMoves()) {
          this.playing.set(false);
          return;
        }
        this.seek.emit(next);
        if (next >= this.totalMoves()) this.playing.set(false);
      }, STEP_INTERVAL_MS / speed);
      onCleanup(() => clearInterval(id));
    });
  }

  protected first(): void {
    this.playing.set(false);
    this.seek.emit(0);
  }

  protected last(): void {
    this.playing.set(false);
    this.seek.emit(this.totalMoves());
  }

  protected onPrev(): void {
    this.playing.set(false);
    this.seek.emit(Math.max(0, this.currentPly() - 1));
  }

  protected onNext(): void {
    this.playing.set(false);
    this.seek.emit(Math.min(this.totalMoves(), this.currentPly() + 1));
  }

  protected togglePlay(): void {
    if (this.atEnd()) {
      this.seek.emit(0);
      this.playing.set(true);
      return;
    }
    this.playing.set(!this.playing());
  }

  protected setSpeed(s: ScrubberSpeed): void {
    this.speed.set(s);
  }

  protected onSeek(event: Event): void {
    const target = event.target as HTMLInputElement;
    const value = Number.parseInt(target.value, 10);
    if (Number.isNaN(value)) return;
    this.playing.set(false);
    this.seek.emit(Math.max(0, Math.min(this.totalMoves(), value)));
  }
}
