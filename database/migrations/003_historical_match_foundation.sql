CREATE TABLE dbo.FixtureScheduleObservations
(
    Id uniqueidentifier NOT NULL CONSTRAINT PK_FixtureScheduleObservations PRIMARY KEY,
    FixtureId uniqueidentifier NOT NULL,
    Provider nvarchar(100) NOT NULL,
    KickoffUtc datetime2 NOT NULL,
    Status tinyint NOT NULL,
    ObservedAtUtc datetime2 NULL,
    AvailableAtUtc datetime2 NOT NULL,
    IngestedAtUtc datetime2 NOT NULL,
    IngestionRunId uniqueidentifier NOT NULL,
    SourceFingerprint varchar(64) NOT NULL,
    CONSTRAINT FK_FixtureScheduleObservations_Fixtures FOREIGN KEY (FixtureId) REFERENCES dbo.Fixtures(Id),
    CONSTRAINT FK_FixtureScheduleObservations_IngestionRuns FOREIGN KEY (IngestionRunId) REFERENCES dbo.IngestionRuns(Id),
    CONSTRAINT CK_FixtureScheduleObservations_Status CHECK (Status BETWEEN 0 AND 4),
    CONSTRAINT CK_FixtureScheduleObservations_Temporal CHECK (AvailableAtUtc <= IngestedAtUtc),
    CONSTRAINT UQ_FixtureScheduleObservations_Source UNIQUE (FixtureId, Provider, SourceFingerprint)
);

CREATE INDEX IX_FixtureScheduleObservations_AsOf
    ON dbo.FixtureScheduleObservations(FixtureId, Provider, AvailableAtUtc, IngestedAtUtc);

CREATE TABLE dbo.FixtureStatisticsSnapshots
(
    Id uniqueidentifier NOT NULL CONSTRAINT PK_FixtureStatisticsSnapshots PRIMARY KEY,
    FixtureId uniqueidentifier NOT NULL,
    Provider nvarchar(100) NOT NULL,
    ObservedAtUtc datetime2 NULL,
    AvailableAtUtc datetime2 NOT NULL,
    IngestedAtUtc datetime2 NOT NULL,
    IngestionRunId uniqueidentifier NOT NULL,
    SourceFingerprint varchar(64) NOT NULL,
    CONSTRAINT FK_FixtureStatisticsSnapshots_Fixtures FOREIGN KEY (FixtureId) REFERENCES dbo.Fixtures(Id),
    CONSTRAINT FK_FixtureStatisticsSnapshots_IngestionRuns FOREIGN KEY (IngestionRunId) REFERENCES dbo.IngestionRuns(Id),
    CONSTRAINT CK_FixtureStatisticsSnapshots_Temporal CHECK (AvailableAtUtc <= IngestedAtUtc),
    CONSTRAINT UQ_FixtureStatisticsSnapshots_Source UNIQUE (FixtureId, Provider, SourceFingerprint)
);

CREATE INDEX IX_FixtureStatisticsSnapshots_AsOf
    ON dbo.FixtureStatisticsSnapshots(FixtureId, Provider, AvailableAtUtc, IngestedAtUtc);

CREATE TABLE dbo.TeamMatchStatistics
(
    Id uniqueidentifier NOT NULL CONSTRAINT PK_TeamMatchStatistics PRIMARY KEY,
    FixtureStatisticsSnapshotId uniqueidentifier NOT NULL,
    TeamId uniqueidentifier NOT NULL,
    Goals int NULL,
    Shots int NULL,
    ShotsOnTarget int NULL,
    PossessionPercentage decimal(5,2) NULL,
    Corners int NULL,
    Offsides int NULL,
    Fouls int NULL,
    YellowCards int NULL,
    RedCards int NULL,
    CONSTRAINT FK_TeamMatchStatistics_Snapshots FOREIGN KEY (FixtureStatisticsSnapshotId) REFERENCES dbo.FixtureStatisticsSnapshots(Id),
    CONSTRAINT FK_TeamMatchStatistics_Teams FOREIGN KEY (TeamId) REFERENCES dbo.Teams(Id),
    CONSTRAINT UQ_TeamMatchStatistics_SnapshotTeam UNIQUE (FixtureStatisticsSnapshotId, TeamId),
    CONSTRAINT CK_TeamMatchStatistics_NonNegative CHECK (
        (Goals IS NULL OR Goals >= 0) AND (Shots IS NULL OR Shots >= 0) AND
        (ShotsOnTarget IS NULL OR ShotsOnTarget >= 0) AND (Corners IS NULL OR Corners >= 0) AND
        (Offsides IS NULL OR Offsides >= 0) AND (Fouls IS NULL OR Fouls >= 0) AND
        (YellowCards IS NULL OR YellowCards >= 0) AND (RedCards IS NULL OR RedCards >= 0)),
    CONSTRAINT CK_TeamMatchStatistics_Possession CHECK (PossessionPercentage IS NULL OR PossessionPercentage BETWEEN 0 AND 100)
);
