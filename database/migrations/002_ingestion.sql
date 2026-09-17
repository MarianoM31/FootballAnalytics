CREATE TABLE dbo.IngestionRuns
(
    Id uniqueidentifier NOT NULL CONSTRAINT PK_IngestionRuns PRIMARY KEY,
    Provider nvarchar(100) NOT NULL,
    StartedAtUtc datetime2 NOT NULL,
    CompletedAtUtc datetime2 NULL,
    Status tinyint NOT NULL,
    CompetitionsProcessed int NOT NULL CONSTRAINT DF_IngestionRuns_CompetitionsProcessed DEFAULT 0,
    SeasonsProcessed int NOT NULL CONSTRAINT DF_IngestionRuns_SeasonsProcessed DEFAULT 0,
    TeamsProcessed int NOT NULL CONSTRAINT DF_IngestionRuns_TeamsProcessed DEFAULT 0,
    FixturesProcessed int NOT NULL CONSTRAINT DF_IngestionRuns_FixturesProcessed DEFAULT 0,
    ErrorMessage nvarchar(500) NULL,
    CONSTRAINT CK_IngestionRuns_Status CHECK (Status BETWEEN 0 AND 2),
    CONSTRAINT CK_IngestionRuns_Counters CHECK (
        CompetitionsProcessed >= 0 AND SeasonsProcessed >= 0 AND TeamsProcessed >= 0 AND FixturesProcessed >= 0)
);

CREATE INDEX IX_IngestionRuns_Provider_StartedAtUtc ON dbo.IngestionRuns(Provider, StartedAtUtc DESC);
