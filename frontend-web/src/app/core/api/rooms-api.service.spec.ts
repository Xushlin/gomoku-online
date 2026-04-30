import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { describe, expect, it } from 'vitest';
import { DefaultRoomsApiService, RoomsApiService } from './rooms-api.service';
import type { RoomSummary } from './models/room.model';

function setup() {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(),
      provideHttpClientTesting(),
      { provide: RoomsApiService, useClass: DefaultRoomsApiService },
    ],
  });
  return {
    svc: TestBed.inject(RoomsApiService),
    http: TestBed.inject(HttpTestingController),
  };
}

function sampleRoom(overrides: Partial<RoomSummary> = {}): RoomSummary {
  return {
    id: 'r-1',
    name: 'Sample',
    status: 'Waiting',
    host: { id: 'u-1', username: 'alice' },
    black: { id: 'u-1', username: 'alice' },
    white: null,
    spectatorCount: 0,
    createdAt: '2026-04-23T00:00:00Z',
    ...overrides,
  };
}

describe('RoomsApiService', () => {
  it('list() GETs /api/rooms', () => {
    const { svc, http } = setup();
    let data: readonly RoomSummary[] | undefined;
    svc.list().subscribe((v) => (data = v));
    const req = http.expectOne('/api/rooms');
    expect(req.request.method).toBe('GET');
    req.flush([sampleRoom()]);
    expect(data?.length).toBe(1);
    http.verify();
  });

  it('myActiveRooms() GETs /api/users/me/active-rooms', () => {
    const { svc, http } = setup();
    svc.myActiveRooms().subscribe();
    const req = http.expectOne('/api/users/me/active-rooms');
    expect(req.request.method).toBe('GET');
    req.flush([]);
    http.verify();
  });

  it('getById() GETs /api/rooms/{id}', () => {
    const { svc, http } = setup();
    svc.getById('abc 123').subscribe();
    // id MUST be URL-encoded.
    const req = http.expectOne('/api/rooms/abc%20123');
    expect(req.request.method).toBe('GET');
    req.flush({});
    http.verify();
  });

  it('create() POSTs { name } to /api/rooms', () => {
    const { svc, http } = setup();
    svc.create('My room').subscribe();
    const req = http.expectOne('/api/rooms');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ name: 'My room' });
    req.flush(sampleRoom({ name: 'My room' }));
    http.verify();
  });

  it('createAiRoom() POSTs { name, difficulty } to /api/rooms/ai when humanSide omitted', () => {
    const { svc, http } = setup();
    svc.createAiRoom('Hard match', 'Hard').subscribe();
    const req = http.expectOne('/api/rooms/ai');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ name: 'Hard match', difficulty: 'Hard' });
    req.flush({});
    http.verify();
  });

  it('createAiRoom() POSTs { name, difficulty, humanSide } when humanSide given', () => {
    const { svc, http } = setup();
    svc.createAiRoom('Defense', 'Medium', 'White').subscribe();
    const req = http.expectOne('/api/rooms/ai');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({
      name: 'Defense',
      difficulty: 'Medium',
      humanSide: 'White',
    });
    req.flush({});
    http.verify();
  });

  it('join() POSTs to /api/rooms/{id}/join', () => {
    const { svc, http } = setup();
    svc.join('r-1').subscribe();
    const req = http.expectOne('/api/rooms/r-1/join');
    expect(req.request.method).toBe('POST');
    req.flush({});
    http.verify();
  });

  it('leave() POSTs to /api/rooms/{id}/leave', () => {
    const { svc, http } = setup();
    svc.leave('r-1').subscribe();
    const req = http.expectOne('/api/rooms/r-1/leave');
    expect(req.request.method).toBe('POST');
    req.flush(null, { status: 204, statusText: 'No Content' });
    http.verify();
  });

  it('dissolve() DELETEs /api/rooms/{id}', () => {
    const { svc, http } = setup();
    svc.dissolve('r-1').subscribe();
    const req = http.expectOne('/api/rooms/r-1');
    expect(req.request.method).toBe('DELETE');
    req.flush(null, { status: 204, statusText: 'No Content' });
    http.verify();
  });

  it('spectate() POSTs to /api/rooms/{id}/spectate', () => {
    const { svc, http } = setup();
    svc.spectate('r-1').subscribe();
    const req = http.expectOne('/api/rooms/r-1/spectate');
    expect(req.request.method).toBe('POST');
    req.flush(null, { status: 204, statusText: 'No Content' });
    http.verify();
  });

  it('resign() POSTs to /api/rooms/{id}/resign', () => {
    const { svc, http } = setup();
    svc.resign('r-1').subscribe();
    const req = http.expectOne('/api/rooms/r-1/resign');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({});
    req.flush({
      result: 'BlackWin',
      winnerUserId: 'u-2',
      endedAt: '2026-04-24T00:05:00Z',
      endReason: 'Resigned',
    });
    http.verify();
  });

  it('getReplay() GETs /api/rooms/{id}/replay with URL encoding', () => {
    const { svc, http } = setup();
    svc.getReplay('abc 123').subscribe();
    const req = http.expectOne('/api/rooms/abc%20123/replay');
    expect(req.request.method).toBe('GET');
    req.flush({
      roomId: 'abc 123',
      name: 'Room',
      host: { id: 'u-1', username: 'alice' },
      black: { id: 'u-1', username: 'alice' },
      white: { id: 'u-2', username: 'bob' },
      startedAt: '2026-04-24T00:00:00Z',
      endedAt: '2026-04-24T00:05:00Z',
      result: 'BlackWin',
      winnerUserId: 'u-1',
      endReason: 'Connected5',
      moves: [],
    });
    http.verify();
  });
});
