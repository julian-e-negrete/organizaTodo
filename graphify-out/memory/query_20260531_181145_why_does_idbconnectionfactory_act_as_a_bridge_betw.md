---
type: "query"
date: "2026-05-31T18:11:45.762240+00:00"
question: "Why does IDbConnectionFactory act as a bridge between 10 different communities?"
contributor: "graphify"
source_nodes: ["IDbConnectionFactory", "IShoppingDbConnectionFactory", "ShoppingDbConnectionFactory", "SqlConnectionFactory", "ShoppingRepository", "ProductCatalogRepository"]
---

# Q: Why does IDbConnectionFactory act as a bridge between 10 different communities?

## Answer

IDbConnectionFactory is a single-method interface (CreateConnection()) injected by all 11 concrete repositories. High betweenness (0.137) is a consequence of the repository pattern: each repo lives in its own community but shares this one infrastructure seam. IShoppingDbConnectionFactory extends it as a marker interface (no additional members) so DI can route ShoppingRepository and ProductCatalogRepository to the separate shopping DB. The risk: if CreateConnection() ever needs to change (e.g. async), all 12 repositories are affected. Expanded from original query via vocab: connection factory dapper repository data access pattern service.

## Source Nodes

- IDbConnectionFactory
- IShoppingDbConnectionFactory
- ShoppingDbConnectionFactory
- SqlConnectionFactory
- ShoppingRepository
- ProductCatalogRepository