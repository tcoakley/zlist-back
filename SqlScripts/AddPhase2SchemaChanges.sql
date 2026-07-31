-- Phase 2 combined schema changes (2026-07-30)
-- Run once against the DB ahead of building the Phase 2 backlog items one at a time.
-- Covers: (1) Brute force login protection, (2) Google login/signup, (3) Subtasks, (6) Mobile autofocus preference.

-- 1. Brute force login protection
ALTER TABLE Users ADD FailedLoginAttempts INT NOT NULL DEFAULT 0;
ALTER TABLE Users ADD LockoutUntil DATETIME2 NULL;

-- 2. Google login/signup — allow password-less (Google-only) accounts
ALTER TABLE Users ALTER COLUMN Password NVARCHAR(MAX) NULL;

-- 3. Subtasks — one level of nesting only (a subtask cannot itself have subtasks);
-- that rule is enforced at the service layer, not by the schema.
ALTER TABLE ListItems ADD ParentId INT NULL;
ALTER TABLE ListItems ADD CONSTRAINT FK_ListItems_ParentId FOREIGN KEY (ParentId) REFERENCES ListItems(Id);
CREATE INDEX IX_ListItems_ParentId ON ListItems(ParentId);

ALTER TABLE ListRunItems ADD ParentId INT NULL;
ALTER TABLE ListRunItems ADD CONSTRAINT FK_ListRunItems_ParentId FOREIGN KEY (ParentId) REFERENCES ListRunItems(Id);
CREATE INDEX IX_ListRunItems_ParentId ON ListRunItems(ParentId);

-- 6. Mobile autofocus preference — defaults to disabled (opt-in). The directive already
-- skips autofocus on mobile by default; this column only re-enables it for a user who has
-- explicitly turned it on in Profile > Settings, so it must default off, not on like the
-- IsHelpEnabled / SortCompletedToBottom toggles.
ALTER TABLE Users ADD AutofocusEnabled BIT NOT NULL DEFAULT 0;
