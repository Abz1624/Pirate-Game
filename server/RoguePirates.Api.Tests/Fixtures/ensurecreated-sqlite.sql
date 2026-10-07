-- Schema the pre-migration API built with EF Core 8 EnsureCreated (Database.GenerateCreateScript, sqlite provider);,
-- captured on 8 Oct 2026 from the net8.0 build before C9a.0. The live Railway Postgres has this schema. Do not edit.
CREATE TABLE "Lobbies" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Lobbies" PRIMARY KEY,
    "Code" TEXT NOT NULL,
    "IsPublic" INTEGER NOT NULL,
    "Status" TEXT NOT NULL,
    "HostPlayerId" TEXT NOT NULL,
    "HostStatus" TEXT NOT NULL,
    "HostDraftJson" TEXT NULL,
    "GuestPlayerId" TEXT NULL,
    "GuestStatus" TEXT NULL,
    "GuestDraftJson" TEXT NULL,
    "CreatedAt" TEXT NOT NULL,
    "UpdatedAt" TEXT NOT NULL,
    "LastActivityAt" TEXT NOT NULL,
    "MatchId" TEXT NULL,
    "Seed" INTEGER NULL,
    "MatchServer" TEXT NULL
);


CREATE TABLE "Matches" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Matches" PRIMARY KEY,
    "LobbyId" TEXT NOT NULL,
    "HostPlayerId" TEXT NOT NULL,
    "GuestPlayerId" TEXT NOT NULL,
    "Seed" INTEGER NOT NULL,
    "Status" TEXT NOT NULL,
    "WinnerPlayerId" TEXT NULL,
    "DurationSeconds" INTEGER NULL,
    "FinalStatsJson" TEXT NULL,
    "DecidedBy" TEXT NULL,
    "CreatedAt" TEXT NOT NULL,
    "FirstReportAt" TEXT NULL,
    "DecidedAt" TEXT NULL,
    "HostRatingBefore" INTEGER NULL,
    "HostRatingAfter" INTEGER NULL,
    "GuestRatingBefore" INTEGER NULL,
    "GuestRatingAfter" INTEGER NULL
);


CREATE TABLE "MatchReports" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_MatchReports" PRIMARY KEY AUTOINCREMENT,
    "MatchId" TEXT NOT NULL,
    "PlayerId" TEXT NOT NULL,
    "WinnerPlayerId" TEXT NOT NULL,
    "DurationSeconds" INTEGER NOT NULL,
    "StatsJson" TEXT NOT NULL,
    "SubmittedAt" TEXT NOT NULL
);


CREATE TABLE "Players" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Players" PRIMARY KEY,
    "Name" TEXT NOT NULL,
    "TokenHash" TEXT NOT NULL,
    "CreatedAt" TEXT NOT NULL,
    "Rating" INTEGER NOT NULL,
    "Wins" INTEGER NOT NULL,
    "Losses" INTEGER NOT NULL
);


CREATE TABLE "Runs" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Runs" PRIMARY KEY AUTOINCREMENT,
    "PlayerId" TEXT NOT NULL,
    "Kills" INTEGER NOT NULL,
    "RunSeconds" INTEGER NOT NULL,
    "Level" INTEGER NOT NULL,
    "Captain" TEXT NULL,
    "MapId" TEXT NOT NULL,
    "BuildHash" TEXT NULL,
    "ClientVersion" TEXT NULL,
    "WeekStart" TEXT NOT NULL,
    "SubmittedAt" TEXT NOT NULL
);


CREATE TABLE "WeeklyBests" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_WeeklyBests" PRIMARY KEY AUTOINCREMENT,
    "PlayerId" TEXT NOT NULL,
    "WeekStart" TEXT NOT NULL,
    "MapId" TEXT NOT NULL,
    "Kills" INTEGER NOT NULL,
    "RunSeconds" INTEGER NOT NULL,
    "Level" INTEGER NOT NULL,
    "Captain" TEXT NULL,
    "RunId" INTEGER NOT NULL,
    "AchievedAt" TEXT NOT NULL
);


CREATE UNIQUE INDEX "IX_Lobbies_Code" ON "Lobbies" ("Code");


CREATE INDEX "IX_Lobbies_Status_IsPublic" ON "Lobbies" ("Status", "IsPublic");


CREATE INDEX "IX_Matches_Status" ON "Matches" ("Status");


CREATE UNIQUE INDEX "IX_MatchReports_MatchId_PlayerId" ON "MatchReports" ("MatchId", "PlayerId");


CREATE UNIQUE INDEX "IX_Players_TokenHash" ON "Players" ("TokenHash");


CREATE INDEX "IX_Runs_PlayerId" ON "Runs" ("PlayerId");


CREATE INDEX "IX_WeeklyBests_WeekStart_MapId_Kills" ON "WeeklyBests" ("WeekStart", "MapId", "Kills");


CREATE UNIQUE INDEX "IX_WeeklyBests_WeekStart_MapId_PlayerId" ON "WeeklyBests" ("WeekStart", "MapId", "PlayerId");


