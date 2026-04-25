import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { map, Observable } from 'rxjs';
import type { OnlineCountWire, UserPresenceWire } from './models/presence.model';

export abstract class PresenceApiService {
  abstract getOnlineCount(): Observable<number>;
  abstract getUserOnline(userId: string): Observable<boolean>;
}

@Injectable({ providedIn: 'root' })
export class DefaultPresenceApiService extends PresenceApiService {
  private readonly http = inject(HttpClient);

  getOnlineCount(): Observable<number> {
    return this.http
      .get<OnlineCountWire>('/api/presence/online-count')
      .pipe(map((res) => res.count));
  }

  getUserOnline(userId: string): Observable<boolean> {
    return this.http
      .get<UserPresenceWire>(`/api/presence/users/${encodeURIComponent(userId)}`)
      .pipe(map((res) => res.isOnline));
  }
}
