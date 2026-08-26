import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { TranslocoTestingModule } from '@jsverse/transloco';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { MoveScrubber } from './move-scrubber';

/**
 * scrubber 的行为断言。
 *
 * 这些断言原来长在 `ReplayPage` 的 spec 里,摸的是页面的私有成员;行为搬到组件之后
 * **断言跟着行为走** —— 留在原地就只能测一个不再存在的实现。
 *
 * 组件不持有当前半手(页面才是真源),所以这里用一个可写信号扮演页面:每收到一次
 * `seek` 就写回去,再 `detectChanges` —— 那正是生产里的回路。
 */
describe('MoveScrubber', () => {
  const langs = { en: {}, 'zh-CN': {} };

  beforeEach(() => TestBed.resetTestingModule());

  function mount(total: number, startAt = 0) {
    TestBed.configureTestingModule({
      imports: [
        MoveScrubber,
        TranslocoTestingModule.forRoot({
          langs,
          translocoConfig: { availableLangs: ['en', 'zh-CN'], defaultLang: 'en' },
          preloadLangs: true,
        }),
      ],
    });
    const fixture = TestBed.createComponent(MoveScrubber);
    const ply = signal(startAt);
    const seeks: number[] = [];
    fixture.componentRef.setInput('totalMoves', total);
    fixture.componentRef.setInput('currentPly', ply());
    fixture.componentInstance.seek.subscribe((n: number) => {
      seeks.push(n);
      ply.set(Math.max(0, Math.min(total, n)));
      fixture.componentRef.setInput('currentPly', ply());
      fixture.detectChanges();
    });
    fixture.detectChanges();
    const api = fixture.componentInstance as unknown as {
      first: () => void;
      last: () => void;
      onPrev: () => void;
      onNext: () => void;
      togglePlay: () => void;
      setSpeed: (s: 0.5 | 1 | 2) => void;
      onSeek: (e: Event) => void;
      playing: () => boolean;
      atStart: () => boolean;
      atEnd: () => boolean;
    };
    return { fixture, ply, seeks, api };
  }

  const button = (fixture: ReturnType<typeof mount>['fixture'], label: string) =>
    (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>(
      `button[aria-label="replay.scrubber.${label}"]`,
    );

  it('next advances one ply and clamps at the end', () => {
    const { ply, api } = mount(3);
    api.onNext();
    expect(ply()).toBe(1);
    api.onNext();
    api.onNext();
    expect(ply()).toBe(3);
    api.onNext();
    expect(ply()).toBe(3);
  });

  it('prev cannot go below 0', () => {
    const { ply, api } = mount(3);
    api.onPrev();
    expect(ply()).toBe(0);
  });

  it('first and last jump to the ends', () => {
    const { ply, api } = mount(5, 2);
    api.last();
    expect(ply()).toBe(5);
    api.first();
    expect(ply()).toBe(0);
  });

  it('disables the backward buttons at the start and the forward ones at the end', () => {
    const { fixture, api } = mount(3);
    expect(button(fixture, 'first')?.disabled).toBe(true);
    expect(button(fixture, 'prev')?.disabled).toBe(true);
    expect(button(fixture, 'next')?.disabled).toBe(false);
    expect(button(fixture, 'last')?.disabled).toBe(false);

    api.last();
    fixture.detectChanges();
    expect(button(fixture, 'first')?.disabled).toBe(false);
    expect(button(fixture, 'next')?.disabled).toBe(true);
    expect(button(fixture, 'last')?.disabled).toBe(true);
  });

  it('togglePlay at the end restarts from 0 and plays', () => {
    const { ply, api } = mount(3);
    api.last();
    expect(api.atEnd()).toBe(true);
    api.togglePlay();
    expect(ply()).toBe(0);
    expect(api.playing()).toBe(true);
  });

  it('auto-play advances on the interval and stops at the end', () => {
    vi.useFakeTimers();
    try {
      const { fixture, ply, api } = mount(2);
      api.togglePlay();
      fixture.detectChanges();
      vi.advanceTimersByTime(700);
      expect(ply()).toBe(1);
      vi.advanceTimersByTime(700);
      expect(ply()).toBe(2);
      expect(api.playing()).toBe(false);
      // 停了之后再走多久都不动 —— 否则「到末尾自动停」只是没被观察到。
      vi.advanceTimersByTime(7000);
      expect(ply()).toBe(2);
    } finally {
      vi.useRealTimers();
    }
  });

  /**
   * 切倍速 MUST NOT 让计时器重建成两个,也 MUST NOT 让当前这一手被跳过。
   * `currentPly` 只在回调里读,所以走一手不会重建计时器 —— 这条钉的就是那件事:
   * 若在 effect 体里读它,下面第二段 350ms 会因为刚重建而不触发。
   */
  it('changing speed keeps exactly one timer running', () => {
    vi.useFakeTimers();
    try {
      const { fixture, ply, api } = mount(6);
      api.togglePlay();
      fixture.detectChanges();
      vi.advanceTimersByTime(700);
      expect(ply()).toBe(1);
      api.setSpeed(2);
      // 改倍速让 effect 重建计时器,而 effect 跑在调度器上 —— 假计时器会抢在它前面。
      // 这里 flush 一次的是**生产里同样发生的那一次调度**,不是给测试开的后门。
      fixture.detectChanges();
      vi.advanceTimersByTime(350);
      expect(ply()).toBe(2);
      vi.advanceTimersByTime(350);
      expect(ply()).toBe(3);
    } finally {
      vi.useRealTimers();
    }
  });

  it('dragging the range input seeks and pauses', () => {
    vi.useFakeTimers();
    try {
      const { fixture, ply, api } = mount(9);
      api.togglePlay();
      fixture.detectChanges();
      expect(api.playing()).toBe(true);
      const range = (fixture.nativeElement as HTMLElement).querySelector<HTMLInputElement>(
        'input[type="range"]',
      );
      expect(range).not.toBeNull();
      range!.value = '7';
      range!.dispatchEvent(new Event('input'));
      expect(ply()).toBe(7);
      expect(api.playing()).toBe(false);
    } finally {
      vi.useRealTimers();
    }
  });

  it('never emits a ply outside the range', () => {
    const { seeks, api } = mount(4, 4);
    api.onNext();
    api.last();
    api.first();
    api.onPrev();
    expect(seeks.every((n) => n >= 0 && n <= 4)).toBe(true);
    // 两端都出现过 —— 否则这条会在一个从不越界的样本上恒真。
    expect(seeks).toContain(0);
    expect(seeks).toContain(4);
  });
});
