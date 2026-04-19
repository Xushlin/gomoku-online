# add-rooms-and-gameplay

Room lifecycle (create/join/spectate/leave), real-time gameplay (turn-based move placement tying gomoku-domain Board into a persisted Game), in-room chat, separate spectator chat channel, and an urge shortcut. Introduces SignalR for real-time push; Hubs stay thin and dispatch to CQRS handlers per CLAUDE.md. Does not include ELO, AI opponent, or game-record replay — those are separate follow-up changes.
