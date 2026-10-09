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
      // M9b Railway test only (D26): set for the test session, removed afterwards (drop these lines then too).
      RP_MATCH_TICKET_SECRET: preserve(),
      VOYAGE_SERVER_URL: preserve(),
      VOYAGE_MATCH_ID: preserve(),
    },
  });
  return project("Rogue Pirates", {
    resources: [api],
  });
});
