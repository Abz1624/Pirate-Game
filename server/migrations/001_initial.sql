-- Rogue Pirates schema v1 (local development only)
CREATE TABLE IF NOT EXISTS accounts (
    id UUID PRIMARY KEY,
    platform_identity TEXT NOT NULL,
    is_guest BOOLEAN NOT NULL DEFAULT TRUE,
    version BIGINT NOT NULL DEFAULT 1,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS run_leases (
    id UUID PRIMARY KEY,
    account_id UUID NOT NULL REFERENCES accounts(id),
    idempotency_key TEXT NOT NULL UNIQUE,
    state TEXT NOT NULL,
    content_version TEXT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS receipts (
    id UUID PRIMARY KEY,
    account_id UUID NOT NULL REFERENCES accounts(id),
    idempotency_key TEXT NOT NULL UNIQUE,
    kind TEXT NOT NULL,
    payload JSONB NOT NULL,
    status TEXT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS ledger_balances (
    account_id UUID PRIMARY KEY REFERENCES accounts(id),
    treasury_gold BIGINT NOT NULL DEFAULT 0,
    rum BIGINT NOT NULL DEFAULT 0,
    version BIGINT NOT NULL DEFAULT 1
);
