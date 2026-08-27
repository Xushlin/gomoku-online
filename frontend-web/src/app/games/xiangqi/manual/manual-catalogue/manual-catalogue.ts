import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { ManualApiService } from '../../../../core/api/manual-api.service';
import type { ManualCatalogue as Catalogue } from '../../../../core/api/models/manual.model';

type Phase = 'loading' | 'ready' | 'not-found' | 'error';

/**
 * 一部古谱的目录。**哪一部由路由说**,而不是这一页写死。
 *
 * **纯读**,不需要登录:它们是明清的公开著作,而回放页要求身份是因为它暴露的是
 * 具体用户的对局。
 *
 * 分组不在这里算:服务端已经按局分好,而局号是线路自己的列。**这一页因此不知道
 * 「一共几局」** —— 硬编码 8 在《梅花谱》上曾经是对的,而它在第二部谱上就错了。
 *
 * **没有分组层的谱不画分组标题**:六辑残局的局号一律 0,而给它们编一个「第0局」
 * 是编数据。这一页据 `grouped` 决定画不画那一行,而 `grouped` 是服务端的字段。
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
  private readonly route = inject(ActivatedRoute);

  protected readonly catalogue = signal<Catalogue | null>(null);
  protected readonly phase = signal<Phase>('loading');

  /** 总条数 —— 由数据算出,给标题用一句「31 条线路」。 */
  protected readonly lineCount = computed(() =>
    (this.catalogue()?.chapters ?? []).reduce((n, c) => n + c.lines.length, 0),
  );

  /** 骨架屏的格子数。只为占位,不表达任何真实数量。 */
  protected readonly skeletons = [1, 2, 3, 4, 5, 6];

  private manualKey = '';

  ngOnInit(): void {
    this.manualKey = this.route.snapshot.paramMap.get('manualKey') ?? '';
    if (this.manualKey === '') {
      this.phase.set('not-found');
      return;
    }
    this.load();
  }

  protected load(): void {
    if (this.manualKey === '') return;
    this.phase.set('loading');
    this.api.getCatalogue(this.manualKey).subscribe({
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
