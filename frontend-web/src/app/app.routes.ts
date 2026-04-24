import { Routes } from '@angular/router';
import { authGuard, guestGuard } from './core/auth/auth.guards';
import { Lobby } from './pages/lobby/lobby';

/**
 * Root routing contract:
 *   - `home` is eager (part of the shell bundle) and now renders the real Lobby.
 *   - Every other route MUST be lazy via `loadComponent` / `loadChildren`.
 *   - CanMatch guards prevent downloading guarded chunks for ineligible users.
 */
export const routes: Routes = [
  { path: 'home', component: Lobby, canMatch: [authGuard] },
  {
    path: 'login',
    canMatch: [guestGuard],
    loadComponent: () => import('./pages/auth/login/login').then((m) => m.Login),
  },
  {
    path: 'register',
    canMatch: [guestGuard],
    loadComponent: () => import('./pages/auth/register/register').then((m) => m.Register),
  },
  {
    path: 'account/password',
    canMatch: [authGuard],
    loadComponent: () =>
      import('./pages/auth/change-password/change-password').then((m) => m.ChangePassword),
  },
  {
    path: 'rooms/:id',
    canMatch: [authGuard],
    loadComponent: () =>
      import('./pages/rooms/room-page/room-page').then((m) => m.RoomPage),
  },
  { path: '', pathMatch: 'full', redirectTo: 'home' },
  { path: '**', redirectTo: 'home' },
];
