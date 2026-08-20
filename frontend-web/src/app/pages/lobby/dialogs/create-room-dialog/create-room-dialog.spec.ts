import { DialogRef } from '@angular/cdk/dialog';
import { HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { LOBBY_GAME_KEY } from '../../../../core/lobby/lobby-game-key';
import { TranslocoTestingModule } from '@jsverse/transloco';
import { of, throwError } from 'rxjs';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { RoomsApiService } from '../../../../core/api/rooms-api.service';
import { CreateRoomDialog } from './create-room-dialog';

class StubRoomsApi {
  create = vi.fn(() =>
    of({
      id: 'r-1',
      name: 'My room',
      status: 'Waiting' as const,
      host: { id: 'u-1', username: 'alice' },
      black: null,
      white: null,
      spectatorCount: 0,
      createdAt: '2026-04-23T00:00:00Z',
    }),
  );
}

function mount(gameKey = 'gomoku') {
  const rooms = new StubRoomsApi();
  const dialogRef = { close: vi.fn() };
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    imports: [
      CreateRoomDialog,
      TranslocoTestingModule.forRoot({
        langs: { en: {} },
        translocoConfig: { availableLangs: ['en'], defaultLang: 'en' },
        preloadLangs: true,
      }),
    ],
    providers: [
      provideHttpClient(),
      provideHttpClientTesting(),
      { provide: RoomsApiService, useValue: rooms },
      { provide: DialogRef, useValue: dialogRef },
      { provide: LOBBY_GAME_KEY, useValue: gameKey },
    ],
  });
  const fixture = TestBed.createComponent(CreateRoomDialog);
  fixture.detectChanges();
  return { fixture, rooms, dialogRef };
}

describe('CreateRoomDialog', () => {
  it('the name placeholder names the game of THIS lobby, not a fixed one', () => {
    // **它此前写死成「我的五子棋房」** —— 大厅泛化之后 `/g/:gameKey/lobby` 是一个棋种的大厅,
    // 于是在挖坑的大厅里那句话点名了另一个棋种。用户在屏幕上看见的。
    //
    // **而没有任何测试断言过那句文案,这正是它活下来的原因** —— 规格只列了键名,不列内容。
    //
    // 这条**自带翻译**:共享的 mount 用的是空 `langs`,那样两个棋种渲染出来是同一个键,
    // 断言不出插值有没有发生。要验的正是插值。
    const withTranslations = (gameKey: string): string => {
      TestBed.resetTestingModule();
      TestBed.configureTestingModule({
        imports: [
          CreateRoomDialog,
          TranslocoTestingModule.forRoot({
            langs: {
              en: {
                lobby: { 'create-room': { 'name-placeholder': 'My {{game}} room' } },
                games: { wakeng: { title: 'Wakeng' }, gomoku: { title: 'Gomoku' } },
              },
            },
            translocoConfig: { availableLangs: ['en'], defaultLang: 'en' },
            preloadLangs: true,
          }),
        ],
        providers: [
          provideHttpClient(),
          provideHttpClientTesting(),
          { provide: RoomsApiService, useValue: new StubRoomsApi() },
          { provide: DialogRef, useValue: { close: vi.fn() } },
          { provide: LOBBY_GAME_KEY, useValue: gameKey },
        ],
      });
      const f = TestBed.createComponent(CreateRoomDialog);
      f.detectChanges();
      return (f.nativeElement.querySelector('input') as HTMLInputElement).placeholder;
    };

    expect(withTranslations('wakeng')).toBe('My Wakeng room');
    // 关键的负向:挖坑的大厅里 MUST NOT 出现五子棋。
    expect(withTranslations('wakeng')).not.toContain('Gomoku');
    // 正面对照:五子棋的大厅里它当然还该说五子棋。
    expect(withTranslations('gomoku')).toBe('My Gomoku room');
  });

  beforeEach(() => TestBed.resetTestingModule());

  it('submit with valid name calls create() + closes with result', () => {
    const { fixture, rooms, dialogRef } = mount();
    const comp = fixture.componentInstance as unknown as {
      form: { setValue: (v: Record<string, string>) => void };
      submit: () => void;
    };
    comp.form.setValue({ name: 'My room' });
    comp.submit();
    expect(rooms.create).toHaveBeenCalledWith('My room', 'gomoku');
    expect(dialogRef.close).toHaveBeenCalledWith(
      expect.objectContaining({ id: 'r-1' }),
    );
  });

  it('too-short name blocks submit, no HTTP call', () => {
    const { fixture, rooms } = mount();
    const comp = fixture.componentInstance as unknown as {
      form: { setValue: (v: Record<string, string>) => void; invalid: boolean };
      submit: () => void;
    };
    comp.form.setValue({ name: 'ab' });
    comp.submit();
    expect(comp.form.invalid).toBe(true);
    expect(rooms.create).not.toHaveBeenCalled();
  });

  it('whitespace-only name is rejected by the pattern validator', () => {
    const { fixture, rooms } = mount();
    const comp = fixture.componentInstance as unknown as {
      form: {
        setValue: (v: Record<string, string>) => void;
        invalid: boolean;
        controls: { name: { errors: Record<string, unknown> | null } };
      };
      submit: () => void;
    };
    comp.form.setValue({ name: '     ' });
    comp.submit();
    expect(comp.form.controls.name.errors?.['pattern']).toBeTruthy();
    expect(rooms.create).not.toHaveBeenCalled();
  });

  it('400 with ProblemDetails.errors.Name maps to field', () => {
    const { fixture, rooms } = mount();
    rooms.create = vi.fn(() =>
      throwError(
        () =>
          new HttpErrorResponse({
            status: 400,
            error: { errors: { Name: ['Name already taken'] } },
          }),
      ),
    );
    const comp = fixture.componentInstance as unknown as {
      form: {
        setValue: (v: Record<string, string>) => void;
        controls: { name: { errors: Record<string, unknown> | null } };
      };
      submit: () => void;
    };
    comp.form.setValue({ name: 'Duplicate' });
    comp.submit();
    expect(comp.form.controls.name.errors?.['server']).toBe('Name already taken');
  });
});
