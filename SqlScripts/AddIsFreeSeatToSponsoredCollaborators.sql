ALTER TABLE SponsoredCollaborators ADD IsFreeSeat BIT NOT NULL DEFAULT 0;

-- For existing data: mark the earliest active record per sponsor as the free seat.
-- This is the best approximation for production data created before this column existed.
UPDATE sc
SET sc.IsFreeSeat = 1
FROM SponsoredCollaborators sc
INNER JOIN (
    SELECT SponsorUserId, MIN(Id) AS FirstId
    FROM SponsoredCollaborators
    WHERE IsActive = 1
    GROUP BY SponsorUserId
) first ON first.SponsorUserId = sc.SponsorUserId AND first.FirstId = sc.Id;
