import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import type { Observable } from 'rxjs';
import type { GameEndedDto, RoomState, RoomSummary } from './models/room.model';

export abstract class RoomsApiService {
  abstract list(): Observable<readonly RoomSummary[]>;
  abstract myActiveRooms(): Observable<readonly RoomSummary[]>;
  abstract getById(roomId: string): Observable<RoomState>;
  abstract create(name: string): Observable<RoomSummary>;
  abstract join(roomId: string): Observable<RoomState>;
  abstract leave(roomId: string): Observable<void>;
  abstract spectate(roomId: string): Observable<void>;
  abstract resign(roomId: string): Observable<GameEndedDto>;
}

@Injectable({ providedIn: 'root' })
export class DefaultRoomsApiService extends RoomsApiService {
  private readonly http = inject(HttpClient);

  list(): Observable<readonly RoomSummary[]> {
    return this.http.get<readonly RoomSummary[]>('/api/rooms');
  }

  myActiveRooms(): Observable<readonly RoomSummary[]> {
    return this.http.get<readonly RoomSummary[]>('/api/users/me/active-rooms');
  }

  getById(roomId: string): Observable<RoomState> {
    return this.http.get<RoomState>(`/api/rooms/${encodeURIComponent(roomId)}`);
  }

  create(name: string): Observable<RoomSummary> {
    return this.http.post<RoomSummary>('/api/rooms', { name });
  }

  join(roomId: string): Observable<RoomState> {
    return this.http.post<RoomState>(`/api/rooms/${encodeURIComponent(roomId)}/join`, {});
  }

  leave(roomId: string): Observable<void> {
    return this.http.post<void>(`/api/rooms/${encodeURIComponent(roomId)}/leave`, {});
  }

  spectate(roomId: string): Observable<void> {
    return this.http.post<void>(`/api/rooms/${encodeURIComponent(roomId)}/spectate`, {});
  }

  resign(roomId: string): Observable<GameEndedDto> {
    return this.http.post<GameEndedDto>(`/api/rooms/${encodeURIComponent(roomId)}/resign`, {});
  }
}
