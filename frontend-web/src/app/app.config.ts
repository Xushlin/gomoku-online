import {
  ApplicationConfig,
  inject,
  provideAppInitializer,
  provideBrowserGlobalErrorListeners,
} from '@angular/core';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { TranslocoService } from '@jsverse/transloco';
import { routes } from './app.routes';
import {
  DefaultLeaderboardApiService,
  LeaderboardApiService,
} from './core/api/leaderboard-api.service';
import {
  DefaultPresenceApiService,
  PresenceApiService,
} from './core/api/presence-api.service';
import { DefaultRoomsApiService, RoomsApiService } from './core/api/rooms-api.service';
import { DefaultUsersApiService, UsersApiService } from './core/api/users-api.service';
import { AuthService, DefaultAuthService } from './core/auth/auth.service';
import { authInterceptor } from './core/auth/auth.interceptor';
import { provideAppHttp } from './core/http/http-config';
import { DefaultLanguageService, LanguageService } from './core/i18n/language.service';
import { provideAppI18n } from './core/i18n/transloco-root.config';
import { DefaultGameHubService, GameHubService } from './core/realtime/game-hub.service';
import {
  BoardSkinService,
  DefaultBoardSkinService,
} from './core/theme/board-skin.service';
import { DefaultThemeService, ThemeService } from './core/theme/theme.service';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes, withComponentInputBinding()),
    provideAppHttp([authInterceptor]),
    provideAppI18n(),
    { provide: ThemeService, useClass: DefaultThemeService },
    { provide: BoardSkinService, useClass: DefaultBoardSkinService },
    { provide: LanguageService, useClass: DefaultLanguageService },
    { provide: AuthService, useClass: DefaultAuthService },
    { provide: PresenceApiService, useClass: DefaultPresenceApiService },
    { provide: RoomsApiService, useClass: DefaultRoomsApiService },
    { provide: UsersApiService, useClass: DefaultUsersApiService },
    { provide: LeaderboardApiService, useClass: DefaultLeaderboardApiService },
    { provide: GameHubService, useClass: DefaultGameHubService },
    // Preload i18n + restore session before first paint so the UI boots with
    // the user logged in (if a valid refresh token is stored). Also eagerly
    // constructs ThemeService + BoardSkinService so their `<html>`-attribute
    // side effects land before the first render.
    provideAppInitializer(() => {
      const transloco = inject(TranslocoService);
      const language = inject(LanguageService);
      const auth = inject(AuthService);
      inject(ThemeService);
      inject(BoardSkinService);
      return Promise.all([
        firstValueFrom(transloco.load(language.current())),
        auth.bootstrap(),
      ]).then(() => undefined);
    }),
  ],
};
