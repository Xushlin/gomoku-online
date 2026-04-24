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
    });
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('You won!');
  });

  it('Black wins and I am White → title-lose', () => {
    const { fixture } = mount({
      result: 'BlackWin',
      winnerUserId: 'u-1',
      endReason: 'Resigned',
      mySide: 'white',
    });
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('You lost.');
  });

  it('Draw shows draw title', () => {
    const { fixture } = mount({
      result: 'Draw',
      winnerUserId: null,
      endReason: 'Connected5',
      mySide: 'black',
    });
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Draw.');
  });

  it('primary button closes with "home"', () => {
    const { fixture, dialogRef } = mount({
      result: 'Draw',
      winnerUserId: null,
      endReason: 'Connected5',
      mySide: 'spectator',
    });
    const buttons = fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>;
    // Dismiss is first, back-to-lobby is second (primary)
    buttons[1].click();
    expect(dialogRef.close).toHaveBeenCalledWith('home');
  });

  it('secondary button closes with "stay"', () => {
    const { fixture, dialogRef } = mount({
      result: 'Draw',
      winnerUserId: null,
      endReason: 'Connected5',
      mySide: 'spectator',
    });
    const buttons = fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>;
    buttons[0].click();
    expect(dialogRef.close).toHaveBeenCalledWith('stay');
  });
});
