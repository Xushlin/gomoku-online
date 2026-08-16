import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { TranslocoTestingModule } from '@jsverse/transloco';
import { beforeEach, describe, expect, it } from 'vitest';
import type { MoveDto, RoomState, Stone } from '../../../core/api/models/room.model';
import { XiangqiBoard, type PieceMoveEvent } from './xiangqi-board';

function slide(fromRow: number, fromCol: number, row: number, col: number, ply: number, stone: Stone): MoveDto {
  return { ply, row, col, stone, playedAt: '2026-08-16T12:00:00Z', fromRow, fromCol };
}

function roomState(moves: readonly MoveDto[], status: RoomState['status'] = 'Playing'): RoomState {
  const turn: Stone = moves.length % 2 === 0 ? 'Black' : 'White';
  return {
    id: 'room-1',
    name: 'xiangqi',
    gameKey: 'xiangqi',
    status,
    host: { id: 'u1', username: 'red' },
    black: { id: 'u1', username: 'red' },
    white: { id: 'u2', username: 'black' },
    spectators: [],
    game: {
      id: 'g1',
      currentTurn: turn,
      startedAt: '2026-08-16T12:00:00Z',
      endedAt: null,
      result: null,
      winnerUserId: null,
      endReason: null,
      turnStartedAt: '2026-08-16T12:00:00Z',
      turnTimeoutSeconds: 60,
      moves: [...moves],
    },
    chatMessages: [],
    createdAt: '2026-08-16T12:00:00Z',
  } as unknown as RoomState;
}

@Component({
  standalone: true,
  imports: [XiangqiBoard],
  template: `<app-xiangqi-board
    [state]="state()"
    [mySide]="mySide()"
    [submitting]="submitting()"
    [readonly]="readonly()"
    (pieceMove)="emitted.push($event)"
  />`,
})
class Host {
  readonly state = signal<RoomState | null>(roomState([]));
  readonly mySide = signal<'black' | 'white' | 'spectator'>('black');
  readonly submitting = signal(false);
  readonly readonly = signal(false);
  readonly emitted: PieceMoveEvent[] = [];
}

function setup() {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    imports: [
      Host,
      TranslocoTestingModule.forRoot({
        langs: { en: {} },
        translocoConfig: { availableLangs: ['en'], defaultLang: 'en' },
        preloadLangs: true,
      }),
    ],
  });
  const fixture = TestBed.createComponent(Host);
  fixture.detectChanges();
  return { fixture, host: fixture.componentInstance };
}

type Fixture = ReturnType<typeof setup>['fixture'];

function points(fixture: Fixture): HTMLButtonElement[] {
  return Array.from(fixture.nativeElement.querySelectorAll('.xq-point')) as HTMLButtonElement[];
}

/** Buttons are laid out row-major, so index arithmetic mirrors the position model. */
function point(fixture: Fixture, row: number, col: number): HTMLButtonElement {
  return points(fixture)[row * 9 + col];
}

function tap(fixture: Fixture, row: number, col: number): void {
  point(fixture, row, col).click();
  fixture.detectChanges();
}

describe('XiangqiBoard', () => {
  beforeEach(() => TestBed.resetTestingModule());

  it('renders 90 intersections holding 32 pieces', () => {
    const { fixture } = setup();

    expect(points(fixture)).toHaveLength(90);
    expect(fixture.nativeElement.querySelectorAll('.xq-piece')).toHaveLength(32);
  });

  it('paints Stone.Black as red — the bet add-xiangqi placed', () => {
    // 象棋 is red-first and `Game` opens on Stone.Black, so Black *is* red here.
    // This assertion exists so nobody "corrects" it back.
    const { fixture } = setup();

    const redGeneral = point(fixture, 9, 4).querySelector('.xq-piece')!;
    const blackGeneral = point(fixture, 0, 4).querySelector('.xq-piece')!;

    expect(redGeneral.textContent).toBe('帥');
    expect(redGeneral.classList.contains('xq-piece--red')).toBe(true);
    expect(blackGeneral.textContent).toBe('將');
    expect(blackGeneral.classList.contains('xq-piece--black')).toBe(true);
  });

  it('emits from → to after picking a piece and a target', () => {
    const { fixture, host } = setup();

    tap(fixture, 9, 0);
    tap(fixture, 8, 0);

    expect(host.emitted).toEqual([{ from: { row: 9, col: 0 }, to: { row: 8, col: 0 } }]);
  });

  it('tapping the held piece again puts it down', () => {
    const { fixture, host } = setup();

    tap(fixture, 9, 0);
    expect(point(fixture, 9, 0).getAttribute('aria-pressed')).toBe('true');

    tap(fixture, 9, 0);

    expect(point(fixture, 9, 0).getAttribute('aria-pressed')).toBe('false');
    expect(host.emitted).toEqual([]);
  });

  it('tapping another of my pieces re-picks rather than emitting', () => {
    // Emitting here would be a request the server is certain to refuse, and
    // "capture your own piece" is not a thing.
    const { fixture, host } = setup();

    tap(fixture, 9, 0);
    tap(fixture, 9, 1);

    expect(host.emitted).toEqual([]);
    expect(point(fixture, 9, 1).getAttribute('aria-pressed')).toBe('true');
    expect(point(fixture, 9, 0).getAttribute('aria-pressed')).toBe('false');
  });

  it('cannot pick up an enemy piece', () => {
    const { fixture, host } = setup();

    tap(fixture, 0, 0);

    expect(host.emitted).toEqual([]);
    expect(point(fixture, 0, 0).getAttribute('aria-pressed')).toBe('false');
    expect(point(fixture, 0, 0).disabled).toBe(true);
  });

  it('keeps the piece in hand when the move is refused', () => {
    // Nothing lands, so nothing clears — the player almost always wants a
    // different target, not to hunt for the piece again.
    const { fixture } = setup();

    tap(fixture, 9, 0);
    tap(fixture, 4, 0); // whatever the server says, no ply arrives

    expect(point(fixture, 9, 0).getAttribute('aria-pressed')).toBe('true');
  });

  it('drops the held piece once a ply lands', () => {
    const { fixture, host } = setup();

    tap(fixture, 9, 0);
    host.state.set(roomState([slide(9, 0, 8, 0, 1, 'Black')]));
    fixture.detectChanges();

    expect(point(fixture, 9, 0).getAttribute('aria-pressed')).toBe('false');
  });

  it('replays the history onto the board', () => {
    const { fixture, host } = setup();

    host.state.set(roomState([slide(7, 1, 0, 1, 1, 'Black')])); // 炮打马
    fixture.detectChanges();

    expect(point(fixture, 7, 1).querySelector('.xq-piece')).toBeNull();
    expect(point(fixture, 0, 1).querySelector('.xq-piece')!.textContent).toBe('炮');
    expect(fixture.nativeElement.querySelectorAll('.xq-piece')).toHaveLength(31);
  });

  it('marks both ends of the last move', () => {
    const { fixture, host } = setup();

    host.state.set(roomState([slide(9, 0, 8, 0, 1, 'Black')]));
    fixture.detectChanges();

    expect(point(fixture, 9, 0).classList.contains('xq-point--last-from')).toBe(true);
    expect(point(fixture, 8, 0).classList.contains('xq-point--last-to')).toBe(true);
  });

  it('is entirely read-only on the opponent’s turn', () => {
    const { fixture } = setup();

    fixture.componentInstance.mySide.set('white');
    fixture.detectChanges();

    expect(points(fixture).every((b) => b.disabled)).toBe(true);
  });

  it('is entirely read-only for spectators', () => {
    const { fixture } = setup();

    fixture.componentInstance.mySide.set('spectator');
    fixture.detectChanges();

    expect(points(fixture).every((b) => b.disabled)).toBe(true);
  });

  it('is entirely read-only once the game is finished', () => {
    const { fixture, host } = setup();

    host.state.set(roomState([], 'Finished'));
    fixture.detectChanges();

    expect(points(fixture).every((b) => b.disabled)).toBe(true);
  });

  it('is entirely read-only while a move is in flight', () => {
    const { fixture, host } = setup();

    host.submitting.set(true);
    fixture.detectChanges();

    expect(points(fixture).every((b) => b.disabled)).toBe(true);
  });

  it('is entirely read-only in readonly mode', () => {
    const { fixture, host } = setup();

    host.readonly.set(true);
    fixture.detectChanges();

    expect(points(fixture).every((b) => b.disabled)).toBe(true);
  });

  it('only offers my own pieces before one is in hand', () => {
    // Tapping an empty point with nothing held does nothing, so it should not be
    // a tab stop either.
    const { fixture } = setup();

    const enabled = points(fixture).filter((b) => !b.disabled);

    expect(enabled).toHaveLength(16);
  });

  it('escape puts the piece down', () => {
    const { fixture } = setup();

    tap(fixture, 9, 0);
    fixture.nativeElement
      .querySelector('.xq-board')!
      .dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    fixture.detectChanges();

    expect(point(fixture, 9, 0).getAttribute('aria-pressed')).toBe('false');
  });

  it('labels every intersection for a screen reader', () => {
    const { fixture } = setup();

    // The label carries the piece name through i18n; the glyph itself is aria-hidden.
    expect(point(fixture, 9, 4).getAttribute('aria-label')).toContain('xiangqi.board.point');
    expect(point(fixture, 9, 4).querySelector('.xq-piece')!.getAttribute('aria-hidden')).toBe(
      'true',
    );
  });

  it('lets the server judge legality — it offers every point once a piece is held', () => {
    // Design D2, stated as behaviour rather than as a grep. The component knows no
    // rules, so with a piece in hand it must not pre-filter targets: a 俥 blocked by
    // its own soldier is still clickable, and the refusal comes from the server.
    const { fixture } = setup();

    tap(fixture, 9, 0);

    expect(points(fixture).filter((b) => b.disabled)).toHaveLength(0);
  });
});
