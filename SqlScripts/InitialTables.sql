-- Users
CREATE TABLE Users (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Email NVARCHAR(256) NOT NULL,
    Password NVARCHAR(MAX) NOT NULL,
    ResetPassword NVARCHAR(MAX) NULL,
    FirstName NVARCHAR(100) NULL,
    LastName NVARCHAR(100) NULL,
    Subscription VARCHAR(20) NOT NULL DEFAULT 'free',
    SubscriptionExpiresAt DATETIME2 NULL,
    IsHelpEnabled BIT NOT NULL DEFAULT 1,
    SortCompletedToBottom BIT NOT NULL DEFAULT 1,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NULL
);

-- Create a unique index for Email
CREATE UNIQUE INDEX IX_Users_Email ON Users(Email);

-- UserPaymentHistory
CREATE TABLE UserPaymentHistory (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    UserId INT NOT NULL,
    StripeEventId VARCHAR(100) NOT NULL,
    AmountPaid DECIMAL(10,2) NOT NULL,
    Currency VARCHAR(10) NOT NULL DEFAULT 'usd',
    PlanType VARCHAR(20) NOT NULL,
    PaidAt DATETIME2 NOT NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_UserPaymentHistory_Users FOREIGN KEY (UserId) REFERENCES Users(Id),
    CONSTRAINT UQ_UserPaymentHistory_StripeEventId UNIQUE (StripeEventId)
);

-- Refresh Token
CREATE TABLE [dbo].[RefreshTokens] (
	[Id] INT IDENTITY(1,1) PRIMARY KEY,
	[UserId] INT NOT NULL,
	[Token] NVARCHAR(255) NOT NULL,
	[ExpiresAt] DATETIME NOT NULL,
	[CreatedAt] DATETIME NOT NULL,
	[Revoked] BIT NOT NULL DEFAULT 0,
	CONSTRAINT FK_RefreshTokens_Users FOREIGN KEY (UserId) REFERENCES Users(Id)
);



-- 1. Lists
CREATE TABLE Lists (
	Id INT IDENTITY(1,1) PRIMARY KEY,
	ListName NVARCHAR(255) NOT NULL,
	ListDescription NVARCHAR(MAX) NULL,
	CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
	UpdatedAt DATETIME2 NULL
);

-- 2. ListItems
CREATE TABLE ListItems (
	Id INT IDENTITY(1,1) PRIMARY KEY,
	ListId INT NOT NULL,
	ItemName NVARCHAR(255) NOT NULL,
	ItemDescription NVARCHAR(MAX) NULL,
	SortOrder INT NOT NULL DEFAULT(0),
	FOREIGN KEY (ListId) REFERENCES Lists(Id)
);

-- 3. UserLists
CREATE TABLE UserLists (
	Id INT IDENTITY(1,1) PRIMARY KEY,
	UserId INT NOT NULL,
	ListId INT NOT NULL,
	IsOwner BIT NOT NULL DEFAULT 0,
	FOREIGN KEY (UserId) REFERENCES Users(Id),
	FOREIGN KEY (ListId) REFERENCES Lists(Id)
);

-- 4. ListInvitations
CREATE TABLE ListInvitations (
	Id INT IDENTITY(1,1) PRIMARY KEY,
	ListId INT NOT NULL,
	InvitedByUserId INT NOT NULL,
	InvitedEmail NVARCHAR(256) NOT NULL,
	Token NVARCHAR(64) NOT NULL,
	Status NVARCHAR(20) NOT NULL DEFAULT 'pending',
	CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
	ExpiresAt DATETIME2 NOT NULL,
	AcceptedByUserId INT NULL,
	CONSTRAINT FK_ListInvitations_Lists FOREIGN KEY (ListId) REFERENCES Lists(Id),
	CONSTRAINT FK_ListInvitations_InvitedBy FOREIGN KEY (InvitedByUserId) REFERENCES Users(Id),
	CONSTRAINT FK_ListInvitations_AcceptedBy FOREIGN KEY (AcceptedByUserId) REFERENCES Users(Id),
	CONSTRAINT UQ_ListInvitations_Token UNIQUE (Token)
);

CREATE INDEX IX_ListInvitations_ListId ON ListInvitations(ListId);
CREATE INDEX IX_ListInvitations_InvitedEmail ON ListInvitations(InvitedEmail);

-- 5. ListRuns
CREATE TABLE ListRuns (
	Id INT IDENTITY(1,1) PRIMARY KEY,
	ListId INT NOT NULL,
	CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
	CompletedAt DATETIME2 NULL,
	CompletedBy INT NULL,
	FOREIGN KEY (ListId) REFERENCES Lists(Id),
	CONSTRAINT FK_ListRuns_CompletedBy FOREIGN KEY (CompletedBy) REFERENCES Users(Id)
);

CREATE INDEX IX_ListRuns_CompletedBy ON ListRuns(CompletedBy);

-- 5. ListRunItems
CREATE TABLE ListRunItems (
	Id INT IDENTITY(1,1) PRIMARY KEY,
	ListRunId INT NOT NULL,
	ListItemId INT NULL,
	ListItemName NVARCHAR(255) NOT NULL,
	ListItemDescription NVARCHAR(MAX) NULL,
	SortOrder INT NOT NULL DEFAULT 0,
	CompletedAt DATETIME2 NULL,
	CompletedBy INT NULL,
	FOREIGN KEY (ListRunId) REFERENCES ListRuns(Id),
	FOREIGN KEY (ListItemId) REFERENCES ListItems(Id)
);

-- 6. AppVersions
CREATE TABLE AppVersions (
	Id         INT           IDENTITY(1,1) PRIMARY KEY,
	Version    NVARCHAR(20)  NOT NULL,
	ReleasedAt DATETIME      NOT NULL DEFAULT GETUTCDATE(),
	Notes      NVARCHAR(MAX) NULL
);

INSERT INTO AppVersions (Version, ReleasedAt, Notes)
VALUES (
	'1.0.0',
	GETUTCDATE(),
	'Initial release of zChecklist.
- Repeatable checklists with full run history
- Shared lists with member invitations
- Real-time collaboration with live check/uncheck updates
- One-time run items
- Mobile-friendly design'
);
