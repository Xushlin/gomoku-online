# add-user-authentication

User registration, login, JWT access + rotating refresh tokens, logout, and current-user query. First change to activate all four Clean-Architecture layers: Domain / Application / Infrastructure / Api. Uses Microsoft.AspNetCore.Identity.PasswordHasher for password hashing (without adopting the full Identity system), SQLite via EF Core, FluentValidation via a MediatR pipeline behavior, and JWT bearer auth.
