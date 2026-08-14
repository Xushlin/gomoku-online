/**
 * Shared Vitest setup.
 *
 * jsdom implements neither `ResizeObserver` nor `matchMedia`, and components
 * that legitimately use them would otherwise have to be tested through a mock
 * of themselves. Stubbing the browser APIs instead keeps the component under
 * test real — the geometry maths it feeds is unit-tested separately in
 * `games/idiom-crossword/grid/geometry.spec.ts`.
 */

if (!('ResizeObserver' in globalThis)) {
  class ResizeObserverStub implements ResizeObserver {
    observe(): void {
      // No layout in jsdom, so no entries are ever delivered. Consumers must
      // therefore have a sensible pre-measurement default — which is exactly
      // the behaviour worth having in a real browser's first frame too.
    }
    unobserve(): void {
      // Nothing observed, so nothing to stop observing.
    }

    disconnect(): void {
      // Present so `ngOnDestroy` can call it without a guard.
    }
  }

  Object.defineProperty(globalThis, 'ResizeObserver', {
    writable: true,
    configurable: true,
    value: ResizeObserverStub,
  });
}
