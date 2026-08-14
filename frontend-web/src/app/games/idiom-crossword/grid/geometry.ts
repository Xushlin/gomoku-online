/** Smallest cell that still reads as a character on a phone. */
export const MIN_CELL_PX = 30;
/** Largest cell — beyond this the board looks sparse rather than generous. */
export const MAX_CELL_PX = 54;

/** Gap shrinks on wide boards so more columns fit before the cells have to. */
export function gapFor(cols: number): number {
  return cols >= 8 ? 4 : 6;
}

/**
 * Cell size for a board of `cols` columns in `availableWidth` pixels.
 *
 * Pure so the geometry can be tested without a DOM — the component wires it to
 * a `ResizeObserver`-backed signal, but the arithmetic is the part that can be
 * wrong.
 *
 * Clamped at both ends: below {@link MIN_CELL_PX} the characters stop being
 * legible (the board scrolls inside its own container instead), and above
 * {@link MAX_CELL_PX} a small level looks stretched.
 */
export function cellSizeFor(availableWidth: number, cols: number, gap: number): number {
  if (availableWidth <= 0 || cols <= 0) return MAX_CELL_PX;
  const raw = Math.floor((availableWidth - gap * (cols - 1)) / cols);
  return Math.max(MIN_CELL_PX, Math.min(MAX_CELL_PX, raw));
}
