import { DialogRef } from '@angular/cdk/dialog';
import { HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { TranslocoTestingModule } from '@jsverse/transloco';
import { of, throwError } from 'rxjs';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { RoomsApiService } from '../../../../core/api/rooms-api.service';
import { CreateAiRoomDialog } from './create-ai-room-dialog';

class StubRoomsApi {
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  createAiRoom: any = vi.fn(() =>
    of({
      id: 'r-ai-1',
      name: 'AI match',
      status: 'Playing' as const,
      host: { id: 'u-1', username: 'alice' },
      black: { id: 'u-1', username: 'alice' },
      white: { id: 'bot', username: 'AI_Medium' },
      spectators: [],
      game: null,
      chatMessages: [],
      createdAt: '2026-04-25T00:00:00Z',
    }),
  );
}

function mount() {
  const rooms = new StubRoomsApi();
  const dialogRef = { close: vi.fn() };
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    imports: [
      CreateAiRoomDialog,
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
    ],
  });
  const fixture = TestBed.createComponent(CreateAiRoomDialog);
  fixture.detectChanges();
  return { fixture, rooms, dialogRef };
}

describe('CreateAiRoomDialog', () => {
  beforeEach(() => TestBed.resetTestingModule());

  it('default difficulty is Medium', () => {
    const { fixture } = mount();
    const comp = fixture.componentInstance as unknown as {
      form: { controls: { difficulty: { value: string } } };
    };
    expect(comp.form.controls.difficulty.value).toBe('Medium');
  });

  it('default humanSide is Black', () => {
    const { fixture } = mount();
    const comp = fixture.componentInstance as unknown as {
      form: { controls: { humanSide: { value: string } } };
    };
    expect(comp.form.controls.humanSide.value).toBe('Black');
  });

  it('valid submit calls createAiRoom with form values + closes with RoomState', () => {
    const { fixture, rooms, dialogRef } = mount();
    const comp = fixture.componentInstance as unknown as {
      form: {
        setValue: (v: { name: string; difficulty: string; humanSide: string }) => void;
      };
      submit: () => void;
    };
    comp.form.setValue({ name: 'Hard match', difficulty: 'Hard', humanSide: 'Black' });
    comp.submit();
    expect(rooms.createAiRoom).toHaveBeenCalledWith('Hard match', 'Hard', 'Black');
    expect(dialogRef.close).toHaveBeenCalledWith(
      expect.objectContaining({ id: 'r-ai-1' }),
    );
  });

  it('picking White flows through to the outgoing arg', () => {
    const { fixture, rooms } = mount();
    const comp = fixture.componentInstance as unknown as {
      pickSide: (s: 'Black' | 'White') => void;
      form: {
        setValue: (v: { name: string; difficulty: string; humanSide: string }) => void;
      };
      submit: () => void;
    };
    comp.form.setValue({ name: 'Defense', difficulty: 'Medium', humanSide: 'Black' });
    comp.pickSide('White');
    comp.submit();
    expect(rooms.createAiRoom).toHaveBeenCalledWith('Defense', 'Medium', 'White');
  });

  it('too-short name blocks submit, no HTTP call', () => {
    const { fixture, rooms } = mount();
    const comp = fixture.componentInstance as unknown as {
      form: {
        setValue: (v: { name: string; difficulty: string; humanSide: string }) => void;
        invalid: boolean;
      };
      submit: () => void;
    };
    comp.form.setValue({ name: 'ab', difficulty: 'Medium', humanSide: 'Black' });
    comp.submit();
    expect(comp.form.invalid).toBe(true);
    expect(rooms.createAiRoom).not.toHaveBeenCalled();
  });

  it('pickDifficulty switches the form value', () => {
    const { fixture } = mount();
    const comp = fixture.componentInstance as unknown as {
      pickDifficulty: (d: 'Easy' | 'Medium' | 'Hard') => void;
      form: { controls: { difficulty: { value: string } } };
    };
    comp.pickDifficulty('Easy');
    expect(comp.form.controls.difficulty.value).toBe('Easy');
  });

  it('400 ProblemDetails maps Name field error', () => {
    const { fixture, rooms } = mount();
    rooms.createAiRoom = vi.fn(() =>
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
        setValue: (v: { name: string; difficulty: string; humanSide: string }) => void;
        controls: { name: { errors: Record<string, unknown> | null } };
      };
      submit: () => void;
    };
    comp.form.setValue({ name: 'Duplicate', difficulty: 'Medium', humanSide: 'Black' });
    comp.submit();
    expect(comp.form.controls.name.errors?.['server']).toBe('Name already taken');
  });
});
