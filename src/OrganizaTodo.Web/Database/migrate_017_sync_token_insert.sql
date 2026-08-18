-- ============================================================
-- MIGRATION 017 — SyncTokens insert function for POST /api/sync/connect
-- ============================================================

CREATE OR REPLACE FUNCTION sp_sync_tokens_store(user_id INT, token_hash VARCHAR(200))
RETURNS VOID LANGUAGE plpgsql AS $$
BEGIN
    INSERT INTO "SyncTokens" ("UserId", "TokenHash") VALUES (user_id, token_hash);
END;
$$;
