import { HttpErrorResponse } from '@angular/common/http';
import { Dialog } from '@angular/cdk/dialog';
import { ChangeDetectionStrategy, Component, computed, DestroyRef, effect, inject, OnDestroy, OnInit, signal, type WritableSignal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import type { GameEndedDto } from '../../../core/api/models/room.model';
import { RoomsApiService } from '../../../core/api/rooms-api.service';
import { AuthService } from '../../../core/auth/auth.service';
import { GameHubService } from '../../../core/realtime/game-hub.service';
import { Board } from './board/board';
import { ChatPanel, type SendChatPayload } from './chat/chat-panel';
import { GameEndedDialog, type GameEndedDialogData, type GameEndedDialogResult } from './dialogs/game-ended-dialog';
import { hubErrorToKey, type HubErrorKey } from './hub-error.mapper';
import { RoomSidebar } from './sidebar/sidebar';

const URGE_COOLDOWN_MS = 30_000;
const URGE_TOAST_MS = 4_000;
const ERROR_TOAST_MS = 3_000;
const TICK_MS = 1_000;

@Component({
  selector: 'app-room-page',
  standalone: true,
  imports: [Board, ChatPanel, RoomSidebar, RouterLink, TranslocoPipe],
  templateUrl: './room-page.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RoomPage implements OnInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly rooms = inject(RoomsApiService);
  private readonly auth = inject(AuthService);
  private readonly hub = inject(GameHubService);
  private readonly dialog = inject(Dialog);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly state = this.hub.state;
  protected readonly connectionStatus = this.hub.connectionStatus;
  protected readonly loading = signal(true);
  protected readonly notFound = signal(false);
  protected readonly loadError = signal(false);
  protected readonly submittingMove = signal(false);
  protected readonly urgeToast = signal(false);
  protected readonly errorToastKey = signal<HubErrorKey | null>(null);
  protected readonly chatBannerKey = signal<string | null>(null);
  private readonly now = signal<number>(Date.now());
  private readonly urgeCooldownUntil = signal<number>(0);
  private gameEndedDialogOpen = false;
  private tickHandle: ReturnType<typeof setInterval> | null = null;
  private readonly timeouts = new Map<string, ReturnType<typeof setTimeout>>();
  private roomId: string | null = null;
  private lastStatus: ReturnType<GameHubService['connectionStatus']> = 'disconnected';

  protected readonly mySide = computed<'black' | 'white' | 'spectator'>(() => {
    const s = this.state();
    const myId = this.auth.user()?.id;
    if (!s || !myId) return 'spectator';
    if (s.black?.id === myId) return 'black';
    if (s.white?.id === myId) return 'white';
    return 'spectator';
  });
  protected readonly myTurn = computed<boolean>(() => {
    const side = this.mySide();
    const turn = this.state()?.game?.currentTurn;
    return (side === 'black' && turn === 'Black') || (side === 'white' && turn === 'White');
  });
  protected readonly turnRemainingMs = computed<number>(() => {
    const g = this.state()?.game;
    if (!g) return 0;
    const started = Date.parse(g.turnStartedAt);
    return Number.isNaN(started) ? 0 : Math.max(0, started + g.turnTimeoutSeconds * 1_000 - this.now());
  });
  protected readonly canUrge = computed<boolean>(() => {
    const s = this.state();
    if (!s || s.status !== 'Playing' || this.mySide() === 'spectator' || this.myTurn()) return false;
    if (this.connectionStatus() !== 'connected') return false;
    return this.now() >= this.urgeCooldownUntil();
  });

  constructor() {
    effect(() => {
      const ended = this.hub.gameEnded();
      if (ended && !this.gameEndedDialogOpen) this.openGameEndedDialog(ended);
    });
    effect(() => {
      const status = this.connectionStatus();
      if (this.lastStatus === 'reconnecting' && status === 'connected') void this.rehydrate();
      this.lastStatus = status;
    });
  }

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.notFound.set(true);
      this.loading.set(false);
      return;
    }
    this.roomId = id;
    if (!this.auth.isAuthenticated()) {
      void this.router.navigateByUrl('/login');
      return;
    }
    this.hub.urged$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      this.urgeToast.set(true);
      this.schedule('urge-toast', URGE_TOAST_MS, () => this.urgeToast.set(false));
    });
    this.hub.roomDissolved$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => void this.router.navigateByUrl('/home'));
    this.tickHandle = setInterval(() => this.now.set(Date.now()), TICK_MS);
    void this.initialLoad(id);
  }

  ngOnDestroy(): void {
    if (this.tickHandle !== null) clearInterval(this.tickHandle);
    for (const h of this.timeouts.values()) clearTimeout(h);
    this.timeouts.clear();
    if (this.roomId) void this.hub.leaveRoom(this.roomId);
  }

  private async initialLoad(id: string): Promise<void> {
    this.loading.set(true);
    this.notFound.set(false);
    this.loadError.set(false);
    try {
      this.hub.applySnapshot(await firstValueFrom(this.rooms.getById(id)));
      await this.hub.joinRoom(id);
      if (this.mySide() === 'spectator') await this.hub.joinSpectatorGroup(id);
    } catch (err) {
      if (err instanceof HttpErrorResponse && err.status === 404) this.notFound.set(true);
      else this.loadError.set(true);
    } finally {
      this.loading.set(false);
    }
  }

  private async rehydrate(): Promise<void> {
    const id = this.roomId;
    if (!id) return;
    try {
      await this.hub.joinRoom(id);
      if (this.mySide() === 'spectator') await this.hub.joinSpectatorGroup(id);
      this.hub.applySnapshot(await firstValueFrom(this.rooms.getById(id)));
    } catch (err) {
      if (err instanceof HttpErrorResponse && err.status === 404) {
        void this.router.navigateByUrl('/home');
      }
    }
  }

  protected retryLoad(): void {
    if (this.roomId) void this.initialLoad(this.roomId);
  }

  protected retryConnection(): void {
    void this.hub.reconnect().catch(() => undefined);
  }

  protected handleCellClick(payload: { row: number; col: number }): void {
    const id = this.roomId;
    if (!id || this.submittingMove()) return;
    this.submittingMove.set(true);
    this.hub
      .makeMove(id, payload.row, payload.col)
      .catch((err: unknown) => {
        const key = hubErrorToKey(err);
        this.flashError(key);
        if (key === 'game.errors.concurrent-move-refetched') {
          this.rooms.getById(id).subscribe({
            next: (s) => this.hub.applySnapshot(s),
            error: () => undefined,
          });
        }
      })
      .finally(() => this.submittingMove.set(false));
  }

  protected handleChatSend(payload: SendChatPayload): void {
    const id = this.roomId;
    if (!id) return;
    this.hub.sendChat(id, payload.content, payload.channel).catch((err: unknown) => {
      const message =
        typeof err === 'object' && err && 'message' in err
          ? String((err as { message?: unknown }).message ?? '')
          : '';
      if (/forbid|spectator/i.test(message)) {
        this.flash(this.chatBannerKey, 'game.chat.forbidden-error');
      } else {
        this.flashError(hubErrorToKey(err));
      }
    });
  }

  protected handleResign(): void {
    if (!this.roomId) return;
    this.rooms.resign(this.roomId).subscribe({
      error: () => this.flashError('game.errors.generic'),
    });
  }

  protected handleLeave(): void {
    if (!this.roomId) return;
    this.rooms.leave(this.roomId).subscribe({
      next: () => void this.router.navigateByUrl('/home'),
      error: () => this.flashError('game.errors.generic'),
    });
  }

  protected handleUrge(): void {
    const id = this.roomId;
    if (!id || !this.canUrge()) return;
    const prev = this.urgeCooldownUntil();
    this.urgeCooldownUntil.set(Date.now() + URGE_COOLDOWN_MS);
    this.hub.urge(id).catch((err: unknown) => {
      const key = hubErrorToKey(err);
      if (key !== 'game.errors.urge-cooldown') this.urgeCooldownUntil.set(prev);
      this.flashError(key);
    });
  }

  private openGameEndedDialog(ended: GameEndedDto): void {
    this.gameEndedDialogOpen = true;
    const data: GameEndedDialogData = {
      result: ended.result,
      winnerUserId: ended.winnerUserId,
      endReason: ended.endReason,
      mySide: this.mySide(),
    };
    const ref = this.dialog.open<GameEndedDialogResult>(GameEndedDialog, { data });
    ref.closed.subscribe((outcome) => {
      this.gameEndedDialogOpen = false;
      if (outcome === 'home') void this.router.navigateByUrl('/home');
    });
  }

  private flashError(key: HubErrorKey): void {
    this.flash(this.errorToastKey, key);
  }

  private flash<T>(sink: WritableSignal<T | null>, value: T, ttl = ERROR_TOAST_MS): void {
    sink.set(value);
    this.schedule(`flash-${String(value)}`, ttl, () => sink.set(null));
  }

  private schedule(key: string, ms: number, cb: () => void): void {
    const prev = this.timeouts.get(key);
    if (prev !== undefined) clearTimeout(prev);
    this.timeouts.set(key, setTimeout(cb, ms));
  }
}
