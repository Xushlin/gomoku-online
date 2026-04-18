# How to run the backend locally

## Prerequisites

- .NET 10 SDK (`net10.0`)
- Optional: `dotnet-ef` global tool for manual migrations
  ```bash
  dotnet tool install --global dotnet-ef
  ```

## First run

```bash
cd backend
dotnet restore Gomoku.slnx
dotnet run --project src/Gomoku.Api --launch-profile http
```

On first launch the app automatically runs the `InitialIdentity` EF Core migration against `gomoku.db` (SQLite) in the `src/Gomoku.Api/` working directory. Listens on `http://localhost:5145`.

`appsettings.Development.json` already contains a local-only base64 JWT signing key — **never use it in production**. For production, set the environment variable `GOMOKU_JWT__SIGNINGKEY` (`__` is ASP.NET Core's config section separator); the app refuses to start in production with an empty key.

## Running tests

```bash
cd backend
dotnet test Gomoku.slnx
```

Expect ~190 tests across `Gomoku.Domain.Tests` and `Gomoku.Application.Tests`.

## End-to-end smoke walk

Below: register → /me → refresh → /me(new token) → reuse old refresh(should fail) → logout.

```bash
# Register — 201 with tokens + UserDto (no passwordHash, no tokens list)
REG=$(curl -s -X POST http://localhost:5145/api/auth/register \
  -H 'Content-Type: application/json' \
  -d '{"email":"alice@example.com","username":"Alice","password":"Password1"}')
ACCESS=$(echo "$REG" | grep -oP '"accessToken":"[^"]+' | cut -d'"' -f4)
REFRESH=$(echo "$REG" | grep -oP '"refreshToken":"[^"]+' | cut -d'"' -f4)

# /me — 200 with rating=1200 and all counters 0
curl -s -H "Authorization: Bearer $ACCESS" http://localhost:5145/api/users/me

# Refresh — 200 with a new pair; old refresh token is revoked server-side
curl -s -X POST http://localhost:5145/api/auth/refresh \
  -H 'Content-Type: application/json' \
  -d "{\"refreshToken\":\"$REFRESH\"}"

# Reuse the OLD refresh token — 401 (rotation guards against replay)
curl -s -w '\nHTTP:%{http_code}\n' -X POST http://localhost:5145/api/auth/refresh \
  -H 'Content-Type: application/json' \
  -d "{\"refreshToken\":\"$REFRESH\"}"

# Logout — 204 (idempotent: repeat also returns 204)
curl -s -w 'HTTP:%{http_code}\n' -X POST http://localhost:5145/api/auth/logout \
  -H 'Content-Type: application/json' \
  -d "{\"refreshToken\":\"$NEW_REFRESH\"}"
```

Error-path spot checks (HTTP codes, not bodies):

| Scenario | Expected |
| --- | --- |
| Duplicate email on register | 409 |
| Duplicate username on register (case-insensitive) | 409 |
| Weak password on register (< 8 chars / no letter / no digit) | 400 with `errors["Password"]` |
| Login with wrong password OR non-existent email | 401, identical message `"Email or password is incorrect."` |
| `GET /api/users/me` without `Authorization` header | 401 |

## Manual EF migrations

```bash
# Add a new migration (name in PascalCase)
dotnet ef migrations add <Name> \
  --project src/Gomoku.Infrastructure \
  --startup-project src/Gomoku.Api \
  --output-dir Persistence/Migrations

# Apply pending migrations
dotnet ef database update \
  --project src/Gomoku.Infrastructure \
  --startup-project src/Gomoku.Api
```

**Rule**: never edit a migration once merged to `main` — add a new one instead.

## Known advisory

`Gomoku.Infrastructure.csproj` has `<NoWarn>NU1903</NoWarn>` to suppress the `System.Security.Cryptography.Xml` advisory (`GHSA-w3x6-4m5h-cxqf`). We do **not** use XML signatures; our JWTs are HS256 via `Microsoft.IdentityModel.JsonWebTokens`. Remove the suppression once a patched `.NET 10` SDK ships.
