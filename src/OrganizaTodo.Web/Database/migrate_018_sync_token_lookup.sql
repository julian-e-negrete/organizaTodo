-- ============================================================
-- MIGRATION 018 — SyncTokens lookup/update functions for /api/sync's
-- bearer-token authentication (resolve a token hash to its owning user,
-- and record last-used time on each successful authenticated call).
-- ============================================================

CREATE OR REPLACE FUNCTION sp_sync_tokens_resolve_user_id(token_hash VARCHAR(200))
RETURNS INT LANGUAGE sql AS $$
    SELECT "UserId" FROM "SyncTokens" WHERE "TokenHash" = token_hash
$$;

CREATE OR REPLACE FUNCTION sp_sync_tokens_touch_last_used(token_hash VARCHAR(200))
RETURNS VOID LANGUAGE plpgsql AS $$
BEGIN
    UPDATE "SyncTokens" SET "LastUsedAt" = NOW() WHERE "TokenHash" = token_hash;
END;
$$;
