## Why

`add-idiom-chain` shipped 成语接龙's rules and left them unreachable. The kernel can store a textual move and the rules can judge one, but nothing can *send* one: `GomokuHub` exposes `MakeMove(roomId, row, col)` and `MovePiece(roomId, fromRow, fromCol, row, col)`, and `MakeMoveCommand` carries four integers.

This change opens the transport. It stops short of the UI, because a transport is verifiable on its own — over a real SignalR connection — and because this repo has already paid once for assuming what SignalR does with a signature.

## What Changes

### A third hub method, not a fourth parameter

`SayWord(Guid roomId, string word)`.

A new method rather than optional parameters on `MakeMove`, and the reason is on the record in `MovePiece`'s own doc comment:

> **SignalR 不套用 C# 的可选参数默认值。** 三参调用打到五参方法上,服务端直接回 `InvalidDataException: Invocation provides 3 argument(s) but target expects 5`

That defect was caught by `AiSmoke`, which speaks the real transport and did not know the refactor existed. The same trap is one keystroke away here, so this change repeats the shape that avoided it and verifies over a real connection rather than by reasoning about it.

### The command carries the payload it was given

`MakeMoveCommand` gains `string? Text`, and `Row` / `Col` become `int?` — mirroring `MoveIntent` and `Move`, which took the same shape in `generalize-match-payload`.

**This is a third place that encodes "positional or textual", and that is a deliberate trade rather than an oversight.** The tidier alternative is to have the command carry a `MoveIntent` directly, which would delete the encoding entirely because the value object already enforces the invariant in its constructor. It is rejected here for one concrete reason: `Position`'s constructor rejects negative coordinates, so moving intent construction up into the hub would move negative-coordinate rejection out of `MakeMoveCommandValidator` — turning a documented **400 with a named field** into an exception thrown before the command exists. `web-game-board` and `add-hub-error-codes` both pin that error path. Changing it is a defensible change; doing it silently inside a feature change is not.

So the command stays flat and transport-shaped, the handler picks exactly one `MoveIntent` factory, and the invariant stays enforced where it already is — in the value object, which will refuse anything the handler assembles wrongly.

`MakeMoveCommandValidator` keeps its non-negative rule, applied **only when a coordinate is present**, and gains "a textual move's word is not blank". Its existing reasoning survives unchanged: bounds belong to the rules, because the validator runs before the room is resolved and does not know which game this is.

## Impact

- Affected specs: `room-and-gameplay` (the hub's method table and the command's shape).
- Affected code: `GomokuHub`, `MakeMoveCommand` + validator + handler, `ExecuteBotMoveCommandHandler` (it builds the command).
- **No Domain changes.** The rules and the payload already exist.
- **No web changes.** The client cannot call `SayWord` yet; `add-web-idiom-chain` does the lobby, the word-list UI and the i18n.
- Verified over a real SignalR connection, not by inspection — the argument-count trap this repo already hit is invisible to every unit-test layer.
