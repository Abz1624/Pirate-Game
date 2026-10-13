// Railway Infrastructure as Code for the playtest API (replaces server/railway.json, which Railway stops reading on
// 2026-12-01). Review with `railway config plan`, push settings with `railway config apply`; server/deploy.ps1 still
// uploads and deploys the code. Every setting below is the one server/railway.json had.
// See https://docs.railway.com/infrastructure-as-code
import { defineRailway, preserve, project, service } from "railway/iac";

// This repository manages only the api service; Postgres and anything else in the project are left alone.
export const partial = "api";

export default defineRailway(() => {
  const api = service("api", {
    build: {
      builder: "DOCKERFILE",
      dockerfilePath: "Dockerfile", // relative to the service root directory, server/
    },
    deploy: {
      numReplicas: 1, // one instance: lobby and match state live in-process (server/README.md)
      healthcheckPath: "/health",
      healthcheckTimeout: 60,
      // Restart policy stays at Railway's default, ON_FAILURE with 10 retries (railway.json's values, confirmed in the
      // deployment manifest). Railway stores the default as null, so declaring it leaves a change in every plan and
      // every apply would redeploy.
    },
    // Variables the file does not list are DELETED on apply. preserve() keeps the value Railway already holds, so no
    // value (or the DATABASE_URL reference to Postgres) lives in the repo. Add any new variable here before setting it.
    env: {
      ALLOWED_ORIGINS: preserve(),
      DATABASE_URL: preserve(),
      PLAY_URL: preserve(),
      // Hackathon (12 Oct, devnet): treasury/escrow key, owner admin key, Solana RPC and sign-in domain.
      TREASURY_SECRET_KEY: preserve(),
      ADMIN_KEY: preserve(),
      SOLANA_RPC_URL: preserve(),
      SOLANA_CLUSTER: preserve(),
      SIWS_DOMAIN: preserve(),
      // Every other variable the API reads (13 Oct hotfix: an apply must never delete one the owner set by hand).
      // ApiSettings.From, RewardService, WagerService, DailyRaceService and IslandService read these; unset ones keep
      // their code defaults (server/README.md lists them).
      PUBLIC_API_URL: preserve(),
      RATE_LIMIT_PER_MINUTE: preserve(),
      SWEEP_SECONDS: preserve(),
      ARENA_TICK_MS: preserve(),
      MATCH_SERVER_URL: preserve(),
      RUNLOG_RETENTION_DAYS: preserve(),
      RUNLOG_RETENTION_SWEEP_HOURS: preserve(),
      WAGER_MAX_LAMPORTS: preserve(),
      WEEKLY_REWARDS_AUTO: preserve(),
      WEEKLY_REWARDS_INTERVAL_MINUTES: preserve(),
      WEEKLY_REWARD_CHECKED_ONLY: preserve(),
      WEEKLY_REWARD_SOL: preserve(),
      WEEKLY_REWARD_MAP: preserve(),
      DAILY_RACE_MAP: preserve(),
      DAILY_RACE_SALT: preserve(),
      DAILY_RACE_TARGET_KILLS: preserve(),
      ALLOW_MAINNET: preserve(),
      ADMIN_PAYOUT_MAX_LAMPORTS: preserve(),
      RUM_RUN_CAP: preserve(),
      RUM_RUN_HALF: preserve(),
      RUM_DAY_CAP: preserve(),
      RUM_DAY_HALF: preserve(),
      // Match server tests (D26) add RP_MATCH_TICKET_SECRET, VOYAGE_SERVER_URL and VOYAGE_MATCH_ID here with preserve()
      // before setting them, and remove them again after the session (M9b's were removed on 11 Oct).
    },
  });
  return project("Rogue Pirates", {
    resources: [api],
  });
});
