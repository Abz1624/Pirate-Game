# Incident response playbook (draft)

1. Detect via alerts or player report.
2. Triage severity: P0 economy duplication, P1 account loss, P2 gameplay, P3 cosmetic.
3. Freeze affected commands (run leases, grants, raids).
4. Capture logs, content version, account IDs (hashed in external comms).
5. Roll back content version if simulation mismatch suspected.
6. Issue idempotent corrective receipts — never double-grant while fixing.
7. Postmortem within 72h with owner sign-off.

# Support runbook (draft)

- Account recovery: verify platform identity → link guest → restore from backup snapshot.
- Purchase restore: sandbox first; production requires store receipt validation.
- Prize dispute: compare manifest line ID + winner account + grant idempotency key.
