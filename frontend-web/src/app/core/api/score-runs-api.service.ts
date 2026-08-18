import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import type { Observable } from 'rxjs';
import type { PagedResult } from './models/leaderboard.model';
import type {
  ScoreLeaderboardEntry,
  ScoreRunResult,
  ScoreRunStarted,
  ScoreWindow,
  SubmitScoreRunBody,
} from './models/score-run.model';

/**
 * Score-attack run lifecycle and leaderboard.
 *
 * `gameKey` is required on `start` and `leaderboard` — the same discipline the
 * rest of this client follows since `require-room-game-key`: a service that
 * guesses which game it is being asked about turns "forgot to pass it" into
 * "silently served another game's data", and that mistake is invisible on screen.
 *
 * An abstract class rather than an interface so it can be a DI token, matching
 * {@link LeaderboardApiService} and the other API services.
 */
export abstract class ScoreRunsApiService {
  /**
   * Open a run and get the server's seed.
   *
   * There is deliberately no seed parameter: the client must not be able to pick
   * the piece sequence, or it could choose a favourable one and the replay would
   * still pass — those placements really are legal.
   */
  abstract start(gameKey: string): Observable<ScoreRunStarted>;

  /** Submit the placement list. Every returned number is the server's. */
  abstract submit(runId: string, body: SubmitScoreRunBody): Observable<ScoreRunResult>;

  abstract leaderboard(
    gameKey: string,
    window: ScoreWindow,
    page: number,
    pageSize: number,
  ): Observable<PagedResult<ScoreLeaderboardEntry>>;
}

@Injectable({ providedIn: 'root' })
export class DefaultScoreRunsApiService extends ScoreRunsApiService {
  private readonly http = inject(HttpClient);

  start(gameKey: string): Observable<ScoreRunStarted> {
    return this.http.post<ScoreRunStarted>('/api/score-runs', { gameKey });
  }

  submit(runId: string, body: SubmitScoreRunBody): Observable<ScoreRunResult> {
    return this.http.post<ScoreRunResult>(`/api/score-runs/${runId}/submit`, body);
  }

  leaderboard(
    gameKey: string,
    window: ScoreWindow,
    page: number,
    pageSize: number,
  ): Observable<PagedResult<ScoreLeaderboardEntry>> {
    const params = new HttpParams()
      .set('gameKey', gameKey)
      .set('window', window)
      .set('page', String(page))
      .set('pageSize', String(pageSize));
    return this.http.get<PagedResult<ScoreLeaderboardEntry>>('/api/score-runs/leaderboard', {
      params,
    });
  }
}
