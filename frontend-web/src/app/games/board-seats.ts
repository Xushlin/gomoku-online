import type { Stone } from '../core/api/models/room.model';

/** The seat that moves first. */
export const FIRST_SEAT = 0;

/** The seat that moves second. */
export const SECOND_SEAT = 1;

/**
 * Seat number → stone colour. **A display reading, not a wire format.**
 *
 * The wire says seats (`MoveDto.seat`, `GameSnapshot.currentSeat`); a board paints
 * colours. Gomoku reads seat 0 as 黑, 象棋 reads it as 红 — same number, two
 * readings, and both live in the display layer where they are true.
 *
 * This is the client-side twin of the backend's `BoardSeats`, and deliberately
 * **not** of `SeatWire`: that one translated seats into colours *in the contract*,
 * which is how seat 2 came to be reported as seat 1. `SeatWire` was temporary and
 * is deleted; this is permanent, because a board really does need a colour per seat.
 *
 * Only the board family may call it. A game with more than two seats has no colours
 * to map, which is why nothing outside `games/` and the board components uses it.
 */
export function seatStone(seat: number): Exclude<Stone, 'Empty'> {
  return seat === FIRST_SEAT ? 'Black' : 'White';
}

/** The seat a viewer's declared side occupies; `null` for a spectator. */
export function seatOfSide(side: 'black' | 'white' | 'spectator'): number | null {
  if (side === 'black') return FIRST_SEAT;
  if (side === 'white') return SECOND_SEAT;
  return null;
}
