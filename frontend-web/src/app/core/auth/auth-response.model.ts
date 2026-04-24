import type { UserDto } from './user.model';

/**
 * Wire shape of POST /api/auth/{register,login,refresh} 200/201 responses.
 *
 * `accessTokenExpiresAt` is an ISO-8601 UTC string on the wire; convert with
 * `parseAuthResponse` before storing in AuthService's signals.
 */
export interface AuthResponseWire {
  readonly accessToken: string;
  readonly refreshToken: string;
  readonly accessTokenExpiresAt: string;
  readonly user: UserDto;
}

/** AuthResponse with the expiry parsed into a Date, ready for state. */
export interface AuthResponse {
  readonly accessToken: string;
  readonly refreshToken: string;
  readonly accessTokenExpiresAt: Date;
  readonly user: UserDto;
}

export function parseAuthResponse(wire: AuthResponseWire): AuthResponse {
  return {
    accessToken: wire.accessToken,
    refreshToken: wire.refreshToken,
    accessTokenExpiresAt: new Date(wire.accessTokenExpiresAt),
    user: wire.user,
  };
}
