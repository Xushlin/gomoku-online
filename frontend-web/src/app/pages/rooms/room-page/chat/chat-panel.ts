import {
  AfterViewInit,
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  ElementRef,
  inject,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import type {
  ChatChannel,
  ChatMessage,
  RoomState,
} from '../../../../core/api/models/room.model';

const MAX_CHAT_LENGTH = 500;

export interface SendChatPayload {
  readonly content: string;
  readonly channel: ChatChannel;
}

@Component({
  selector: 'app-chat-panel',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink, TranslocoPipe],
  templateUrl: './chat-panel.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ChatPanel implements AfterViewInit {
  private readonly fb = inject(FormBuilder);

  readonly state = input<RoomState | null>(null);
  readonly mySide = input<'black' | 'white' | 'spectator'>('spectator');
  readonly canSend = input<boolean>(true);
  readonly forbiddenBannerKey = input<string | null>(null);

  readonly send = output<SendChatPayload>();

  protected readonly isSpectator = computed(() => this.mySide() === 'spectator');
  protected readonly activeChannel = signal<ChatChannel>('Room');

  protected readonly form = this.fb.nonNullable.group({
    content: ['', [Validators.required, Validators.maxLength(MAX_CHAT_LENGTH)]],
  });

  protected readonly visibleMessages = computed<readonly ChatMessage[]>(() => {
    const messages = this.state()?.chatMessages ?? [];
    const channel = this.activeChannel();
    return [...messages]
      .filter((m) => m.channel === channel)
      .sort((a, b) => a.sentAt.localeCompare(b.sentAt));
  });

  protected readonly trimmedLength = computed(() => this.form.controls.content.value.trim().length);

  private readonly listRef = viewChild<ElementRef<HTMLDivElement>>('list');

  constructor() {
    effect(() => {
      this.visibleMessages();
      queueMicrotask(() => this.scrollToBottom());
    });
  }

  ngAfterViewInit(): void {
    this.scrollToBottom();
  }

  protected setTab(channel: ChatChannel): void {
    this.activeChannel.set(channel);
  }

  protected submit(): void {
    if (!this.canSend()) return;
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const raw = this.form.controls.content.value;
    const content = raw.trim();
    if (content.length === 0) return;
    if (content.length > MAX_CHAT_LENGTH) return;
    this.send.emit({ content, channel: this.activeChannel() });
    this.form.reset({ content: '' });
  }

  protected readonly maxLength = MAX_CHAT_LENGTH;

  private scrollToBottom(): void {
    const el = this.listRef()?.nativeElement;
    if (!el) return;
    el.scrollTop = el.scrollHeight;
  }
}
