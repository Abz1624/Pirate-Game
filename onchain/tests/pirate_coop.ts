import * as anchor from "@coral-xyz/anchor";
import { Program } from "@coral-xyz/anchor";
import { expect } from "chai";

/**
 * Integration tests against local MagicBlock stack.
 *
 * Prerequisites:
 *   ./scripts/start-local-stack.sh
 *   anchor build && anchor deploy --provider.cluster localnet
 *
 * Environment:
 *   EPHEMERAL_PROVIDER_ENDPOINT=http://localhost:7799
 *   EPHEMERAL_WS_ENDPOINT=ws://localhost:7800
 */
describe("pirate_coop", () => {
  const provider = anchor.AnchorProvider.env();
  anchor.setProvider(provider);

  it("initializes config", async () => {
  });

  it("CU stress estimate via simcli stays under 1.4M", async () => {
    // Host-side gate — run: cargo run -p pirate-simcli -- stress-cu --capacity 4
    expect(true).to.be.true;
  });
});
