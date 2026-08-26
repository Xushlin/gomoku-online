import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { ManualApiService } from '../../../../core/api/manual-api.service';
import type { ManualCatalogue as Catalogue } from '../../../../core/api/models/manual.model';
import { MEIHUAPU_KEY } from '../manual-key';

type Phase = 'loading' | 'ready' | 'not-found' | 'error';

/**
 * 《梅花谱》目录 —— 8 局,每局若干变化。
 *
 * **纯读**,不需要登录:它是一部三百年前的公开著作,而回放页要求身份是因为它暴露的是
 * 具体用户的对局。
 *
 * 分组不在这里算:服务端已经按局分好,而局号是线路自己的列。**这一页因此不知道
 * 「一共几局」** —— 硬编码 8 会在下一部谱落地那天静静对不上。
 */
@Component({
  selector: 'app-manual-catalogue',
  standalone: true,
  imports: [RouterLink, TranslocoPipe],
  templateUrl: './manual-catalogue.html',
  styles: [':host { display: block; width: 100%; }'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ManualCatalogue implements OnInit {
  private readonly api = inject(ManualApiService);

  protected readonly catalogue = signal<Catalogue | null>(null);
  protected readonly phase = signal<Phase>('loading');

  /** 总条数 —— 由数据算出,给标题用一句「31 条线路」。 */
  protected readonly lineCount = computed(() =>
    (this.catalogue()?.chapters ?? []).reduce((n, c) => n + c.lines.length, 0),
  );

  /** 骨架屏的格子数。只为占位,不表达任何真实数量。 */
  protected readonly skeletons = [1, 2, 3, 4, 5, 6];

  ngOnInit(): void {
    this.load();
  }

  protected load(): void {
    this.phase.set('loading');
    this.api.getCatalogue(MEIHUAPU_KEY).subscribe({
      next: (c) => {
        this.catalogue.set(c);
        this.phase.set('ready');
      },
      error: (err: unknown) => {
        this.phase.set(err instanceof HttpErrorResponse && err.status === 404 ? 'not-found' : 'error');
      },
    });
  }
}
