## Why

`hubErrorToKey` maps a hub failure to a translated message by **substring-matching the server's English prose**. CLAUDE.md has carried it as a deferred item for three changes, described as fragile. It is worse than fragile.

**It does not work outside Development, and this was verified, not deduced.**

A hub method that throws a plain domain exception only has its message delivered to the client when `EnableDetailedErrors` is on — and `Program.cs` sets that to `builder.Environment.IsDevelopment()`. In Production SignalR replaces the message with a generic one, so every keyword in that table misses and every failure lands on `game.errors.generic`.

Measured, same illegal 象棋 move (帥 stepping diagonally), same build, same database:

| Environment | What the player sees |
| --- | --- |
| Development | *That move isn't allowed.* |
| **Production** | ***Something went wrong. Please try again.*** |

So the message-quality fix `add-web-xiangqi` shipped — the one motivated by "in 象棋 a refused move is the ordinary way a player learns the rules" — is **switched off in the only environment that will ever have players**. Nobody noticed because nothing is deployed yet; this repo's local-only status is the only reason it has not bitten.

That reframes the item. It is not tidying a fuzzy match. It is a feature that is off in production.

## What Changes

### Domain errors get a stable code

A `DomainException` base carrying `Code` (kebab-case, e.g. `not-your-turn`, `invalid-move`, `self-check`). Every exception the API deliberately maps derives from it. The code is the identity of the error; the message stays human prose for logs.

Why in the Domain rather than a lookup table in the Api layer: there would then be **three** places that enumerate these exceptions — the HTTP status mapper, the new hub mapper, and the client. A table is a list someone must remember to extend. A constructor parameter is one the compiler demands.

### The hub translates them into `HubException`

`HubException` messages reach the client **in both environments** — that is precisely what the type is for, and it is why the fix is not "turn detailed errors on in production" (which would also ship stack traces and internal messages to every client).

They do **not** arrive verbatim, though, and this cost a round trip to discover. SignalR wraps them; measured on the wire, byte-identical with `EnableDetailedErrors` on and off:

```
"An unexpected error occurred invoking 'MovePiece' on the server. HubException: invalid-move"
```

So the client extracts the code rather than comparing the whole string. An earlier draft of this change compared it and still showed the generic message even though the server was already sending codes — the fix looked done and was not. Both wire forms are now in the mapper's tests.

A hub filter catches `DomainException` and rethrows `HubException(code)`. The payload is the **code alone**: the player is shown a translated string, so server English is never displayed, and shipping it would only tempt someone to display it. The original exception is logged server-side with its message intact.

### The client maps codes, not prose

`hubErrorToKey` becomes an exhaustive `Record<code, HubErrorKey>` plus the existing client-side network check. An unmapped code falls back to generic — but a *new* server error now arrives as a distinct code rather than as prose that happens to miss every keyword, so the fallback stops being the common case.

### Messages for the errors a player can actually reach

Several hub-reachable errors have no message of their own today and fell through to generic even in Development: room-not-in-play, not-a-player, spectator-channel-forbidden, invalid-chat. They get keys.

## Non-goals

- Not turning on `EnableDetailedErrors` in production. That would leak internals and would still leave the client matching prose.
- No change to the HTTP error contract. `ExceptionHandlingMiddleware` keeps its status mapping; it gains nothing and loses nothing.
- Not renaming `GomokuHub` / `/hubs/gomoku`. That rides with lobby generalization.

## Impact

- `room-and-gameplay` / `web-game-board`: the hub error contract becomes a code rather than prose.
- Frontend: `hubErrorToKey` and its i18n keys.
- **This is the enabling cleanup before 成语接龙.** A fourth game means a fourth set of exception phrasings; doing it after would mean writing them into a table that does not work in production.
