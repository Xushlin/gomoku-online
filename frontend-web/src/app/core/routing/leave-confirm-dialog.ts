import { Dialog, DIALOG_DATA, DialogRef } from '@angular/cdk/dialog';
import {
  ChangeDetectionStrategy,
  Component,
  inject,
  runInInjectionContext,
  type Injector,
} from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { map, type Observable } from 'rxjs';

/** 正文的 i18n 键 —— 由页面给,因为**代价按页面不同**。 */
export interface LeaveConfirmData {
  readonly messageKey: string;
}

/**
 * 「真的要走吗」——一个 CDK 弹框,而不是 `window.confirm`。
 *
 * `window.confirm` 拿不到主题、拿不到 i18n、在 jsdom 里也测不了,而这三件正好是本仓库
 * 的硬规则。焦点陷阱 / ESC / backdrop / ARIA 由 CDK 给。
 *
 * 正文的键由调用方传:**「超时判负」和「这一局不计入排行」是两件不同的事**,而玩家
 * 要据此决定。一句通用的「确定离开?」把这个差别抹平了。
 */
@Component({
  selector: 'app-leave-confirm-dialog',
  standalone: true,
  imports: [TranslocoPipe],
  templateUrl: './leave-confirm-dialog.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LeaveConfirmDialog {
  private readonly dialogRef = inject<DialogRef<boolean>>(DialogRef);
  protected readonly data = inject<LeaveConfirmData>(DIALOG_DATA);

  protected leave(): void {
    this.dialogRef.close(true);
  }

  protected stay(): void {
    this.dialogRef.close(false);
  }
}

/**
 * 打开离开确认框,返回「确认了吗」。
 *
 * 守卫和「离开房间」按钮都走这里,所以**弹的是同一个框、说的是同一句话**,而配置
 * (data 的形状、ariaLabel)只有一处 —— 两处各写一份是它们迟早说出两句话的方式。
 */
export function openLeaveConfirm(injector: Injector, messageKey: string): Observable<boolean> {
  const closed = runInInjectionContext(injector, () =>
    inject(Dialog).open<boolean>(LeaveConfirmDialog, {
      data: { messageKey } satisfies LeaveConfirmData,
      ariaLabel: 'Leave confirmation',
    }).closed,
  );
  return closed.pipe(map((confirmed) => confirmed === true));
}
