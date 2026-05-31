---
type: "query"
date: "2026-05-31T18:11:58.480012+00:00"
question: "Why does the shopping feature use a separate database and what does that imply?"
contributor: "graphify"
source_nodes: ["IShoppingDbConnectionFactory", "ShoppingDbConnectionFactory", "ProductCatalogRepository", "ShoppingRepository", "MockProductRepository", "ScrapedProduct", "MockProduct"]
---

# Q: Why does the shopping feature use a separate database and what does that imply?

## Answer

Two distinct subsystems: ShoppingRepository (main DB, user-owned ShoppingList/ShoppingListItem, per user_id) and ProductCatalogRepository (shopping DB, read-only scraped product catalog from Carrefour and Coto). IShoppingDbConnectionFactory is a zero-member marker interface extending IDbConnectionFactory — its only purpose is DI discrimination. MockProductRepository exists as a drop-in replacement for ProductCatalogRepository when the shopping DB is unavailable, revealing the catalog was added after the feature existed on mock data. The split means shopping spend can never appear in sp_balance_get_monthly: actual spending enters the balance via OtherExpenses or CreditCardPurchases, entered manually by the user. Expanded from original query via vocab: shopping connection database separate catalog product.

## Source Nodes

- IShoppingDbConnectionFactory
- ShoppingDbConnectionFactory
- ProductCatalogRepository
- ShoppingRepository
- MockProductRepository
- ScrapedProduct
- MockProduct