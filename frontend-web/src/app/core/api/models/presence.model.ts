/**
 * Wire shape of GET /api/presence/online-count.
 * Service unwraps to a plain number before handing to callers.
 */
export interface OnlineCountWire {
  readonly count: number;
}

/**
 * Wire shape of GET /api/presence/users/{id}.
 * Backend returns IsOnline=false for unknown users (no 404). Service
 * unwraps to a plain boolean before handing to callers.
 */
export interface UserPresenceWire {
  readonly userId: string;
  readonly isOnline: boolean;
}
