# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Structure

ASP.NET Core 8 MVC application with PostgreSQL (Dapper + Npgsql). Solution at `OrganizaTodo.sln`, web project at `src/OrganizaTodo.Web/`.

## Commands

```bash
# Build
dotnet build OrganizaTodo.sln

# Run locally (requires .env with POSTGRES_* vars)
dotnet run --project src/OrganizaTodo.Web

# Docker (copy .env.example to .env and fill in DB credentials first)
docker compose up --build
```

## .NET Skills Plugin

The `dotnet-skills` plugin is installed and active. When doing any C#/.NET work, consult `.claude/AGENTS.md` for skill routing — it maps task types (EF Core, Blazor, testing, CI/CD, architecture, etc.) to the correct skill to invoke. Always prefer skill-guided patterns over pre-training defaults.

## Database Access Rules

All database operations must go through PostgreSQL **functions** defined in `src/OrganizaTodo.Web/Database/init.sql`. Never write ad-hoc SQL in application code.

Call conventions with Dapper + Npgsql 10.x (`CommandType.StoredProcedure` is NOT used — it maps to `CALL` which only works for PostgreSQL procedures, not functions):

- **Table-returning functions** → `CommandType.Text` + `SELECT * FROM sp_name(@param)`
- **Scalar functions** → `CommandType.Text` + `SELECT sp_name(@param)` via `ExecuteScalarAsync<T>`
- **Void functions** → `CommandType.Text` + `SELECT sp_name(@param)` via `QueryFirstOrDefaultAsync<object>`
- **Boolean parameters** → always use `DynamicParameters` with explicit `DbType.Boolean` (Npgsql 10.x cannot infer PostgreSQL BOOLEAN from C# `bool` in anonymous objects)
- **Domain models** → must use `{ get; set; }` (not `{ get; init; }`) — Dapper's IL-emitted setters cannot set `init`-only properties

Key meta-skills to run after making code changes:
- `slopwatch` — detects LLM-generated anti-patterns
- `dotnet-agent-gotchas` — catches common AI mistakes in .NET
