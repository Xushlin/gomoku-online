import { DialogRef } from '@angular/cdk/dialog';
import { HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
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

function mount() {
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
    ],
  });
  const fixture = TestBed.createComponent(CreateRoomDialog);
  fixture.detectChanges();
  return { fixture, rooms, dialogRef };
}

describe('CreateRoomDialog', () => {
  beforeEach(() => TestBed.resetTestingModule());

  it('submit with valid name calls create() + closes with result', () => {
    const { fixture, rooms, dialogRef } = mount();
    const comp = fixture.componentInstance as unknown as {
      form: { setValue: (v: Record<string, string>) => void };
      submit: () => void;
    };
    comp.form.setValue({ name: 'My room' });
    comp.submit();
    expect(rooms.create).toHaveBeenCalledWith('My room');
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
