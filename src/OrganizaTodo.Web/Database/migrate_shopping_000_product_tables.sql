-- ============================================================
-- SHOPPING DB MIGRATION 000 — Scraped catalog tables (offline/fresh-install safety net)
-- Run this against POSTGRES_DB_Shopping, NOT the main organizaTodo DB
--
-- carrefour_products / coto_products are normally created and populated by an
-- external Python scraper against the shared production Shopping DB, before
-- this repo's migrations ever run there — so `IF NOT EXISTS` below is a no-op
-- in that environment.
--
-- On a brand-new Postgres instance (desktop offline install, CI, local dev)
-- nothing else creates these tables. Without them, migrate_shopping_001's
-- `LANGUAGE sql` functions fail at CREATE FUNCTION time (Postgres resolves
-- table references in SQL-language function bodies eagerly, unlike plpgsql),
-- which aborts schema bootstrap entirely. Creating empty tables here lets
-- bootstrap succeed; the catalog is simply empty until a scraper populates it
-- — the accepted, documented offline-catalog limitation, not a startup crash.
--
-- Column types match the real production schema (see migrate_shopping_002's
-- header comment): name/brand/category/promo -> TEXT, price/list_price ->
-- NUMERIC, available -> BOOLEAN, scraped_at -> TIMESTAMPTZ. Note carrefour_products
-- has no `promo` column in production (see migrate_shopping_00x's NULL::TEXT
-- placeholder for it); coto_products does.
-- ============================================================

CREATE TABLE IF NOT EXISTS carrefour_products (
    product_id  TEXT PRIMARY KEY,
    sku_id      TEXT,
    name        TEXT,
    brand       TEXT,
    category    TEXT,
    price       NUMERIC,
    list_price  NUMERIC,
    available   BOOLEAN,
    scraped_at  TIMESTAMPTZ
);

CREATE TABLE IF NOT EXISTS coto_products (
    product_id  TEXT PRIMARY KEY,
    sku_id      TEXT,
    name        TEXT,
    brand       TEXT,
    category    TEXT,
    price       NUMERIC,
    list_price  NUMERIC,
    promo       TEXT,
    available   BOOLEAN,
    scraped_at  TIMESTAMPTZ
);
