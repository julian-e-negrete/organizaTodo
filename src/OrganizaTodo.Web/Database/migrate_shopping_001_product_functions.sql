-- ============================================================
-- SHOPPING DB MIGRATION 001 — Product catalog SP functions
-- Run this against POSTGRES_DB_Shopping, NOT the main organizaTodo DB
-- ============================================================

-- Search: UNION of both tables, ILIKE on name / brand / category
CREATE OR REPLACE FUNCTION sp_products_search(p_query VARCHAR(200))
RETURNS TABLE(
    "Name"      VARCHAR,
    "Brand"     VARCHAR,
    "Category"  VARCHAR,
    "Price"     NUMERIC,
    "ListPrice" NUMERIC,
    "Promo"     VARCHAR,
    "Available" BOOLEAN,
    "Source"    VARCHAR,
    "ScrapedAt" TIMESTAMP
) LANGUAGE plpgsql AS $$
BEGIN
    RETURN QUERY
    SELECT c.name, c.brand, c.category, c.price, c.list_price,
           NULL::VARCHAR AS promo, c.available,
           'Carrefour'::VARCHAR AS source, c.scraped_at
    FROM carrefour_products c
    WHERE c.name     ILIKE '%' || p_query || '%'
       OR c.brand    ILIKE '%' || p_query || '%'
       OR c.category ILIKE '%' || p_query || '%'
    UNION ALL
    SELECT t.name, t.brand, t.category, t.price, t.list_price,
           t.promo, t.available,
           'Coto'::VARCHAR AS source, t.scraped_at
    FROM coto_products t
    WHERE t.name     ILIKE '%' || p_query || '%'
       OR t.brand    ILIKE '%' || p_query || '%'
       OR t.category ILIKE '%' || p_query || '%'
    ORDER BY "Name";
END;
$$;

-- Categories: distinct categories from both tables combined
CREATE OR REPLACE FUNCTION sp_products_get_categories()
RETURNS TABLE("Category" VARCHAR) LANGUAGE plpgsql AS $$
BEGIN
    RETURN QUERY
    SELECT DISTINCT c.category FROM carrefour_products c
    UNION
    SELECT DISTINCT t.category FROM coto_products t
    ORDER BY 1;
END;
$$;

-- Browse by category: both tables filtered, UNION, ordered by name
CREATE OR REPLACE FUNCTION sp_products_get_by_category(p_category VARCHAR(200))
RETURNS TABLE(
    "Name"      VARCHAR,
    "Brand"     VARCHAR,
    "Category"  VARCHAR,
    "Price"     NUMERIC,
    "ListPrice" NUMERIC,
    "Promo"     VARCHAR,
    "Available" BOOLEAN,
    "Source"    VARCHAR,
    "ScrapedAt" TIMESTAMP
) LANGUAGE plpgsql AS $$
BEGIN
    RETURN QUERY
    SELECT c.name, c.brand, c.category, c.price, c.list_price,
           NULL::VARCHAR AS promo, c.available,
           'Carrefour'::VARCHAR AS source, c.scraped_at
    FROM carrefour_products c
    WHERE c.category = p_category
    UNION ALL
    SELECT t.name, t.brand, t.category, t.price, t.list_price,
           t.promo, t.available,
           'Coto'::VARCHAR AS source, t.scraped_at
    FROM coto_products t
    WHERE t.category = p_category
    ORDER BY "Name";
END;
$$;
