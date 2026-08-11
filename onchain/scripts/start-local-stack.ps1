# Start MagicBlock local development stack (Windows PowerShell).
# Requires: npm install -g @magicblock-labs/ephemeral-validator@latest
#           mb-test-validator (from ephemeral-validator package)

$ErrorActionPreference = "Stop"

Write-Host "Starting mb-test-validator on :8899 ..."
Start-Process -NoNewWindow mb-test-validator -ArgumentList "--reset"

Start-Sleep -Seconds 5

Write-Host "Starting ephemeral-validator on :7799 ..."
$env:RUST_LOG = "info"
Start-Process -NoNewWindow ephemeral-validator -ArgumentList @(
    "--remotes", "http://localhost:8899",
    "--remotes", "ws://localhost:8900",
    "-l", "7799",
    "--lifecycle", "ephemeral"
)

Write-Host @"

Local stack endpoints:
  Base layer RPC:  http://localhost:8899
  Base layer WS:   ws://localhost:8900
  ER RPC:          http://localhost:7799
  ER WS:           ws://localhost:7800
  ER validator:    mAGicPQYBMvcYveUZA5F5UNNwyHvfYh5xkLS2Fr1mev

Deploy program:
  cd onchain
  anchor build && anchor deploy --provider.cluster localnet

Run tests:
  EPHEMERAL_PROVIDER_ENDPOINT=http://localhost:7799 EPHEMERAL_WS_ENDPOINT=ws://localhost:7800 anchor test --skip-local-validator

"@
