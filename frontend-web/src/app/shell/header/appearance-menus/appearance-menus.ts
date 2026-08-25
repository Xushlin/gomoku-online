import { CdkMenu, CdkMenuItem, CdkMenuItemCheckbox, CdkMenuTrigger } from '@angular/cdk/menu';
import {
  afterNextRender,
  ChangeDetectionStrategy,
  Component,
  inject,
  input,
  viewChildren,
} from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { SoundService } from '../../../core/sound/sound.service';

/**
 * One dropdown appearance control. Every string derives from `prefix`: the
 * control's name is `<prefix>.label` and the current value plus each option
 * is `<prefix>.<option>`. Language, theme, board skin and sound pack are all
 * this shape, so the template renders them from one loop.
 */
export interface PickerControl {
  readonly prefix: string;
  readonly options: readonly string[];
  readonly value: string;
  /** The sound-pack menu carries the volume slider under its options. */
  readonly hasVolume: boolean;
  readonly apply: (option: string) => void;
}

/** One two-state appearance control — sound on/off, dark mode on/off. */
export interface ToggleControl {
  readonly labelKey: string;
  readonly stateKey: string;
  readonly checked: boolean;
  readonly toggle: () => void;
}

/**
 * header 那一组外观控件**连同它们的菜单** —— 而它存在的唯一理由是**把
 * `@angular/cdk` 挪出首屏**。
 *
 * 量出来的账:cdk 在首屏 **77.13 kB**(overlay 34.17 / menu 18.69 / focus-monitor 6.07 /
 * portal 5.02 / list-key-manager 4.27 / scrolling 3.03 / 十来个小块),而**我们自己全部的
 * 代码只有 52.12 kB** —— 一组下拉菜单比整个应用大 1.5 倍。而 header 是 shell 的一部分,
 * 所以那 77 kB 每个人首屏都付,包括从来不点它的人。打桩量到的上限是 477.83 → 396.42 kB。
 *
 * **整组一起搬,不是只搬菜单** —— 这不是偷懒:`CdkMenu` 用
 * `@ContentChildren(CdkMenuItem, { descendants: true })` 收集菜单项,而 content query
 * **既不进子组件的 view,也不进 `ngTemplateOutlet` 实例化的 embedded view**。所以触发器、
 * `cdkMenu` 与菜单项必须在**同一个模板**里(既有要求里写着,踩过 `NG0201`)。
 *
 * 数据不在这里:`pickers` / `toggles` 仍然由 `Header` 拼(它是那六个控件的唯一定义处),
 * 这边只负责画和开。
 */
@Component({
  selector: 'app-appearance-menus',
  standalone: true,
  imports: [CdkMenu, CdkMenuItem, CdkMenuItemCheckbox, CdkMenuTrigger, TranslocoPipe],
  templateUrl: './appearance-menus.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AppearanceMenus {
  protected readonly sound = inject(SoundService);

  readonly pickers = input.required<readonly PickerControl[]>();
  readonly toggles = input.required<readonly ToggleControl[]>();

  /**
   * 加载完之后要立刻打开哪一个 —— **占位那一次点击不能白点**。
   *
   * 索引与 DOM 顺序一致:`0…n-1` 是内联的 picker,`n` 是窄视口那个 Settings 按钮。
   * `null` 表示不是点出来的(例如 `@prefetch` 提前拉到之后自然渲染),那就什么都不开。
   *
   * 可接受的代价是「等一个 chunk」,而不是「白点一次」—— 那条延期项自己这么写的。
   */
  readonly openTrigger = input<number | null>(null);

  private readonly triggers = viewChildren(CdkMenuTrigger);

  constructor() {
    /*
     * `afterNextRender` 而不是 `ngAfterViewInit`:视图查询在这一刻才齐,而
     * `CdkMenuTrigger.open()` 要把 overlay 挂到已经在文档里的元素上。
     */
    afterNextRender(() => {
      const index = this.openTrigger();
      if (index === null) return;
      /*
       * **下一个宏任务里才 open(),而这是量出来的。**
       *
       * 直接在这里同步调 `open()`:回调确实跑了(探针确认过 `ran: true`、索引与触发器
       * 数量都对),而**菜单没有出现**(`cdk-overlay-pane` 数量 0)。原因是发起这一切的
       * 那次点击还在冒泡,而 CDK 打开菜单时会订阅 document 上的「点到外面就关」——
       * 它接住了同一次事件的尾巴,于是刚开就关。
       *
       * 挪到下一个宏任务之后:pane 1 个,菜单项是 Material / System / Ink / Game hall。
       *
       * **单元测试抓不到这一条** —— 它在 jsdom 里同步版本也是绿的。所以这段注释是这条
       * 修复唯一的记录,而浏览器里点那一下是唯一的证据。
       */
      setTimeout(() => this.triggers()[index]?.open());
    });
  }

  protected onVolumeChange(value: string): void {
    this.sound.setVolume(Number(value));
    // Audition the new level on release so the user hears what they chose
    // (mirrors the pack-switch audition; silent when muted or at 0).
    if (!this.sound.muted()) this.sound.play('move-place');
  }
}
