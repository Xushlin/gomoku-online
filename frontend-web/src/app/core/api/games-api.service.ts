import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import type { Observable } from 'rxjs';
import type { GameDescriptor } from './models/game-descriptor.model';

/**
 * Reads the server's versus-game registry (`GET /api/games`).
 *
 * Components MUST inject this abstract token, never the default implementation,
 * so specs can supply a stub — same shape as the other four API services.
 */
export abstract class GamesApiService {
  abstract list(): Observable<readonly GameDescriptor[]>;
}

@Injectable({ providedIn: 'root' })
export class DefaultGamesApiService extends GamesApiService {
  private readonly http = inject(HttpClient);

  list(): Observable<readonly GameDescriptor[]> {
    return this.http.get<readonly GameDescriptor[]>('/api/games');
  }
}
