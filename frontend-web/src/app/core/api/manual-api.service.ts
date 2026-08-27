import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import type { Observable } from 'rxjs';
import type { ManualCatalogue, ManualLine, ManualSummary } from './models/manual.model';

/**
 * 古谱(《梅花谱》一类)的读取口。抽象类作 DI token,好让 spec 换桩。
 *
 * **只读**,而且**匿名可读** —— 它是公开资料。回放端点要求身份是因为它暴露的是
 * 具体用户的对局,那是另一件事。
 */
export abstract class ManualApiService {
  /** 列出全部古谱。清单来自服务端 —— 加一辑前端不改。 */
  abstract listManuals(): Observable<readonly ManualSummary[]>;

  /** 取一部古谱的目录(按局分组)。 */
  abstract getCatalogue(manualKey: string): Observable<ManualCatalogue>;

  /** 取一条线路的完整着法。 */
  abstract getLine(lineId: number): Observable<ManualLine>;
}

/** HTTP 实现。 */
@Injectable()
export class DefaultManualApiService extends ManualApiService {
  private readonly http = inject(HttpClient);

  /** @inheritdoc */
  listManuals(): Observable<readonly ManualSummary[]> {
    return this.http.get<readonly ManualSummary[]>('/api/manuals/xiangqi');
  }

  /** @inheritdoc */
  getCatalogue(manualKey: string): Observable<ManualCatalogue> {
    return this.http.get<ManualCatalogue>(
      `/api/manuals/xiangqi/${encodeURIComponent(manualKey)}`,
    );
  }

  /** @inheritdoc */
  getLine(lineId: number): Observable<ManualLine> {
    return this.http.get<ManualLine>(`/api/manuals/xiangqi/lines/${lineId}`);
  }
}
