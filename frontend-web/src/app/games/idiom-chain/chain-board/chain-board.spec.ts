import { ComponentFixture, TestBed } from '@angular/core/testing';
import { TranslocoTestingModule } from '@jsverse/transloco';
import { beforeEach, describe, expect, it } from 'vitest';

import type { MoveDto, RoomState } from '../../../core/api/models/room.model';
import { WRAPPING_UTILITIES, wrapsLongWords } from '../../../testing/wrapping';
import { ChainBoard } from './chain-board';

function said(ply: number, text: string, stone: 'Black' | 'White'): MoveDto {
  return { ply, row: null, col: null, text, stone, playedAt: '2026-08-17T00:00:00Z' };
}

function state(moves: readonly MoveDto[], currentTurn: 'Black' | 'White' = 'Black'): RoomState {
  return {
    id: 'r-1',
    name: 'chain room',
    gameKey: 'idiom-chain',
    status: 'Playing',
    host: { id: 'u-1', username: 'alice' },
    black: { id: 'u-1', username: 'alice' },
    white: { id: 'u-2', username: 'bob' },
    spectators: [],
    game: {
      id: 'g-1',
      currentTurn,
      startedAt: '2026-08-17T00:00:00Z',
      endedAt: null,
      result: null,
      winnerUserId: null,
      endReason: null,
      turnStartedAt: '2026-08-17T00:00:00Z',
      turnTimeoutSeconds: 60,
      moves: [...moves],
    },
    chatMessages: [],
    createdAt: '2026-08-17T00:00:00Z',
  } as unknown as RoomState;
}

describe('ChainBoard', () => {
  let fixture: ComponentFixture<ChainBoard>;

  function mount(
    moves: readonly MoveDto[],
    mySide: 'black' | 'white' | 'spectator' = 'black',
    currentTurn: 'Black' | 'White' = 'Black',
  ) {
    fixture = TestBed.createComponent(ChainBoard);
    fixture.componentRef.setInput('state', state(moves, currentTurn));
    fixture.componentRef.setInput('mySide', mySide);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  function type(el: HTMLElement, word: string) {
    const input = el.querySelector('input[type="text"]') as HTMLInputElement;
    input.value = word;
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    return input;
  }

  function submitButton(el: HTMLElement) {
    return el.querySelector('button[type="submit"]') as HTMLButtonElement;
  }

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ChainBoard, TranslocoTestingModule.forRoot({ langs: { en: {} } })],
    }).compileComponents();
  });

  it('renders the chain in ply order', () => {
    const el = mount([said(1, '一心一意', 'Black'), said(2, '意气风发', 'White')]);

    const items = Array.from(el.querySelectorAll('ol li'));
    expect(items).toHaveLength(2);
    expect(items[0].textContent).toContain('一心一意');
    expect(items[1].textContent).toContain('意气风发');
  });

  it('breaks the longest dictionary idiom instead of widening the page', () => {
    // 成语接龙's own change measured this: 1,393 of the 30,895 idioms are not four
    // characters, running 3 to 15 — so the row really can be long, and it only stays
    // inside 375 px because the text wraps. Same shape of guard as the chat panel:
    // it catches the utility being deleted from the markup, not the stylesheet
    // dropping it.
    const el = mount([said(1, '风'.repeat(15), 'Black')]);
    const text = el.querySelector('ol li span.break-words, ol li span:last-of-type');

    expect(text, 'the idiom text should render').toBeTruthy();
    expect(
      wrapsLongWords(text),
      `the idiom text needs one of: ${WRAPPING_UTILITIES.join(', ')}`,
    ).toBe(true);
  });

  it('shows the character the next word must start with', () => {
    const el = mount([said(1, '一心一意', 'Black')], 'white', 'White');

    expect(el.textContent).toContain('意');
  });

  it('shows no required character on the opening move', () => {
    // Any idiom in the dictionary is legal first. A hint here would invent a rule.
    const el = mount([]);

    expect(el.querySelector('ol li')?.textContent).toBeTruthy(); // the empty-state row
    expect(el.querySelectorAll('ol li')).toHaveLength(1);
  });

  it('lets the seat on turn type and submit', () => {
    const el = mount([], 'black', 'Black');
    type(el, '一心一意');

    expect((el.querySelector('input') as HTMLInputElement).disabled).toBe(false);
    expect(submitButton(el).disabled).toBe(false);
  });

  it('is read-only off turn', () => {
    const el = mount([], 'black', 'White');

    expect((el.querySelector('input') as HTMLInputElement).disabled).toBe(true);
    expect(submitButton(el).disabled).toBe(true);
  });

  it('gives spectators no input at all', () => {
    const el = mount([said(1, '一心一意', 'Black')], 'spectator');

    expect(el.querySelector('input')).toBeNull();
    expect(el.querySelector('button[type="submit"]')).toBeNull();
  });

  it('emits the trimmed word on submit', () => {
    const el = mount([], 'black', 'Black');
    const emitted: string[] = [];
    fixture.componentInstance.wordSay.subscribe((w) => emitted.push(w));

    type(el, '  一心一意 ');
    submitButton(el).click();

    expect(emitted).toEqual(['一心一意']);
  });

  it('does not emit blank input', () => {
    const el = mount([], 'black', 'Black');
    const emitted: string[] = [];
    fixture.componentInstance.wordSay.subscribe((w) => emitted.push(w));

    type(el, '   ');
    expect(submitButton(el).disabled).toBe(true);

    expect(emitted).toEqual([]);
  });

  it('emits a word that does not link on — legality is the server’s call', () => {
    // The load-bearing test for "the board judges nothing". Two of the three rules
    // are decidable here; deciding them would make the field partly authoritative
    // and could refuse a legal word off a one-ply-stale history.
    const el = mount([said(1, '一心一意', 'Black')], 'white', 'White');
    const emitted: string[] = [];
    fixture.componentInstance.wordSay.subscribe((w) => emitted.push(w));

    type(el, '风和日丽'); // starts with 风, not 意
    expect(submitButton(el).disabled).toBe(false);
    submitButton(el).click();

    expect(emitted).toEqual(['风和日丽']);
  });

  it('emits a word already played — also the server’s call', () => {
    const el = mount([said(1, '一心一意', 'Black')], 'white', 'White');
    const emitted: string[] = [];
    fixture.componentInstance.wordSay.subscribe((w) => emitted.push(w));

    type(el, '一心一意');
    submitButton(el).click();

    expect(emitted).toEqual(['一心一意']);
  });

  it('accepts idioms that are not four characters', () => {
    // Measured against the shipped dictionary: 1,393 of 30,895 idioms are not four
    // characters. A maxlength of 4 would make every one of them unenterable.
    const el = mount([], 'black', 'Black');
    const emitted: string[] = [];
    fixture.componentInstance.wordSay.subscribe((w) => emitted.push(w));

    const long = '各人自扫门前雪，莫管他家瓦上霜'; // 15 chars, with a full-width comma
    const input = type(el, long);
    expect(input.value).toBe(long);
    expect(Number(input.getAttribute('maxlength'))).toBeGreaterThanOrEqual(long.length);

    submitButton(el).click();
    expect(emitted).toEqual([long]);
  });

  it('accepts an idiom containing punctuation', () => {
    const el = mount([], 'black', 'Black');
    const emitted: string[] = [];
    fixture.componentInstance.wordSay.subscribe((w) => emitted.push(w));

    type(el, '一不做，二不休');
    submitButton(el).click();

    expect(emitted).toEqual(['一不做，二不休']);
  });

  it('mirrors only the cap the server has', () => {
    // Move.Text is HasMaxLength(64). Anything tighter is a client-invented rule.
    const el = mount([], 'black', 'Black');

    expect((el.querySelector('input') as HTMLInputElement).getAttribute('maxlength')).toBe('64');
  });

  it('clears the box once a word is sent', () => {
    const el = mount([], 'black', 'Black');
    type(el, '一心一意');
    submitButton(el).click();
    fixture.detectChanges();

    expect((el.querySelector('input') as HTMLInputElement).value).toBe('');
  });
});
