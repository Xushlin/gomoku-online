import { Dialog } from '@angular/cdk/dialog';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { TranslocoTestingModule } from '@jsverse/transloco';
import { of } from 'rxjs';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { AiGameCard } from './ai-game';

function routerStub() {
  return {
    navigate: vi.fn(() => Promise.resolve(true)),
    navigateByUrl: vi.fn(() => Promise.resolve(true)),
    createUrlTree: vi.fn(() => ({ toString: () => '/' })),
    serializeUrl: vi.fn(() => '/'),
    events: of(),
  };
}

function mount(closedValue: unknown) {
  const dialog = {
    open: vi.fn(() => ({ closed: of(closedValue) })),
  };
  const router = routerStub();
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    imports: [
      AiGameCard,
      TranslocoTestingModule.forRoot({
        langs: { en: {} },
        translocoConfig: { availableLangs: ['en'], defaultLang: 'en' },
        preloadLangs: true,
      }),
    ],
    providers: [
      { provide: Dialog, useValue: dialog },
      { provide: Router, useValue: router },
    ],
  });
  const fixture = TestBed.createComponent(AiGameCard);
  fixture.detectChanges();
  return { fixture, dialog, router };
}

describe('AiGameCard', () => {
  beforeEach(() => TestBed.resetTestingModule());

  it('clicking the button opens the dialog', () => {
    const { fixture, dialog } = mount(undefined);
    const btn = fixture.nativeElement.querySelector(
      'button[type="button"]',
    ) as HTMLButtonElement;
    btn.click();
    expect(dialog.open).toHaveBeenCalled();
  });

  it('navigates /rooms/:id when dialog closes with a RoomState', () => {
    const room = { id: 'r-ai-1' };
    const { fixture, router } = mount(room);
    (
      fixture.nativeElement.querySelector('button[type="button"]') as HTMLButtonElement
    ).click();
    expect(router.navigateByUrl).toHaveBeenCalledWith('/rooms/r-ai-1');
  });

  it('does not navigate when dialog closes with undefined (cancel)', () => {
    const { fixture, router } = mount(undefined);
    (
      fixture.nativeElement.querySelector('button[type="button"]') as HTMLButtonElement
    ).click();
    expect(router.navigateByUrl).not.toHaveBeenCalled();
  });
});
