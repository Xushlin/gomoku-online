import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { beforeEach, describe, expect, it } from 'vitest';
import { DefaultPuzzleApiService, PuzzleApiService } from './puzzle-api.service';

describe('DefaultPuzzleApiService', () => {
  let api: PuzzleApiService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: PuzzleApiService, useClass: DefaultPuzzleApiService },
      ],
    });
    api = TestBed.inject(PuzzleApiService);
    http = TestBed.inject(HttpTestingController);
  });

  it('lists levels for a game', () => {
    api.listLevels('idiom-crossword').subscribe();
    const req = http.expectOne('/api/games/idiom-crossword/levels');
    expect(req.request.method).toBe('GET');
    req.flush([]);
    http.verify();
  });

  it('gets one level', () => {
    api.getLevel('idiom-crossword', 3).subscribe();
    http.expectOne('/api/games/idiom-crossword/levels/3').flush({});
    http.verify();
  });

  it('starts an attempt', () => {
    api.startAttempt('idiom-crossword', 0).subscribe();
    const req = http.expectOne('/api/games/idiom-crossword/levels/0/attempts');
    expect(req.request.method).toBe('POST');
    req.flush({});
    http.verify();
  });

  it('sends the partial submission as a JSON string', () => {
    api.check('att-1', { slotIndex: 0, word: '合而为一' }).subscribe();

    const req = http.expectOne('/api/puzzle-attempts/att-1/check');
    // The wire contract is a *string* — the platform does not understand game
    // payloads, so it cannot embed them as objects.
    expect(req.request.body).toEqual({
      partialJson: JSON.stringify({ slotIndex: 0, word: '合而为一' }),
    });
    req.flush({ isCorrect: false, mistakes: 1, payloadJson: null });
    http.verify();
  });

  it('parses the nested payload of a correct check', () => {
    let solved: unknown = 'unset';
    api.check('att-1', {}).subscribe((r) => (solved = r.solved));

    http.expectOne('/api/puzzle-attempts/att-1/check').flush({
      isCorrect: true,
      mistakes: 2,
      payloadJson: JSON.stringify({ index: 0, word: '合而为一', explanation: '合成一个整体。' }),
    });

    expect(solved).toEqual({ index: 0, word: '合而为一', explanation: '合成一个整体。' });
  });

  it('yields a null payload when the verdict is wrong', () => {
    let result: { solved: unknown; mistakes: number } | undefined;
    api.check('att-1', {}).subscribe((r) => (result = r));

    http
      .expectOne('/api/puzzle-attempts/att-1/check')
      .flush({ isCorrect: false, mistakes: 3, payloadJson: null });

    expect(result?.solved).toBeNull();
    expect(result?.mistakes).toBe(3);
  });

  it('treats a malformed payload as null rather than throwing', () => {
    // A broken slip must not take down a solved puzzle.
    let result: { isCorrect: boolean; solved: unknown } | undefined;
    api.check('att-1', {}).subscribe((r) => (result = r));

    http
      .expectOne('/api/puzzle-attempts/att-1/check')
      .flush({ isCorrect: true, mistakes: 0, payloadJson: '{not json' });

    expect(result?.isCorrect).toBe(true);
    expect(result?.solved).toBeNull();
  });

  it('parses the revealed cell of a hint', () => {
    let hint: { revealed: unknown; hintsUsed: number } | undefined;
    api.hint('att-1').subscribe((h) => (hint = h));

    http
      .expectOne('/api/puzzle-attempts/att-1/hint')
      .flush({ revealedJson: JSON.stringify({ row: 0, col: 1, char: '而' }), hintsUsed: 1 });

    expect(hint?.revealed).toEqual({ row: 0, col: 1, char: '而' });
    expect(hint?.hintsUsed).toBe(1);
  });

  it('sends the submission as a JSON string and returns the result as-is', () => {
    let result: { stars: number | null } | undefined;
    api.submit('att-1', { cells: { '0,0': '合' } }).subscribe((r) => (result = r));

    const req = http.expectOne('/api/puzzle-attempts/att-1/submit');
    expect(req.request.body).toEqual({
      submissionJson: JSON.stringify({ cells: { '0,0': '合' } }),
    });
    req.flush({ isCorrect: true, stars: 3, durationMs: 1000, mistakes: 0, hintsUsed: 0, newBest: true });

    expect(result?.stars).toBe(3);
  });

  it('parses a level layout, and yields null for a broken one', () => {
    expect(api.parseLayout('{"rows":4,"cols":4,"cells":[],"given":[],"tray":[],"slots":[]}'))
      .toMatchObject({ rows: 4, cols: 4 });
    expect(api.parseLayout('nonsense')).toBeNull();
  });

  it('encodes ids into the path', () => {
    api.check('a/b', {}).subscribe();
    http.expectOne('/api/puzzle-attempts/a%2Fb/check').flush({
      isCorrect: false,
      mistakes: 0,
      payloadJson: null,
    });
    http.verify();
  });
});
