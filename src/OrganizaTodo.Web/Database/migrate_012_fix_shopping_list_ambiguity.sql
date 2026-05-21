-- Fix: "column reference UserId is ambiguous" in sp_shopping_get_or_create_list
-- Root cause: RETURNS TABLE declares "UserId","Month","Year" as output column variables;
-- unqualified WHERE clauses in plpgsql body clash with those names.
-- Fix: qualify every column reference with a table alias and rename params to avoid shadow.
-- DROP required because PostgreSQL does not allow renaming input parameters via CREATE OR REPLACE.

DROP FUNCTION IF EXISTS sp_shopping_get_or_create_list(integer, integer, integer);

CREATE OR REPLACE FUNCTION sp_shopping_get_or_create_list(p_user_id INT, p_month INT, p_year INT)
RETURNS TABLE(
    "Id" INT, "UserId" INT, "Name" VARCHAR, "Month" INT, "Year" INT, "CreatedAt" TIMESTAMP
) LANGUAGE plpgsql AS $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM "ShoppingLists" sl
        WHERE sl."UserId" = p_user_id AND sl."Month" = p_month AND sl."Year" = p_year
    ) THEN
        INSERT INTO "ShoppingLists"("UserId","Month","Year") VALUES (p_user_id, p_month, p_year);
    END IF;
    RETURN QUERY
    SELECT l."Id", l."UserId", l."Name", l."Month", l."Year", l."CreatedAt"
    FROM "ShoppingLists" l
    WHERE l."UserId" = p_user_id AND l."Month" = p_month AND l."Year" = p_year;
END;
$$;
