---
type: "query"
date: "2026-05-31T18:11:45.952608+00:00"
question: "How does sp_balance_get_monthly aggregate across all domain tables without going through repositories?"
contributor: "graphify"
source_nodes: ["BalanceService", "sp_balance_get_monthly", "IBalanceService", "DashboardController", "ReportsController"]
---

# Q: How does sp_balance_get_monthly aggregate across all domain tables without going through repositories?

## Answer

BalanceService.GetMonthlyBalanceAsync() calls sp_balance_get_monthly directly, bypassing all per-domain repositories. The SP was rewritten twice: migrate_006 added vehicle monthly payments, migrate_010 added recurring income. It aggregates Income, FixedLiabilities, CreditCardPurchases, OtherExpenses, HousingServices, Vehicles, and RecurringIncomes in one query. This is a deliberate performance trade-off: N repo calls + C# aggregation vs one DB round-trip. The hidden risk: any new domain module must update the SP and its migration to appear in the balance — there is no opt-in mechanism. Expanded from original query via vocab: balance monthly service income expense vehicle housing credit.

## Source Nodes

- BalanceService
- sp_balance_get_monthly
- IBalanceService
- DashboardController
- ReportsController