import { Dialog } from '@angular/cdk/dialog';
import { ChangeDetectionStrategy, Component, computed, inject, input, output } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import type { RoomState } from '../../../../core/api/models/room.model';
import {
  ResignConfirmDialog,
  type ResignConfirmResult,
} from '../dialogs/resign-confirm-dialog';

@Component({
  selector: 'app-room-sidebar',
  standalone: true,
  imports: [TranslocoPipe],
  templateUrl: './sidebar.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RoomSidebar {
  private readonly dialog = inject(Dialog);

  readonly state = input<RoomState | null>(null);
  readonly mySide = input<'black' | 'white' | 'spectator'>('spectator');
  readonly turnRemainingMs = input<number>(0);
  readonly canUrge = input<boolean>(false);

  readonly resign = output<void>();
  readonly leave = output<void>();
  readonly urge = output<void>();

  protected readonly isPlayer = computed(() => this.mySide() !== 'spectator');

  protected readonly myTurn = computed(() => {
    const side = this.mySide();
    const turn = this.state()?.game?.currentTurn;
    return (side === 'black' && turn === 'Black') || (side === 'white' && turn === 'White');
  });

  protected readonly countdownText = computed(() => {
    const ms = this.turnRemainingMs();
    const total = Math.max(0, Math.ceil(ms / 1000));
    const mm = Math.floor(total / 60);
    const ss = total % 60;
    return `${mm}:${ss.toString().padStart(2, '0')}`;
  });

  protected readonly countdownDanger = computed(() => this.turnRemainingMs() <= 10_000);

  protected openResignConfirm(): void {
    const ref = this.dialog.open<ResignConfirmResult>(ResignConfirmDialog, {
      ariaLabel: 'Resign confirmation',
    });
    ref.closed.subscribe((confirmed) => {
      if (confirmed === true) this.resign.emit();
    });
  }

  protected emitLeave(): void {
    this.leave.emit();
  }

  protected emitUrge(): void {
    this.urge.emit();
  }
}
