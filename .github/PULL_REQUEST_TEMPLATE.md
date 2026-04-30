<!--
Thanks for contributing! A few minutes filling this in saves the reviewer
twenty trying to reverse-engineer the change. Delete sections that don't
apply.
-->

## Summary

<!-- 1–3 sentences: what changes, why now. Link the OpenSpec change if any. -->

OpenSpec change: `<change-name>` (or N/A — pure docs / build-infra fix)

## What

<!-- A bullet list of what's actually different. Keep at the "behavior" level,
     not the diff level (the diff is already visible). -->

- ...
- ...

## Why

<!-- The motivation. What problem does this solve? What did the previous
     behavior get wrong, or what new use case unlocks? -->

## Test plan

- [ ] Backend `dotnet test` green
- [ ] Web `npm run lint` + `npm run test:ci` + `npm run build` green
- [ ] Manually verified the affected user flow (describe briefly)

## Author self-check (per CLAUDE.md)

- [ ] Layer dependency direction respected — Domain has no outward deps; Application only depends on Domain; DB access only in Infrastructure
- [ ] No `async void` / `.Result` / `.Wait()` in Domain or Application
- [ ] SignalR Hub stays a router (business logic in handlers, not in the hub)
- [ ] Public methods have at least an XML `<summary>` doc comment; interfaces use `I` prefix
- [ ] Unit tests cover: win detection / ELO / new handlers / web services with logic
- [ ] No secrets / connection strings / `appsettings.*.json` sensitive values committed
- [ ] OpenSpec `tasks.md` checked off; PR description reflects latest progress
- [ ] i18n keys parity (en + zh-CN) holds — zero drift after change

## Notes for the reviewer

<!-- Anything that's NOT obvious from the diff but the reviewer should
     understand: tradeoffs, deferred work, "I considered X but rejected
     because Y", places you want extra eyes. -->
