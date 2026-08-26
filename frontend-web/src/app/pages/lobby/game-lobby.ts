import { ChangeDetectionStrategy, Component, computed, inject, OnInit } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import {
  DefaultLobbyDataService,
  LobbyDataService,
} from '../../core/lobby/lobby-data.service';
import { LOBBY_GAME_KEY } from '../../core/lobby/lobby-game-key';
import { GameCapabilitiesService } from '../../games/game-capabilities.service';
import { GameCatalogService } from '../../games/game-catalog.service';
import { ActiveRoomsCard } from './cards/active-rooms/active-rooms';
import { AiGameCard } from './cards/ai-game/ai-game';
import { LeaderboardCard } from './cards/leaderboard/leaderboard';

/** Why this game has no lobby, when it has none. */
type Unavailable = 'unknown' | 'ai-only';

/**
 * `/g/:gameKey/lobby` — one game's lobby.
 *
 * The key comes from the route and nowhere else, so a lobby is shareable,
 * bookmarkable and reload-safe, and so the page carries no notion of a
 * "current" or "default" game.
 */
@Component({
  selector: 'app-game-lobby',
  standalone: true,
  imports: [ActiveRoomsCard, AiGameCard, LeaderboardCard, RouterLink, TranslocoPipe],
  templateUrl: './game-lobby.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [
    {
      provide: LOBBY_GAME_KEY,
      useFactory: () => inject(ActivatedRoute).snapshot.paramMap.get('gameKey') ?? '',
    },
    { provide: LobbyDataService, useClass: DefaultLobbyDataService },
  ],
})
export class GameLobby implements OnInit {
  private readonly capabilities = inject(GameCapabilitiesService);
  private readonly catalog = inject(GameCatalogService);

  protected readonly gameKey = inject(LOBBY_GAME_KEY);

  // Wrapped rather than aliased: `= this.capabilities.loaded` hands the
  // template a detached method, and an implementation that reads `this`
  // then reports "not loaded" forever.
  protected readonly loaded = computed(() => this.capabilities.loaded());

  private readonly manifest = computed(() => this.catalog.byKey(this.gameKey));
  private readonly descriptor = computed(() => this.capabilities.of(this.gameKey));

  protected readonly titleKey = computed(
    () => this.manifest()?.titleKey ?? 'lobby.game-lobby.title',
  );

  /**
   * `null` while the page is usable.
   *
   * Only meaningful once `loaded()` is true — before that the descriptor is
   * absent for every key, and painting "unknown game" over a key the client is
   * one response away from recognising is the mistake `remove-manifest-board`
   * named: *the descriptor has not arrived* and *this key is not a game* are
   * different claims.
   */
  protected readonly unavailable = computed<Unavailable | null>(() => {
    if (!this.capabilities.loaded()) return null;
    const descriptor = this.descriptor();
    if (!descriptor) return 'unknown';
    return descriptor.supportsHumanVsHuman ? null : 'ai-only';
  });

  /** Where the "this game is AI-only" panel sends the player. */
  protected readonly aiOnlyRoute = computed(() => this.manifest()?.launchRoute ?? '/games');

  /**
   * The ladder card is hidden for unrated games: a permanently empty board
   * reads as "nobody has played this yet" rather than "this game has no
   * ladder", and the two need to look different.
   */
  protected readonly showLeaderboard = computed(() => !!this.descriptor()?.isRated);

  /**
   * The AI card is hidden for games with no computer opponent.
   *
   * This spot used to render unconditionally, with a written argument that the
   * branch could not be tested until a game had human play and no AI. That
   * argument was right to defer and wrong about the stakes: it reasoned about
   * the *card*, while `POST /api/rooms/ai` never looked at whether a lobby
   * existed. Creating an AI room for 成语接龙 returned 201, seated a bot that
   * could not move, and let the turn timeout hand the human a rated win.
   *
   * So hiding this is presentation. The defence is the server-side validator —
   * see `enforce-ai-availability`. Deleting this line must not make the exploit
   * reachable again, and does not.
   */
  protected readonly showAiCard = computed(() => !!this.descriptor()?.supportsAi);

  /**
   * 古谱入口 —— 来自 **manifest**,不是一句 `gameKey === 'xiangqi'`。
   *
   * 旁边两张卡片按服务端能力开关,而古谱不是服务端能力:它是这个棋种有没有配套资料。
   * 所以判据是 manifest 里填没填,《橘中秘》落地时大厅一行都不用改。
   */
  protected readonly manualRoute = computed(() => this.manifest()?.manualRoute ?? null);

  /** 入口文案。`manualRoute` 有值时它 MUST 也有值 —— 两个一起填是 manifest 的约定。 */
  protected readonly manualLabelKey = computed(() => this.manifest()?.manualLabelKey ?? null);

  ngOnInit(): void {
    this.capabilities.ensureLoaded();
  }
}
