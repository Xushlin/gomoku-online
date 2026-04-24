/**
 * Shape of the signed-in user as returned by the backend's AuthResponse.user.
 * Only fields the web client actually consumes are listed — backend may send
 * more, and we'd keep extras via index signatures on a later change if needed.
 */
export interface UserDto {
  readonly id: string;
  readonly username: string;
  readonly email: string;
}
