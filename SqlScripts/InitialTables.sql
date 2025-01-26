CREATE TABLE Users (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Email NVARCHAR(256) NOT NULL,
    Password NVARCHAR(MAX) NOT NULL,
    FirstName NVARCHAR(100) NULL,
    LastName NVARCHAR(100) NULL,
    CreatedAt DATETIME NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME NULL
);

-- Create a unique index for Email
CREATE UNIQUE INDEX IX_Users_Email ON Users(Email);

