/**
 * Wire shape of GET /api/presence/online-count.
 * Service unwraps to a plain number before handing to callers.
 */
export interface OnlineCountWire {
  readonly count: number;
}
