import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { ManualApiService } from '../../../../core/api/manual-api.service';
import type { ManualSummary } from '../../../../core/api/models/manual.model';

type Phase = 'loading' | 'ready' | 'empty' | 'error';

/**
 * 象棋古谱的清单 —— 七部谱的入口。
 *
 * **清单来自服务端**,而不是客户端写死的七个键:加一辑是加一份数据文件加一行注册,
 * 这一页无 diff。它也因此不知道「一共几部」—— 那正是硬编码会静静说错的地方。
 *
 * 纯读、匿名可读:三百年前的公开著作,而回放页要求身份是因为它暴露具体用户的对局。
 */
@Component({
  selector: 'app-manual-list',
  standalone: true,
  imports: [RouterLink, TranslocoPipe],
  templateUrl: './manual-list.html',
  styles: [':host { display: block; width: 100%; }'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ManualList implements OnInit {
  private readonly api = inject(ManualApiService);

  protected readonly manuals = signal<readonly ManualSummary[]>([]);
  protected readonly phase = signal<Phase>('loading');

  /** 骨架屏的格子数。只为占位,不表达任何真实数量。 */
  protected readonly skeletons = [1, 2, 3, 4, 5, 6];

  ngOnInit(): void {
    this.load();
  }

  protected load(): void {
    this.phase.set('loading');
    this.api.listManuals().subscribe({
      next: (list) => {
        this.manuals.set(list);
        // **空清单是「还没导入」,不是错误。** 而它与「加载中」必须分开:一个停在加载态
        // 的页面什么也不画,那和功能不存在长得一模一样。
        this.phase.set(list.length === 0 ? 'empty' : 'ready');
      },
      error: (err: unknown) => {
        this.phase.set(err instanceof HttpErrorResponse ? 'error' : 'error');
      },
    });
  }
}
