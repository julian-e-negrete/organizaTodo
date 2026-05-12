# OrganizaTodo

Personal finance tracker built with ASP.NET Core 8 MVC and PostgreSQL.

## Features

- **Dashboard** — monthly balance summary with income, liabilities, and remaining balance; pending payment alerts
- **Ingresos** — track monthly income by category
- **Servicios de Vivienda** — housing services (electricity, gas, internet, etc.) with paid/pending toggle
- **Tarjeta de Crédito** — credit card installments with interest rate tracking
- **Pasivos Fijos** — fixed monthly liabilities with paid/pending toggle
- **Otros Egresos** — variable monthly expenses
- **Ahorros** — savings tracker with cumulative total, monthly average, and 6/12-month projections; supports pre-existing balance entries that don't skew the average
- **Lista de Compras** — shopping list with price comparison across supermarkets
- **Reportes** — monthly financial reports
- **Admin** — user management panel (admin role only)

## Requirements

- .NET 8 SDK
- PostgreSQL (external instance — connection configured via `.env`)

## Setup

```bash
# 1. Copy and fill in your database credentials
cp .env.example .env

# 2. Run the database schema
# Execute src/OrganizaTodo.Web/Database/init.sql against your PostgreSQL instance

# 3. Run locally
dotnet run --project src/OrganizaTodo.Web
```

`.env` variables:

```
POSTGRES_HOST=
POSTGRES_PORT=5432
POSTGRES_USER=
POSTGRES_PASSWORD=
POSTGRES_DB=
```

## Docker

```bash
cp .env.example .env
# fill in .env
docker compose up --build
```

## Database

All DB operations go through PostgreSQL functions defined in `src/OrganizaTodo.Web/Database/init.sql`. Migrations for schema changes are in `src/OrganizaTodo.Web/Database/migrate_*.sql`.

## Tech stack

- ASP.NET Core 8 MVC
- PostgreSQL + Dapper + Npgsql 10
- Bootstrap 5
