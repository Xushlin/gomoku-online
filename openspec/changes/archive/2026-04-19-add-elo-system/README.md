# add-elo-system

ELO rating calculation and leaderboard. Extends User.RecordGameResult to update rating+counters atomically when a game ends; MakeMoveCommand handler consumes GameEnded path to apply ELO to both players in the same transaction. Adds GET /api/leaderboard returning top 100 players by rating. K factor is segmented by games played (40 / 20 / 10). Does not change room gameplay rules or spec.
