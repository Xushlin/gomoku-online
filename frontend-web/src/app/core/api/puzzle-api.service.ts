import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { map, type Observable } from 'rxjs';
import type {
  PuzzleAttemptStarted,
  PuzzleCheckResult,
  PuzzleCheckResultDto,
  PuzzleHint,
  PuzzleHintDto,
  PuzzleLevelDetail,
  PuzzleLevelSummary,
  PuzzleProgress,
  PuzzleSubmitResult,
} from './models/puzzle.model';

/**
 * Abstract DI token for the single-player puzzle API. Consumers MUST inject
 * this rather than the default implementation so specs can supply a stub.
 *
 * The service owns the double parse: `payloadJson` / `revealedJson` / `layoutJson`
 * are JSON strings *inside* a JSON response — the price of a platform that does
 * not understand game payloads and therefore cannot embed them as objects. That
 * unwrapping happens here, once, so no component ever handles a raw string.
 */
export abstract class PuzzleApiService {
  abstract listLevels(gameKey: string): Observable<readonly PuzzleLevelSummary[]>;
  abstract getLevel(gameKey: string, levelIndex: number): Observable<PuzzleLevelDetail>;
  abstract getProgress(gameKey: string): Observable<PuzzleProgress>;
  abstract startAttempt(gameKey: string, levelIndex: number): Observable<PuzzleAttemptStarted>;
  abstract check<TSolved = unknown>(
    attemptId: string,
    partial: unknown,
  ): Observable<PuzzleCheckResult<TSolved>>;
  abstract hint<TRevealed = unknown>(
    attemptId: string,
    state?: unknown,
  ): Observable<PuzzleHint<TRevealed>>;
  abstract submit(attemptId: string, submission: unknown): Observable<PuzzleSubmitResult>;

  /**
   * Parse a level's opaque `layoutJson` into that game's layout shape.
   *
   * Generic because the platform does not understand layouts — it only knows
   * they are JSON. This used to return `CrosswordLayout`, which put one game's
   * shape on the shared client; 华容道 is the game that exposed it.
   */
  abstract parseLayout<TLayout>(layoutJson: string): TLayout | null;
}

/**
 * Parse a nested JSON string, yielding `null` instead of throwing.
 *
 * A broken slip should not take down a solved puzzle: the player has already
 * earned that word, and losing its gloss is a cosmetic failure, not a reason to
 * lose the level.
 */
function parseNested<T>(json: string | null | undefined): T | null {
  if (!json) return null;
  try {
    return JSON.parse(json) as T;
  } catch {
    return null;
  }
}

@Injectable()
export class DefaultPuzzleApiService extends PuzzleApiService {
  private readonly http = inject(HttpClient);

  listLevels(gameKey: string): Observable<readonly PuzzleLevelSummary[]> {
    return this.http.get<readonly PuzzleLevelSummary[]>(
      `/api/games/${encodeURIComponent(gameKey)}/levels`,
    );
  }

  getLevel(gameKey: string, levelIndex: number): Observable<PuzzleLevelDetail> {
    return this.http.get<PuzzleLevelDetail>(
      `/api/games/${encodeURIComponent(gameKey)}/levels/${levelIndex}`,
    );
  }

  getProgress(gameKey: string): Observable<PuzzleProgress> {
    return this.http.get<PuzzleProgress>(`/api/games/${encodeURIComponent(gameKey)}/progress`);
  }

  startAttempt(gameKey: string, levelIndex: number): Observable<PuzzleAttemptStarted> {
    return this.http.post<PuzzleAttemptStarted>(
      `/api/games/${encodeURIComponent(gameKey)}/levels/${levelIndex}/attempts`,
      {},
    );
  }

  check<TSolved = unknown>(
    attemptId: string,
    partial: unknown,
  ): Observable<PuzzleCheckResult<TSolved>> {
    return this.http
      .post<PuzzleCheckResultDto>(`/api/puzzle-attempts/${encodeURIComponent(attemptId)}/check`, {
        partialJson: JSON.stringify(partial),
      })
      .pipe(
        map((dto) => ({
          isCorrect: dto.isCorrect,
          mistakes: dto.mistakes,
          solved: parseNested<TSolved>(dto.payloadJson),
        })),
      );
  }

  hint<TRevealed = unknown>(
    attemptId: string,
    state?: unknown,
  ): Observable<PuzzleHint<TRevealed>> {
    return this.http
      .post<PuzzleHintDto>(`/api/puzzle-attempts/${encodeURIComponent(attemptId)}/hint`, {
        // Same string-inside-JSON shape as check / submit: the platform does not
        // understand game payloads, so it cannot embed them as objects.
        stateJson: state === undefined ? null : JSON.stringify(state),
      })
      .pipe(
        map((dto) => ({
          revealed: parseNested<TRevealed>(dto.revealedJson),
          hintsUsed: dto.hintsUsed,
        })),
      );
  }

  submit(attemptId: string, submission: unknown): Observable<PuzzleSubmitResult> {
    return this.http.post<PuzzleSubmitResult>(
      `/api/puzzle-attempts/${encodeURIComponent(attemptId)}/submit`,
      { submissionJson: JSON.stringify(submission) },
    );
  }

  parseLayout<TLayout>(layoutJson: string): TLayout | null {
    return parseNested<TLayout>(layoutJson);
  }
}
