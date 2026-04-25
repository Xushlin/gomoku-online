import { DIALOG_DATA, DialogRef } from '@angular/cdk/dialog';
import { TestBed } from '@angular/core/testing';
import { TranslocoTestingModule } from '@jsverse/transloco';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { GameEndedDialog, type GameEndedDialogData } from './game-ended-dialog';

function mount(data: GameEndedDialogData) {
  const dialogRef = { close: vi.fn() };
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    imports: [
      GameEndedDialog,
      TranslocoTestingModule.forRoot({
        langs: {
          en: {
            game: {
              ended: {
                'title-win': 'You won!',
                'title-lose': 'You lost.',
                'title-draw': 'Draw.',
                'reason-connected-5': 'Five in a row.',
                'reason-resigned': 'Opponent resigned.',
                'reason-timeout': 'Turn timed out.',
                'back-to-lobby': 'Back to lobby',
                'view-replay': 'View replay',
                dismiss: 'Stay',
              },
            },
          },
        },
        translocoConfig: { availableLangs: ['en'], defaultLang: 'en' },
        preloadLangs: true,
      }),
    ],
    providers: [
      { provide: DialogRef, useValue: dialogRef },
      { provide: DIALOG_DATA, useValue: data },
    ],
  });
  const fixture = TestBed.createComponent(GameEndedDialog);
  fixture.detectChanges();
  return { fixture, dialogRef };
}

describe('GameEndedDialog', () => {
  beforeEach(() => TestBed.resetTestingModule());

  it('Black wins and I am Black → title-win', () => {
    const { fixture } = mount({
      result: 'BlackWin',
      winnerUserId: 'u-1',
      endReason: 'Connected5',
      mySide: 'black',
      roomId: 'r-1',
    });
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('You won!');
  });

  it('Black wins and I am White → title-lose', () => {
    const { fixture } = mount({
      result: 'BlackWin',
      winnerUserId: 'u-1',
      endReason: 'Resigned',
      mySide: 'white',
      roomId: 'r-1',
    });
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('You lost.');
  });

  it('Draw shows draw title', () => {
    const { fixture } = mount({
      result: 'Draw',
      winnerUserId: null,
      endReason: 'Connected5',
      mySide: 'black',
      roomId: 'r-1',
    });
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Draw.');
  });

  it('Back-to-lobby button closes with "home"', () => {
    const { fixture, dialogRef } = mount({
      result: 'Draw',
      winnerUserId: null,
      endReason: 'Connected5',
      mySide: 'spectator',
      roomId: 'r-1',
    });
    const buttons = fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>;
    // [Dismiss, View-replay, Back-to-lobby] — back-to-lobby is the third (index 2)
    buttons[2].click();
    expect(dialogRef.close).toHaveBeenCalledWith('home');
  });

  it('View-replay button closes with "replay"', () => {
    const { fixture, dialogRef } = mount({
      result: 'BlackWin',
      winnerUserId: 'u-1',
      endReason: 'Connected5',
      mySide: 'black',
      roomId: 'r-1',
    });
    const buttons = fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>;
    buttons[1].click();
    expect(dialogRef.close).toHaveBeenCalledWith('replay');
  });

  it('Dismiss button closes with "stay"', () => {
    const { fixture, dialogRef } = mount({
      result: 'Draw',
      winnerUserId: null,
      endReason: 'Connected5',
      mySide: 'spectator',
      roomId: 'r-1',
    });
    const buttons = fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>;
    buttons[0].click();
    expect(dialogRef.close).toHaveBeenCalledWith('stay');
  });
});
