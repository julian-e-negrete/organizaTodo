---
type: "query"
date: "2026-05-31T18:11:58.739135+00:00"
question: "How deeply is the vehicle subsystem wired into the rest of the app?"
contributor: "graphify"
source_nodes: ["VehicleRepository", "VehiclesController", "FuelPriceService", "VehicleMaintenanceLog", "sp_vehicles_get_monthly_total", "OtherExpense"]
---

# Q: How deeply is the vehicle subsystem wired into the rest of the app?

## Answer

Vehicles is the most integrated domain: (1) sp_vehicles_get_monthly_total feeds sp_balance_get_monthly via migrate_006; (2) migrate_011 added VehicleId FK to OtherExpenses, rewriting two SPs and adding a conditional vehicle selector to the expense form; (3) DashboardViewModel includes the vehicle list; (4) VehiclesController.GetFuelPrices() is a live AJAX endpoint scraping surtidores.com.ar via FuelPriceService (no caching). Three separate DB tables: Vehicles (migrate_005), VehicleMonthlyPayments (migrate_007, payment toggle per month/year), VehicleMaintenanceLogs (migrate_008, due_month added in migrate_009). Soft delete on vehicles. No other domain has as many outgoing connections. Expanded from original query via vocab: vehicle maintenance fuel payment log expense service.

## Source Nodes

- VehicleRepository
- VehiclesController
- FuelPriceService
- VehicleMaintenanceLog
- sp_vehicles_get_monthly_total
- OtherExpense