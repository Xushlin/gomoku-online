import { TestBed } from '@angular/core/testing';
import { TranslocoTestingModule } from '@jsverse/transloco';
import { describe, expect, it } from 'vitest';
import { initialPositions, type KlotskiLayout } from '../model';
import { KlotskiBoard } from './klotski-board';

/**
 * 四类角色**齐全**的盘面 —— 2x2 主帅、1x2 竖将、2x1 横将、1x1 兵。
 *
 * 齐全是这组测试的前提,不是巧合:一个只有两类的盘面会让「四类两两不同」恒真,
 * 而恒真的断言和有效的断言打印一样的东西。下面第一条就是钉这个前提的。
 */
const ALL_ROLES: KlotskiLayout = {
  rows: 5,
  cols: 4,
  exit: { row: 3, col: 1 },
  pieces: [
    { id: 'cao', name: '曹操', row: 0, col: 1, height: 2, width: 2, target: true },
    { id: 'zhang', name: '张飞', row: 0, col: 0, height: 2, width: 1 },
    { id: 'guan', name: '关羽', row: 2, col: 1, height: 1, width: 2 },
    { id: 'bing', name: '兵', row: 4, col: 0, height: 1, width: 1 },
  ],
};

const ROLE_CLASSES = ['kt-piece--boss', 'kt-piece--general', 'kt-piece--guard', 'kt-piece--soldier'];

function mount(layout: KlotskiLayout = ALL_ROLES, selected: string | null = null) {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    imports: [
      KlotskiBoard,
      TranslocoTestingModule.forRoot({
        langs: { en: {} },
        translocoConfig: { availableLangs: ['en'], defaultLang: 'en' },
        preloadLangs: true,
      }),
    ],
  });
  const fixture = TestBed.createComponent(KlotskiBoard);
  fixture.componentRef.setInput('layout', layout);
  fixture.componentRef.setInput('positions', initialPositions(layout));
  fixture.componentRef.setInput('selected', selected);
  fixture.detectChanges();
  return fixture;
}

const pieces = (fixture: ReturnType<typeof mount>): HTMLButtonElement[] =>
  Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('.kt-piece'));

const rolesOn = (el: Element): string[] => ROLE_CLASSES.filter((c) => el.classList.contains(c));

describe('KlotskiBoard roles', () => {
  it('the sample really does carry all four roles — the precondition of everything below', () => {
    const present = new Set(pieces(mount()).flatMap(rolesOn));
    expect([...present].sort()).toEqual([...ROLE_CLASSES].sort());
  });

  it('gives every piece exactly one role', () => {
    const counts = pieces(mount()).map((el) => rolesOn(el).length);
    expect(counts.length).toBeGreaterThan(0);
    expect(counts.filter((n) => n !== 1)).toEqual([]);
  });

  it('assigns the role by shape, not by name', () => {
    const renamed: KlotskiLayout = {
      ...ALL_ROLES,
      pieces: ALL_ROLES.pieces.map((p) => ({ ...p, name: 'X' })),
    };
    // 名字全改成同一个字之后,四类仍然齐全 —— 按名字分类的实现在这里只会剩一类。
    const roles = pieces(mount(renamed)).map((el) => rolesOn(el)[0]);
    expect(roles.length).toBe(ALL_ROLES.pieces.length);
    expect([...new Set(roles)].sort()).toEqual([...ROLE_CLASSES].sort());
  });
});

describe('KlotskiBoard placement', () => {
  it('positions pieces by grid coordinate, never by grid-area', () => {
    const all = pieces(mount());
    expect(all.length).toBeGreaterThan(0);
    for (const el of all) {
      expect(el.style.getPropertyValue('--kt-r')).not.toBe('');
      expect(el.style.getPropertyValue('--kt-c')).not.toBe('');
      /*
       * `grid-area` 是上一版的定位方式,而它**不可动画** —— 棋子因此瞬移。
       * 这条断言在它回来的那天会红。
       */
      expect(el.style.gridArea).toBe('');
    }
  });

  it('moves a piece by changing its coordinate', () => {
    const fixture = mount();
    const before = pieces(fixture).map((el) => el.style.getPropertyValue('--kt-r'));
    fixture.componentRef.setInput('positions', {
      ...initialPositions(ALL_ROLES),
      bing: { row: 3, col: 0 },
    });
    fixture.detectChanges();
    const after = pieces(fixture).map((el) => el.style.getPropertyValue('--kt-r'));
    expect(after).not.toEqual(before);
    // 而**只有**那一个动了 —— 否则一个把所有坐标都改掉的实现同样通过。
    expect(after.filter((v, i) => v !== before[i]).length).toBe(1);
  });

  it('sizes the destination marker to the selected piece, not to one cell', () => {
    const fixture = mount(ALL_ROLES, 'cao');
    const markers = Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll<HTMLElement>('.kt-target'),
    );
    expect(markers.length).toBeGreaterThan(0);
    for (const m of markers) {
      expect(m.style.getPropertyValue('--kt-w')).toBe('2');
      expect(m.style.getPropertyValue('--kt-h')).toBe('2');
    }
  });
});
