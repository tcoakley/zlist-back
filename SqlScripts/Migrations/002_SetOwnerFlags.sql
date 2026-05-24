-- Migration 002: Set IsOwner = 1 for list creators.
-- Since all existing lists were created by the sole user, set all rows to IsOwner = 1.
-- If you have multiple users in the future, update this logic accordingly.
UPDATE UserLists SET IsOwner = 1;
