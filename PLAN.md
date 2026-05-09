# Development Plan – OrganizaTodo: Sistema de Finanzas Personales Multi-Usuario

## Scope

ASP.NET Core 8 MVC · SQL Server · Dapper · Cookie Auth · Bootstrap 5 · Docker

---

## Phase 1 – Foundation (done)

- [x] Solution & MVC project scaffolding
- [x] Dapper + `Microsoft.Data.SqlClient` packages
- [x] `IDbConnectionFactory` / `SqlConnectionFactory`
- [x] Dockerfile + docker-compose (app + SQL Server 2022)
- [x] BCrypt.Net-Next for password hashing
- [x] Database schema: all tables and stored procedures (`Database/init.sql`)
- [x] Mock supermarket product seed data (30 items, Coto & Carrefour)

## Phase 2 – Authentication & User Management (done)

- [x] Cookie authentication (8 h session, no sliding – MU-15)
- [x] Register (MU-01, MU-06) / Login (MU-02) / Logout (MU-04)
- [x] BCrypt password storage (MU-16)
- [x] User profile: name, email, currency (MU-05)
- [x] Roles: USER / ADMIN (MU-11–MU-14)
- [x] Data isolation enforced by `user_id` filter in every SP (MU-07–MU-10)

## Phase 3 – Core Financial Modules (done)

- [x] **Servicios de vivienda** – CRUD, mark paid, monthly total (RF-01–RF-04)
- [x] **Tarjeta de crédito** – installment + current purchases, monthly total (RF-05–RF-10)
- [x] **Pasivos fijos** – CRUD, monthly total (RF-11–RF-13)
- [x] **Ingresos** – CRUD by month/year (RF-14)
- [x] **Otros egresos** – CRUD by month/year (RF-15)

## Phase 4 – Balance & Dashboard (done)

- [x] `sp_Balance_GetMonthly` stored procedure
  ```
  Saldo = Ingresos − (Servicios + Tarjeta + Pasivos Fijos) − Otros Egresos
  ```
- [x] Monthly summary card matching RF-16 format
- [x] Dashboard with breakdown by category (RF-25)

## Phase 5 – Shopping List & Mock Prices (done)

- [x] Mock product catalog (Coto / Carrefour) – 30 seed products (RF-18 spec pending)
- [x] Product search by name or category (RF-19)
- [x] Shopping list per month/user with add / remove items (RF-20, RF-24)
- [x] Estimated total vs. remaining balance, alert if over budget (RF-21, RF-22)
- [x] Auto-priority recommendation (RF-23)

## Phase 6 – Reports & Admin (done)

- [x] Monthly history view (last 12 months – RF-26)
- [x] CSV export of monthly balance (RF-27, RF-28)
- [x] Admin dashboard: active users, total income, average balance (MU-12)
- [x] Admin user list: enable / disable, promote to ADMIN (MU-13, MU-14)
- [x] Last-access log (MU-18)
- [x] Account deletion with cascade (MU-19)

---

## Architecture

```
Controllers → Services → Repositories → Dapper (CommandType.StoredProcedure) → SQL Server
```

- **No inline SQL** anywhere in C# code.
- All DB operations go through named stored procedures.
- Every query is scoped to `@UserId` parameter – prevents cross-user data leakage.
- `IDbConnectionFactory` is singleton; repositories and services are scoped.

## Known Deferred Items

- Password reset via email (MU-03): token generation implemented, email delivery stubbed.
- Real Coto/Carrefour API: replace `MockProductRepository` with HTTP client once endpoint spec is finalised (RF-18).
- PDF export: CSV currently satisfies RF-27; PDF requires additional package.
- Bimonthly service calculation: currently uses monthly amount; needs period-awareness.
