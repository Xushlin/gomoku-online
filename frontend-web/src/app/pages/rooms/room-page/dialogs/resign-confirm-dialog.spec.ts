import { DialogRef } from '@angular/cdk/dialog';
import { TestBed } from '@angular/core/testing';
import { TranslocoTestingModule } from '@jsverse/transloco';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ResignConfirmDialog } from './resign-confirm-dialog';

function mount() {
  const dialogRef = { close: vi.fn() };
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    imports: [
      ResignConfirmDialog,
      TranslocoTestingModule.forRoot({
        langs: { en: {} },
        translocoConfig: { availableLangs: ['en'], defaultLang: 'en' },
        preloadLangs: true,
      }),
    ],
    providers: [{ provide: DialogRef, useValue: dialogRef }],
  });
  const fixture = TestBed.createComponent(ResignConfirmDialog);
  fixture.detectChanges();
  return { fixture, dialogRef };
}

describe('ResignConfirmDialog', () => {
  beforeEach(() => TestBed.resetTestingModule());

  it('confirm closes with true', () => {
    const { fixture, dialogRef } = mount();
    const buttons = fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>;
    // Cancel is the first button; Confirm is the second
    buttons[1].click();
    expect(dialogRef.close).toHaveBeenCalledWith(true);
  });

  it('cancel closes with false', () => {
    const { fixture, dialogRef } = mount();
    const buttons = fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>;
    buttons[0].click();
    expect(dialogRef.close).toHaveBeenCalledWith(false);
  });
});
