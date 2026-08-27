import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { AuthService } from '../../../../core/auth/auth.service';
import { ManualApiService } from '../../../../core/api/manual-api.service';
import { RoomsApiService } from '../../../../core/api/rooms-api.service';
import type { ManualLine } from '../../../../core/api/models/manual.model';
import type { MoveDto, RoomState, UserSummary } from '../../../../core/api/models/room.model';
import { MoveScrubber } from '../../../../platform/move-scrubber/move-scrubber';
import { FIRST_SEAT } from '../../../board-seats';
import { XIANGQI_ENDGAME_KEY } from '../../game-key';
import { XiangqiBoard } from '../../board/xiangqi-board';

type Phase = 'loading' | 'ready' | 'not-found' | 'error';

/** 满盘的子数。少于它就是残局 —— 而「满盘」不等于「标准开局」,见 `natureKey`。 */
const FULL_BOARD_PIECES = 32;

/**
 * 古谱不是对局,所以没有对手 —— 而 `RoomState.host` 不可空。
 *
 * 这里给的是**空字符串,不是编造的名字**:量过,`XiangqiBoard` 只读 `state().game` 与
 * `state().status`,这些字段一个都不画。`check-source-rules.mjs` 里有一条围栏钉着这件事,
 * 所以哪天有人让棋盘去读 `host`,那是 lint 红,而不是页面上一个空白的用户名。
 */
const NO_PLAYER: UserSummary = { id: '', username: '' };

/**
 * 一条古谱的学习页 —— 只读棋盘 + 共享 scrubber。
 *
 * 它把古谱那份**窄**的着法映射成棋盘要的 `RoomState`。这一步在页面边界上做,与回放页
 * 合成 `RoomState` 同一类映射;好处是棋谱端点不必为了「形状一致」给三百年前的每一手
 * 编一个时间戳,而**一个假的时间戳看起来和真的一模一样**。
 */
@Component({
  selector: 'app-manual-study',
  standalone: true,
  imports: [MoveScrubber, RouterLink, TranslocoPipe, XiangqiBoard],
  templateUrl: './manual-study.html',
  styles: [':host { display: block; width: 100%; }'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ManualStudy implements OnInit {
  private readonly api = inject(ManualApiService);
  private readonly rooms = inject(RoomsApiService);
  private readonly router = inject(Router);
  private readonly auth = inject(AuthService);
  private readonly route = inject(ActivatedRoute);

  protected readonly line = signal<ManualLine | null>(null);
  protected readonly phase = signal<Phase>('loading');
  protected readonly currentPly = signal(0);

  /** 正在开房。按钮上要有它 —— 否则连点两下会开出两间房。 */
  protected readonly opening = signal(false);

  /** 开房失败时的横幅键;`null` = 没有失败。 */
  protected readonly openErrorKey = signal<string | null>(null);

  /**
   * 登录了才画「摆此局对弈」。
   *
   * 古谱页是**匿名可读**的(`data.publicContent`),而建房要认证。不 gate 的话,匿名
   * 读者点下去拿到的是一次 401 —— 一个只在失败时才告诉你「你得先登录」的按钮。
   */
  protected readonly canPlay = this.auth.isAuthenticated;

  private lineId: number | null = null;

  protected readonly totalMoves = computed(() => this.line()?.moves.length ?? 0);

  /**
   * 谱主的评断 —— **不是「将死」**。量过:《梅花谱》31 条线路里只有 11 条以杀棋收,
   * 20 条走到「优势已成」就停。把它说成将死,在那 20 条上是错的,而错的样子和对的样子
   * 在界面上完全一样。
   *
   * 键**从取值推**(`manual.verdict.<verdict>`),而不是一串 `if`:四态之外将来若再加一态,
   * 缺的是一个 i18n 键(双语对齐那条测试会红),而不是一个静静落到「红优」的分支。
   */
  protected readonly verdictKey = computed(() => {
    const verdict = this.line()?.verdict;
    return verdict === undefined ? null : `manual.verdict.${verdict}`;
  });

  /**
   * 这条是残局还是满盘 —— **由子数推**,而不是按谱名判断。
   *
   * 32 子是满盘。注意它 MUST NOT 被读成「标准开局」:实测有 6 局是 32 子却不是标准摆法。
   */
  protected readonly natureKey = computed(() => {
    const start = this.line()?.startPosition;
    if (start === undefined) return null;
    const pieces = [...start].filter((c) => c !== '.').length;
    return pieces === FULL_BOARD_PIECES ? 'manual.nature.full' : 'manual.nature.endgame';
  });

  /**
   * 这条记录的起始局面 —— **首帧就是它**。
   *
   * 判据是首帧的子数等于起始局面的子数,而不是「代码里传了起始局面」:改这条之前棋盘
   * 从标准开局重放,一条 10 子的残局会渲染成 32 子加几步棋 —— **一个看起来完全正常的
   * 错盘面**。
   */
  protected readonly startPosition = computed(() => this.line()?.startPosition ?? null);

  /** 棋盘那一帧。`status: 'Finished'` 让共享棋盘进永久只读。 */
  protected readonly boardState = computed<RoomState | null>(() => {
    const line = this.line();
    if (!line) return null;
    const moves: MoveDto[] = line.moves.slice(0, this.currentPly()).map((m) => ({
      ply: m.ply,
      row: m.row,
      col: m.col,
      seat: m.seat,
      // 古谱没有下棋的时间;棋盘不读这个字段(量过),所以这里不编一个像真的时刻。
      playedAt: '',
      fromRow: m.fromRow,
      fromCol: m.fromCol,
      text: null,
    }));
    return {
      id: `manual-${line.id}`,
      name: line.title,
      gameKey: line.gameKey,
      status: 'Finished',
      host: NO_PLAYER,
      black: null,
      white: null,
      spectators: [],
      /*
       * **空数组,而这里它是真话。** 回放页的注释警告过「空数组会让棋盘以为这局没人下」,
       * 那是因为它的 DTO 说不出第三个座位;古谱是**确实没人下过** —— 一部书里的一条变化
       * 没有坐着的人。所以这里给空,而不是给两个空壳玩家。
       */
      seats: [],
      game: {
        id: `manual-${line.id}`,
        currentSeat: FIRST_SEAT,
        startedAt: '',
        endedAt: null,
        result: 'Ongoing',
        winnerUserId: null,
        endReason: null,
        turnStartedAt: '',
        turnTimeoutSeconds: 0,
        moves,
      },
      chatMessages: [],
      createdAt: '',
    };
  });

  ngOnInit(): void {
    const raw = this.route.snapshot.paramMap.get('lineId');
    const id = raw === null ? Number.NaN : Number.parseInt(raw, 10);
    if (Number.isNaN(id)) {
      this.phase.set('not-found');
      return;
    }
    this.lineId = id;
    this.load();
  }

  protected load(): void {
    if (this.lineId === null) return;
    this.phase.set('loading');
    this.api.getLine(this.lineId).subscribe({
      next: (l) => {
        this.line.set(l);
        this.currentPly.set(0);
        this.phase.set('ready');
      },
      error: (err: unknown) => {
        this.phase.set(err instanceof HttpErrorResponse && err.status === 404 ? 'not-found' : 'error');
      },
    });
  }

  /**
   * 摆这一局对弈 —— 开一间从**这条线路的起始局面**开始的房,然后进去等人。
   *
   * **它不违反「古谱只做研习」那条规则,而那条规则也因此收窄了。** 规则守的是
   * *平台不判对错*:领域里没有重复局面 / 长将 / 长捉,所以「你解对了」和「和棋」都判不出来,
   * 而一个判错的判定教错棋。这里开的是一局**由两个人自己下的、正常的**棋 —— 平台一句
   * 判断都不宣布,连和棋都不宣布(残局房的界面写着这句话)。
   *
   * 递给服务端的是**线路 id**,不是盘面:起始局面与先走方从库里那条线路上取。
   *
   * 房名就用这条线路的标题 —— 量过全部 1665 条,长度 4–25,而房名的界是 [3, 50],
   * 所以不需要截断,也不需要一个「截断了怎么办」的分支。
   */
  protected playFromHere(): void {
    const line = this.line();
    if (!line || this.opening() || !this.canPlay()) return;
    this.opening.set(true);
    this.openErrorKey.set(null);
    this.rooms.create(line.title, XIANGQI_ENDGAME_KEY, line.id).subscribe({
      next: (room) => {
        this.opening.set(false);
        void this.router.navigate(['/rooms', room.id]);
      },
      error: () => {
        this.opening.set(false);
        this.openErrorKey.set('manual.play.failed');
      },
    });
  }

  /** scrubber 请求跳到第 N 手。钳制在这里,所以越界的请求不会变成越界的一帧。 */
  protected onScrub(ply: number): void {
    this.currentPly.set(Math.max(0, Math.min(this.totalMoves(), ply)));
  }
}
