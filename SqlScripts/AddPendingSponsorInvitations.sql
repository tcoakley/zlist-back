CREATE TABLE PendingSponsorInvitations (
    Id            INT           IDENTITY(1,1) PRIMARY KEY,
    SponsorUserId INT           NOT NULL,
    InvitedEmail  NVARCHAR(256) NOT NULL,
    Token         NVARCHAR(64)  NOT NULL,
    CreatedAt     DATETIME2     NOT NULL DEFAULT GETUTCDATE(),
    ExpiresAt     DATETIME2     NOT NULL,
    CONSTRAINT FK_PendingSponsorInvitations_Sponsor FOREIGN KEY (SponsorUserId) REFERENCES Users(Id),
    CONSTRAINT UQ_PendingSponsorInvitations_Token UNIQUE (Token),
    CONSTRAINT UQ_PendingSponsorInvitations_SponsorEmail UNIQUE (SponsorUserId, InvitedEmail)
);

CREATE INDEX IX_PendingSponsorInvitations_Sponsor ON PendingSponsorInvitations(SponsorUserId);
CREATE INDEX IX_PendingSponsorInvitations_Email   ON PendingSponsorInvitations(InvitedEmail);
