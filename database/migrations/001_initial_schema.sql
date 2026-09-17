CREATE TABLE dbo.Competitions
(
    Id uniqueidentifier NOT NULL CONSTRAINT PK_Competitions PRIMARY KEY,
    Name nvarchar(200) NOT NULL,
    Country nvarchar(100) NULL
);

CREATE TABLE dbo.Seasons
(
    Id uniqueidentifier NOT NULL CONSTRAINT PK_Seasons PRIMARY KEY,
    CompetitionId uniqueidentifier NOT NULL,
    Name nvarchar(100) NOT NULL,
    StartDate date NOT NULL,
    EndDate date NOT NULL,
    CONSTRAINT FK_Seasons_Competitions FOREIGN KEY (CompetitionId) REFERENCES dbo.Competitions(Id),
    CONSTRAINT CK_Seasons_DateRange CHECK (StartDate <= EndDate)
);

CREATE TABLE dbo.Teams
(
    Id uniqueidentifier NOT NULL CONSTRAINT PK_Teams PRIMARY KEY,
    Name nvarchar(200) NOT NULL
);

CREATE TABLE dbo.Players
(
    Id uniqueidentifier NOT NULL CONSTRAINT PK_Players PRIMARY KEY,
    FullName nvarchar(200) NOT NULL,
    DateOfBirth date NULL
);

CREATE TABLE dbo.Fixtures
(
    Id uniqueidentifier NOT NULL CONSTRAINT PK_Fixtures PRIMARY KEY,
    CompetitionId uniqueidentifier NOT NULL,
    SeasonId uniqueidentifier NOT NULL,
    HomeTeamId uniqueidentifier NOT NULL,
    AwayTeamId uniqueidentifier NOT NULL,
    KickoffUtc datetime2 NOT NULL,
    Status tinyint NOT NULL,
    HomeScore int NULL,
    AwayScore int NULL,
    CONSTRAINT FK_Fixtures_Competitions FOREIGN KEY (CompetitionId) REFERENCES dbo.Competitions(Id),
    CONSTRAINT FK_Fixtures_Seasons FOREIGN KEY (SeasonId) REFERENCES dbo.Seasons(Id),
    CONSTRAINT FK_Fixtures_HomeTeams FOREIGN KEY (HomeTeamId) REFERENCES dbo.Teams(Id),
    CONSTRAINT FK_Fixtures_AwayTeams FOREIGN KEY (AwayTeamId) REFERENCES dbo.Teams(Id),
    CONSTRAINT CK_Fixtures_DifferentTeams CHECK (HomeTeamId <> AwayTeamId),
    CONSTRAINT CK_Fixtures_Status CHECK (Status BETWEEN 0 AND 4)
);

CREATE TABLE dbo.ProviderIdentifiers
(
    Id uniqueidentifier NOT NULL CONSTRAINT DF_ProviderIdentifiers_Id DEFAULT NEWSEQUENTIALID(),
    Provider nvarchar(100) NOT NULL,
    EntityType tinyint NOT NULL,
    InternalEntityId uniqueidentifier NOT NULL,
    ExternalId nvarchar(200) NOT NULL,
    CONSTRAINT PK_ProviderIdentifiers PRIMARY KEY (Id),
    CONSTRAINT UQ_ProviderIdentifiers_External UNIQUE (Provider, EntityType, ExternalId),
    CONSTRAINT UQ_ProviderIdentifiers_Internal UNIQUE (Provider, EntityType, InternalEntityId)
);

CREATE INDEX IX_Fixtures_KickoffUtc ON dbo.Fixtures(KickoffUtc);
CREATE INDEX IX_Fixtures_HomeTeamId ON dbo.Fixtures(HomeTeamId);
CREATE INDEX IX_Fixtures_AwayTeamId ON dbo.Fixtures(AwayTeamId);
CREATE INDEX IX_Fixtures_CompetitionId_SeasonId ON dbo.Fixtures(CompetitionId, SeasonId);
