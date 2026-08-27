import { Routes } from '@angular/router';
import { authGuard, guestGuard } from './core/auth/auth.guards';
import { leaveGameGuard } from './core/routing/leave-game.guard';
import { Lobby } from './pages/lobby/lobby';

/**
 * Root routing contract:
 *   - `home` is eager (part of the shell bundle) and now renders the real Lobby.
 *   - Every other route MUST be lazy via `loadComponent` / `loadChildren`.
 *   - CanMatch guards prevent downloading guarded chunks for ineligible users.
 *   - **Every** route carries `leaveGameGuard` — see `withLeaveGuard` below.
 */

/**
 * 给每一条路由挂上离开守卫。
 *
 * 挑「游戏路由」挂的做法会在第十款游戏落地那天漏掉一条,而**漏掉的表现是没有弹框**
 * —— 一个看不出来的缺陷。整条数组 map 一遍之后,就没有「记得挂上」这件事了:决定
 * 「现在离开贵不贵」的是组件上那个可选方法,不是这张表。
 */
const withLeaveGuard = (routes: Routes): Routes =>
  routes.map((route) => ({ ...route, canDeactivate: [leaveGameGuard] }));

export const routes: Routes = withLeaveGuard([
  { path: 'home', component: Lobby, canMatch: [authGuard] },
  {
    path: 'games',
    canMatch: [authGuard],
    loadComponent: () => import('./platform/catalog/catalog').then((m) => m.Catalog),
  },
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
    path: 'g/tictactoe',
    canMatch: [authGuard],
    loadComponent: () =>
      import('./games/tictactoe/ai-game/ai-game').then((m) => m.TicTacToeAiGame),
  },
  {
    /*
     * 古谱 —— **没有 authGuard**,而这不是漏了:它们是明清的公开著作。
     * 回放页要求身份,是因为它暴露的是具体用户的对局,那是另一件事。
     */
    path: 'g/xiangqi/manual',
    // 三层:谱的清单 -> 单谱目录 -> 学习页。学习页带上谱键是为了「返回这部谱」——
    // 线路 id 全局唯一,所以那一段在寻址上冗余,而它承载的是导航。
    // 显式标记「这是公开资料」。走查认这个标记,所以少一个 authGuard 不再是一个洞,
    // 而是一处**写下来的**决定 —— 而走查同时断言豁免名单**恰好**是这两条。
    data: { publicContent: true },
    loadComponent: () =>
      import('./games/xiangqi/manual/manual-list/manual-list').then(
        (m) => m.ManualList,
      ),
  },
  {
    path: 'g/xiangqi/manual/:manualKey',
    data: { publicContent: true },
    loadComponent: () =>
      import('./games/xiangqi/manual/manual-catalogue/manual-catalogue').then(
        (m) => m.ManualCatalogue,
      ),
  },
  {
    path: 'g/xiangqi/manual/:manualKey/:lineId',
    data: { publicContent: true },
    loadComponent: () =>
      import('./games/xiangqi/manual/manual-study/manual-study').then((m) => m.ManualStudy),
  },
  {
    path: 'g/xiangqi',
    canMatch: [authGuard],
    loadComponent: () => import('./games/xiangqi/ai-game/ai-game').then((m) => m.XiangqiAiGame),
  },
  {
    // One game's lobby. The key comes from the URL and nowhere else, so a
    // lobby is shareable, bookmarkable and reload-safe.
    path: 'g/:gameKey/lobby',
    canMatch: [authGuard],
    loadComponent: () => import('./pages/lobby/game-lobby').then((m) => m.GameLobby),
  },
  {
    // Per-game ladder.
    path: 'g/:gameKey/leaderboard',
    canMatch: [authGuard],
    loadComponent: () =>
      import('./pages/leaderboard/leaderboard-page/leaderboard-page').then(
        (m) => m.LeaderboardPage,
      ),
  },
  {
    path: 'g/idiom-crossword',
    canMatch: [authGuard],
    loadComponent: () =>
      import('./games/idiom-crossword/level-list/level-list').then((m) => m.LevelList),
  },
  {
    path: 'g/idiom-crossword/levels/:index',
    canMatch: [authGuard],
    loadComponent: () => import('./games/idiom-crossword/play/play').then((m) => m.Play),
  },
  {
    path: 'g/tetris',
    canMatch: [authGuard],
    loadComponent: () => import('./games/tetris/play/play').then((m) => m.TetrisPlay),
  },
  {
    // A score-attack ladder. Deliberately not `g/:gameKey/leaderboard`: that one is
    // the ELO ladder, whose rows and endpoint are both different.
    path: 'g/:gameKey/scores',
    canMatch: [authGuard],
    loadComponent: () =>
      import('./pages/scores/score-leaderboard-page/score-leaderboard-page').then(
        (m) => m.ScoreLeaderboardPage,
      ),
  },
  {
    path: 'g/klotski',
    canMatch: [authGuard],
    loadComponent: () =>
      import('./games/klotski/level-list/level-list').then((m) => m.KlotskiLevelList),
  },
  {
    path: 'g/klotski/levels/:index',
    canMatch: [authGuard],
    loadComponent: () => import('./games/klotski/play/play').then((m) => m.KlotskiPlay),
  },
  {
    path: 'rooms/:id',
    canMatch: [authGuard],
    loadComponent: () =>
      import('./pages/rooms/room-page/room-page').then((m) => m.RoomPage),
  },
  {
    path: 'replay/:id',
    canMatch: [authGuard],
    loadComponent: () =>
      import('./pages/replay/replay-page/replay-page').then((m) => m.ReplayPage),
  },
  {
    path: 'users/:id',
    canMatch: [authGuard],
    loadComponent: () =>
      import('./pages/users/profile-page/profile-page').then((m) => m.ProfilePage),
  },
  { path: '', pathMatch: 'full', redirectTo: 'home' },
  { path: '**', redirectTo: 'home' },
]);
