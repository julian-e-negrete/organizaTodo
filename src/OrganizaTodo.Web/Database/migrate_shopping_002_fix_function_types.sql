-- ============================================================
-- SHOPPING DB MIGRATION 002 — Fix function return types
-- Run this against POSTGRES_DB_Shopping, NOT the main organizaTodo DB
--
-- Root cause of 42804: original functions declared VARCHAR/TIMESTAMP but
-- actual column types are TEXT (name, brand, category, promo) and
-- TIMESTAMPTZ (scraped_at). PostgreSQL enforces exact type matching in
-- RETURNS TABLE, so every declared column type must match the query output.
-- ============================================================

DROP FUNCTION IF EXISTS sp_products_search(character varying);
DROP FUNCTION IF EXISTS sp_products_get_categories();
DROP FUNCTION IF EXISTS sp_products_get_by_category(character varying);

-- Categories: SETOF TEXT avoids named-column type issues entirely;
-- Dapper QueryAsync<string> reads the single text column fine.
CREATE OR REPLACE FUNCTION sp_products_get_categories()
RETURNS SETOF TEXT LANGUAGE sql AS $$
    SELECT DISTINCT category FROM carrefour_products
    UNION
    SELECT DISTINCT category FROM coto_products
    ORDER BY 1
$$;

-- Search: TEXT for all text columns, TIMESTAMPTZ for scraped_at (matches real schema).
-- Cast scraped_at to TIMESTAMP (no tz) so Dapper/Npgsql maps cleanly to DateTime.
CREATE OR REPLACE FUNCTION sp_products_search(p_query TEXT)
RETURNS TABLE(
    "Name"      TEXT,
    "Brand"     TEXT,
    "Category"  TEXT,
    "Price"     NUMERIC,
    "ListPrice" NUMERIC,
    "Promo"     TEXT,
    "Available" BOOLEAN,
    "Source"    TEXT,
    "ScrapedAt" TIMESTAMP
) LANGUAGE sql AS $$
    SELECT
        c.name, c.brand, c.category, c.price, c.list_price,
        NULL::TEXT,
        c.available,
        'Carrefour'::TEXT,
        c.scraped_at::TIMESTAMP
    FROM carrefour_products c
    WHERE c.name     ILIKE '%' || p_query || '%'
       OR c.brand    ILIKE '%' || p_query || '%'
       OR c.category ILIKE '%' || p_query || '%'
    UNION ALL
    SELECT
        t.name, t.brand, t.category, t.price, t.list_price,
        t.promo,
        t.available,
        'Coto'::TEXT,
        t.scraped_at::TIMESTAMP
    FROM coto_products t
    WHERE t.name     ILIKE '%' || p_query || '%'
       OR t.brand    ILIKE '%' || p_query || '%'
       OR t.category ILIKE '%' || p_query || '%'
    ORDER BY 1
$$;

-- Browse by category: same type corrections as search.
CREATE OR REPLACE FUNCTION sp_products_get_by_category(p_category TEXT)
RETURNS TABLE(
    "Name"      TEXT,
    "Brand"     TEXT,
    "Category"  TEXT,
    "Price"     NUMERIC,
    "ListPrice" NUMERIC,
    "Promo"     TEXT,
    "Available" BOOLEAN,
    "Source"    TEXT,
    "ScrapedAt" TIMESTAMP
) LANGUAGE sql AS $$
    SELECT
        c.name, c.brand, c.category, c.price, c.list_price,
        NULL::TEXT,
        c.available,
        'Carrefour'::TEXT,
        c.scraped_at::TIMESTAMP
    FROM carrefour_products c
    WHERE c.category = p_category
    UNION ALL
    SELECT
        t.name, t.brand, t.category, t.price, t.list_price,
        t.promo,
        t.available,
        'Coto'::TEXT,
        t.scraped_at::TIMESTAMP
    FROM coto_products t
    WHERE t.category = p_category
    ORDER BY 1
$$;
