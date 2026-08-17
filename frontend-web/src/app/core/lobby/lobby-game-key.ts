import { InjectionToken } from '@angular/core';

/**
 * The game key the current lobby page is showing.
 *
 * Provided by `GameLobby` from its route parameter, so the URL stays the one
 * source of truth for "which game am I looking at" — a lobby can be shared,
 * bookmarked and reloaded. Nothing else may provide it: a component constant
 * or a stored preference would reintroduce exactly the invisible default that
 * `require-room-game-key` removed from the server.
 */
export const LOBBY_GAME_KEY = new InjectionToken<string>('lobby.game-key');
