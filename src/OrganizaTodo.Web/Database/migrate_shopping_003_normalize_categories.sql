-- ============================================================
-- SHOPPING DB MIGRATION 003 — Normalize category casing
-- Run this against POSTGRES_DB_Shopping, NOT the main organizaTodo DB
--
-- Problem: Carrefour uses sentence case ("Aguas saborizadas"),
--          Coto uses title case ("Aguas Saborizadas").
--          Both represent the same category, so browsing by category
--          only returns products from one table.
--
-- Fix: normalize with INITCAP(LOWER(TRIM(category))) for display
--      and LOWER(TRIM(category)) for all equality comparisons.
-- ============================================================

DROP FUNCTION IF EXISTS sp_products_search(text);
DROP FUNCTION IF EXISTS sp_products_get_categories();
DROP FUNCTION IF EXISTS sp_products_get_by_category(text);

-- Categories: deduplicated after normalization to title case
CREATE OR REPLACE FUNCTION sp_products_get_categories()
RETURNS SETOF TEXT LANGUAGE sql AS $$
    SELECT DISTINCT INITCAP(LOWER(TRIM(category))) FROM carrefour_products
    UNION
    SELECT DISTINCT INITCAP(LOWER(TRIM(category))) FROM coto_products
    ORDER BY 1
$$;

-- Search: normalize category column in output; ILIKE already case-insensitive
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
        c.name, c.brand,
        INITCAP(LOWER(TRIM(c.category))),
        c.price, c.list_price,
        NULL::TEXT, c.available, 'Carrefour'::TEXT, c.scraped_at::TIMESTAMP
    FROM carrefour_products c
    WHERE c.name     ILIKE '%' || p_query || '%'
       OR c.brand    ILIKE '%' || p_query || '%'
       OR c.category ILIKE '%' || p_query || '%'
    UNION ALL
    SELECT
        t.name, t.brand,
        INITCAP(LOWER(TRIM(t.category))),
        t.price, t.list_price,
        t.promo, t.available, 'Coto'::TEXT, t.scraped_at::TIMESTAMP
    FROM coto_products t
    WHERE t.name     ILIKE '%' || p_query || '%'
       OR t.brand    ILIKE '%' || p_query || '%'
       OR t.category ILIKE '%' || p_query || '%'
    ORDER BY 1
$$;

-- Browse by category: compare with LOWER(TRIM(...)) so both tables match
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
        c.name, c.brand,
        INITCAP(LOWER(TRIM(c.category))),
        c.price, c.list_price,
        NULL::TEXT, c.available, 'Carrefour'::TEXT, c.scraped_at::TIMESTAMP
    FROM carrefour_products c
    WHERE LOWER(TRIM(c.category)) = LOWER(TRIM(p_category))
    UNION ALL
    SELECT
        t.name, t.brand,
        INITCAP(LOWER(TRIM(t.category))),
        t.price, t.list_price,
        t.promo, t.available, 'Coto'::TEXT, t.scraped_at::TIMESTAMP
    FROM coto_products t
    WHERE LOWER(TRIM(t.category)) = LOWER(TRIM(p_category))
    ORDER BY 1
$$;
