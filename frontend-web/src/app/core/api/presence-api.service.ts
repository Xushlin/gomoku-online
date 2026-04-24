import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { map, Observable } from 'rxjs';
import type { OnlineCountWire } from './models/presence.model';

export abstract class PresenceApiService {
  abstract getOnlineCount(): Observable<number>;
}

@Injectable({ providedIn: 'root' })
export class DefaultPresenceApiService extends PresenceApiService {
  private readonly http = inject(HttpClient);

  getOnlineCount(): Observable<number> {
    return this.http
      .get<OnlineCountWire>('/api/presence/online-count')
      .pipe(map((res) => res.count));
  }
}
