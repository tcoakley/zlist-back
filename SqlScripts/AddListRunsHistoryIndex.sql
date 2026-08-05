-- Adds a missing index on ListRuns(ListId, CreatedAt) — every run-history query (per-list and
-- the new cross-list history) filters by ListId and orders by CreatedAt, and this was previously
-- a full scan. Purely additive, no risk to existing queries.
CREATE INDEX IX_ListRuns_ListId_CreatedAt ON ListRuns(ListId, CreatedAt DESC);
