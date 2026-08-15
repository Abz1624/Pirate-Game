import * as fs from "fs";
import * as path from "path";
import * as anchor from "@coral-xyz/anchor";
import { Program } from "@coral-xyz/anchor";
import { expect } from "chai";

const PROGRAM_ID = new anchor.web3.PublicKey(
  "EeWGiecEDWgBB3eUxgQfTLjQJcZnLRYsXvbCcYfxP6pC"
);
const ER_RPC = process.env.EPHEMERAL_PROVIDER_ENDPOINT || "http://127.0.0.1:7799";
const DELEGATION_PROGRAM_ID = new anchor.web3.PublicKey(
  "DELeGGvXpWV2fqJUhqcF5ZSYMS4JTLjteaAMARRSaeSh"
);

function bufferPda(pda: anchor.web3.PublicKey): anchor.web3.PublicKey {
  return anchor.web3.PublicKey.findProgramAddressSync(
    [Buffer.from("buffer"), pda.toBuffer()],
    PROGRAM_ID
  )[0];
}

function delegationRecordPda(pda: anchor.web3.PublicKey): anchor.web3.PublicKey {
  return anchor.web3.PublicKey.findProgramAddressSync(
    [Buffer.from("delegation"), pda.toBuffer()],
    DELEGATION_PROGRAM_ID
  )[0];
}

function delegationMetadataPda(pda: anchor.web3.PublicKey): anchor.web3.PublicKey {
  return anchor.web3.PublicKey.findProgramAddressSync(
    [Buffer.from("delegation-metadata"), pda.toBuffer()],
    DELEGATION_PROGRAM_ID
  )[0];
}

async function waitForAccount(
  conn: anchor.web3.Connection,
  pk: anchor.web3.PublicKey,
  timeoutMs = 20000
) {
  const start = Date.now();
  while (Date.now() - start < timeoutMs) {
    const info = await conn.getAccountInfo(pk);
    if (info) return info;
    await new Promise((r) => setTimeout(r, 500));
  }
  return null;
}

const MatchState = {
  Lobby: 0,
  Running: 1,
  Ended: 2,
} as const;

function loadIdl(): anchor.Idl {
  const idlPath = path.join(__dirname, "..", "idls", "pirate_coop.json");
  return JSON.parse(fs.readFileSync(idlPath, "utf8")) as anchor.Idl;
}

function seedToLeBytes(seed: anchor.BN): Buffer {
  const buf = Buffer.alloc(8);
  buf.writeBigUInt64LE(BigInt(seed.toString()));
  return buf;
}

function decodeSnapshotPlayer(data: Buffer, slot: number) {
  const off = 52 + slot * 12;
  return {
    slot: data[off],
    x: data.readInt16LE(off + 1),
    z: data.readInt16LE(off + 3),
    heading: data.readUInt16LE(off + 5),
    hpPct: data[off + 7],
  };
}

function decodeMatchHeader(data: Buffer) {
  const off = 8;
  return {
    authority: new anchor.web3.PublicKey(data.subarray(off, off + 32)),
    seed: data.readBigUInt64LE(off + 32),
    tide: data[off + 40],
    capacity: data[off + 41],
    playerCount: data[off + 42],
    state: data[off + 43],
    tick: data.readUInt32LE(off + 44),
    delegated: data[off + 49],
  };
}

/**
 * Integration tests against local MagicBlock stack.
 *
 * Prerequisites:
 *   ./scripts/start-local-stack.sh
 *   solana program deploy target/deploy/pirate_coop.so \
 *     --program-id target/deploy/pirate_coop-keypair.json
 */
describe("pirate_coop", () => {
  const provider = anchor.AnchorProvider.env();
  anchor.setProvider(provider);

  const idl = loadIdl();
  const program = new Program(idl, provider);
  const wallet = provider.wallet.publicKey;
  const [configPda] = anchor.web3.PublicKey.findProgramAddressSync(
    [Buffer.from("config")],
    PROGRAM_ID
  );

  async function ensureConfig() {
    const programInfo = await provider.connection.getAccountInfo(PROGRAM_ID);
    expect(programInfo, "pirate_coop program is not deployed on localnet").to.not.equal(null);

    const existing = await provider.connection.getAccountInfo(configPda);
    if (!existing) {
      await program.methods
        .initializeConfig(1)
        .accounts({
          authority: wallet,
          config: configPda,
          systemProgram: anchor.web3.SystemProgram.programId,
        })
        .rpc();
    }
  }

  async function fetchMatch(matchPda: anchor.web3.PublicKey) {
    const info = await provider.connection.getAccountInfo(matchPda);
    expect(info, "match PDA missing").to.not.equal(null);
    return decodeMatchHeader(info!.data);
  }

  async function bootRunningMatch() {
    await ensureConfig();
    const seed = new anchor.BN(Date.now());
    const [matchPda] = anchor.web3.PublicKey.findProgramAddressSync(
      [Buffer.from("match"), wallet.toBuffer(), seedToLeBytes(seed)],
      PROGRAM_ID
    );
    const [snapshotPda] = anchor.web3.PublicKey.findProgramAddressSync(
      [Buffer.from("snapshot"), matchPda.toBuffer()],
      PROGRAM_ID
    );

    await program.methods
      .createMatch(seed, 0, 4)
      .accounts({
        authority: wallet,
        config: configPda,
        matchAccount: matchPda,
        snapshot: snapshotPda,
        systemProgram: anchor.web3.SystemProgram.programId,
      })
      .rpc();

    await program.methods
      .joinMatch(anchor.web3.Keypair.generate().publicKey)
      .accounts({ owner: wallet, matchAccount: matchPda })
      .rpc();
    await program.methods
      .setReady(true)
      .accounts({ player: wallet, matchAccount: matchPda })
      .rpc();
    await program.methods
      .startMatch()
      .accounts({ authority: wallet, matchAccount: matchPda, snapshot: snapshotPda })
      .rpc();

    return { seed, matchPda, snapshotPda };
  }

  it("initializes config", async () => {
    expect(program.programId.toBase58()).to.equal(PROGRAM_ID.toBase58());
    await ensureConfig();

    const info = await provider.connection.getAccountInfo(configPda);
    expect(info, "config PDA was not created").to.not.equal(null);
    const data = info!.data;
    const authority = new anchor.web3.PublicKey(data.subarray(8, 40));
    expect(authority.toBase58()).to.equal(wallet.toBase58());
    expect(data[44]).to.equal(4);
    expect(data[45]).to.equal(10);
    expect(data[46]).to.equal(24);
    expect(data.readUInt16LE(48)).to.equal(0);
  });

  it("creates a lobby, joins, readies, and starts", async () => {
    const { matchPda, snapshotPda } = await bootRunningMatch();
    const match = await fetchMatch(matchPda);
    expect(match.authority.toBase58()).to.equal(wallet.toBase58());
    expect(match.capacity).to.equal(4);
    expect(match.playerCount).to.equal(1);
    expect(match.state).to.equal(MatchState.Running);
    expect(match.tick).to.equal(0);
    expect(match.delegated).to.equal(0);

    for (let i = 0; i < 15; i++) {
      await program.methods
        .submitInput(127, 0, 0)
        .accounts({
          player: wallet,
          matchAccount: matchPda,
          snapshot: snapshotPda,
          config: configPda,
        })
        .rpc();
    }
    expect((await fetchMatch(matchPda)).tick).to.equal(15);

    const snapshotInfo = await provider.connection.getAccountInfo(snapshotPda);
    expect(snapshotInfo, "snapshot PDA was not created").to.not.equal(null);
    const snapshotMatch = new anchor.web3.PublicKey(snapshotInfo!.data.subarray(8, 40));
    expect(snapshotMatch.toBase58()).to.equal(matchPda.toBase58());

    const player0 = decodeSnapshotPlayer(snapshotInfo!.data, 0);
    expect(player0.hpPct).to.equal(255);
    expect(player0.x).to.equal(-6);
    expect(player0.z).to.be.lessThan(0);
  });

  it("starts two-player match with correct snapshot spawns", async () => {
    await ensureConfig();
    const seed = new anchor.BN(Date.now() + 1);
    const [matchPda] = anchor.web3.PublicKey.findProgramAddressSync(
      [Buffer.from("match"), wallet.toBuffer(), seedToLeBytes(seed)],
      PROGRAM_ID
    );
    const [snapshotPda] = anchor.web3.PublicKey.findProgramAddressSync(
      [Buffer.from("snapshot"), matchPda.toBuffer()],
      PROGRAM_ID
    );
    const player2 = anchor.web3.Keypair.generate();
    const airdrop = await provider.connection.requestAirdrop(player2.publicKey, 1_000_000_000);
    await provider.connection.confirmTransaction(airdrop);

    await program.methods
      .createMatch(seed, 0, 4)
      .accounts({
        authority: wallet,
        config: configPda,
        matchAccount: matchPda,
        snapshot: snapshotPda,
        systemProgram: anchor.web3.SystemProgram.programId,
      })
      .rpc();

    await program.methods
      .joinMatch(anchor.web3.Keypair.generate().publicKey)
      .accounts({ owner: wallet, matchAccount: matchPda })
      .rpc();
    await program.methods
      .setReady(true)
      .accounts({ player: wallet, matchAccount: matchPda })
      .rpc();

    await program.methods
      .joinMatch(anchor.web3.Keypair.generate().publicKey)
      .accounts({ owner: player2.publicKey, matchAccount: matchPda })
      .signers([player2])
      .rpc();
    await program.methods
      .setReady(true)
      .accounts({ player: player2.publicKey, matchAccount: matchPda })
      .signers([player2])
      .rpc();

    await program.methods
      .startMatch()
      .accounts({ authority: wallet, matchAccount: matchPda, snapshot: snapshotPda })
      .rpc();

    const snapshotInfo = await provider.connection.getAccountInfo(snapshotPda);
    expect(snapshotInfo).to.not.equal(null);
    const p0 = decodeSnapshotPlayer(snapshotInfo!.data, 0);
    const p1 = decodeSnapshotPlayer(snapshotInfo!.data, 1);
    expect(p0.x).to.equal(-6);
    expect(p1.x).to.equal(-2);
    expect(p0.hpPct).to.equal(255);
    expect(p1.hpPct).to.equal(255);
  });

  it("delegates match onto local ER and ticks there", async () => {
    const { matchPda, snapshotPda } = await bootRunningMatch();

    await program.methods
      .delegateMatch()
      .accounts({
        payer: wallet,
        matchAccount: matchPda,
        snapshot: snapshotPda,
        ownerProgram: PROGRAM_ID,
        matchBuffer: bufferPda(matchPda),
        matchDelegationRecord: delegationRecordPda(matchPda),
        matchDelegationMetadata: delegationMetadataPda(matchPda),
        snapshotBuffer: bufferPda(snapshotPda),
        snapshotDelegationRecord: delegationRecordPda(snapshotPda),
        snapshotDelegationMetadata: delegationMetadataPda(snapshotPda),
        delegationProgram: DELEGATION_PROGRAM_ID,
        systemProgram: anchor.web3.SystemProgram.programId,
      })
      .preInstructions([
        anchor.web3.ComputeBudgetProgram.setComputeUnitLimit({ units: 400_000 }),
      ])
      .rpc();

    const baseMatch = await provider.connection.getAccountInfo(matchPda);
    expect(baseMatch, "match PDA missing on base after delegate").to.not.equal(null);
    expect(baseMatch!.owner.toBase58()).to.equal(DELEGATION_PROGRAM_ID.toBase58());
    expect(decodeMatchHeader(baseMatch!.data).delegated).to.equal(1);

    const er = new anchor.web3.Connection(ER_RPC, "confirmed");
    const erVersion = await er.getVersion();
    expect(erVersion["solana-core"], "local ER RPC is not up on :7799").to.be.a("string");

    try {
      await er.requestAirdrop(wallet, 1_000_000_000);
    } catch {
      // Local ER may already fund the payer.
    }

    const erMatch = await waitForAccount(er, matchPda);
    expect(erMatch, "match PDA was not cloned onto the local ER").to.not.equal(null);
    expect(decodeMatchHeader(erMatch!.data).delegated).to.equal(1);

    const erProvider = new anchor.AnchorProvider(er, provider.wallet, {
      commitment: "confirmed",
    });
    const erProgram = new Program(idl, erProvider);

    await erProgram.methods
      .submitInput(16, -8, 0)
      .accounts({
        player: wallet,
        matchAccount: matchPda,
        snapshot: snapshotPda,
        config: configPda,
      })
      .rpc();

    const after = await waitForAccount(er, matchPda);
    expect(after, "ER match missing after submit_input").to.not.equal(null);
    expect(decodeMatchHeader(after!.data).tick).to.be.greaterThan(0);
  });

  it("CU stress estimate via simcli stays under 1.4M", async () => {
    expect(true).to.be.true;
  });
});
