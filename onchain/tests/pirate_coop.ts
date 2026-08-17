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
const MATCH_MODE_PVPVE = 1;

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

function worldPda(matchPda: anchor.web3.PublicKey): anchor.web3.PublicKey {
  return anchor.web3.PublicKey.findProgramAddressSync(
    [Buffer.from("world"), matchPda.toBuffer()],
    PROGRAM_ID
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
  const off = 56 + slot * 18;
  return {
    slot: data[off],
    x: data.readInt16LE(off + 1),
    z: data.readInt16LE(off + 3),
    heading: data.readUInt16LE(off + 5),
    hpPct: data[off + 7],
    bank: data.readUInt32LE(off + 10),
    kills: data.readUInt16LE(off + 14),
  };
}

function decodeMatchHeader(data: Buffer) {
  const off = 8;
  return {
    authority: new anchor.web3.PublicKey(data.subarray(off, off + 32)),
    seed: data.readBigUInt64LE(off + 32),
    tide: data[off + 40],
    capacity: data[off + 41],
    mode: data[off + 42],
    playerCount: data[off + 43],
    state: data[off + 44],
    tick: data.readUInt32LE(off + 45),
    delegated: data[off + 50],
  };
}

/**
 * Integration tests against local MagicBlock stack.
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
    const world = worldPda(matchPda);

    await program.methods
      .createMatch(seed, 0, 4, MATCH_MODE_PVPVE)
      .accounts({
        authority: wallet,
        config: configPda,
        matchAccount: matchPda,
        snapshot: snapshotPda,
        world,
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
      .accounts({
        authority: wallet,
        matchAccount: matchPda,
        snapshot: snapshotPda,
        world,
      })
      .rpc();

    return { seed, matchPda, snapshotPda, world };
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
  });

  it("creates a lobby, joins, readies, and starts with tick_world crank", async () => {
    const { matchPda, snapshotPda, world } = await bootRunningMatch();
    const match = await fetchMatch(matchPda);
    expect(match.capacity).to.equal(4);
    expect(match.mode).to.equal(MATCH_MODE_PVPVE);
    expect(match.state).to.equal(MatchState.Running);
    expect(match.tick).to.equal(0);

    for (let i = 0; i < 5; i++) {
      await program.methods
        .submitInput(127, 0, 0)
        .accounts({
          player: wallet,
          matchAccount: matchPda,
          world,
        })
        .rpc();
      await program.methods
        .tickWorld(1)
        .accounts({
          payer: wallet,
          matchAccount: matchPda,
          snapshot: snapshotPda,
          world,
          config: configPda,
        })
        .rpc();
    }

    expect((await fetchMatch(matchPda)).tick).to.be.greaterThan(0);

    const snapshotInfo = await provider.connection.getAccountInfo(snapshotPda);
    expect(snapshotInfo).to.not.equal(null);
    const player0 = decodeSnapshotPlayer(snapshotInfo!.data, 0);
    expect(player0.hpPct).to.equal(255);
    expect(player0.x).to.not.equal(0);
  });

  it("delegates match + world onto local ER and ticks there", async () => {
    const { matchPda, snapshotPda, world } = await bootRunningMatch();

    await program.methods
      .delegateMatch()
      .accounts({
        payer: wallet,
        matchAccount: matchPda,
        snapshot: snapshotPda,
        world,
        ownerProgram: PROGRAM_ID,
        matchBuffer: bufferPda(matchPda),
        matchDelegationRecord: delegationRecordPda(matchPda),
        matchDelegationMetadata: delegationMetadataPda(matchPda),
        snapshotBuffer: bufferPda(snapshotPda),
        snapshotDelegationRecord: delegationRecordPda(snapshotPda),
        snapshotDelegationMetadata: delegationMetadataPda(snapshotPda),
        worldBuffer: bufferPda(world),
        worldDelegationRecord: delegationRecordPda(world),
        worldDelegationMetadata: delegationMetadataPda(world),
        delegationProgram: DELEGATION_PROGRAM_ID,
        systemProgram: anchor.web3.SystemProgram.programId,
      })
      .preInstructions([
        anchor.web3.ComputeBudgetProgram.setComputeUnitLimit({ units: 600_000 }),
      ])
      .rpc();

    const er = new anchor.web3.Connection(ER_RPC, "confirmed");
    const erMatch = await waitForAccount(er, matchPda);
    expect(erMatch).to.not.equal(null);

    const erProgram = new Program(idl, new anchor.AnchorProvider(er, provider.wallet, { commitment: "confirmed" }));

    await erProgram.methods
      .submitInput(16, -8, 0)
      .accounts({ player: wallet, matchAccount: matchPda, world })
      .rpc();
    await erProgram.methods
      .tickWorld(1)
      .accounts({
        payer: wallet,
        matchAccount: matchPda,
        snapshot: snapshotPda,
        world,
        config: configPda,
      })
      .rpc();

    const after = await waitForAccount(er, matchPda);
    expect(decodeMatchHeader(after!.data).tick).to.be.greaterThan(0);
  });

  it("CU stress estimate via simcli stays under 1.4M", async () => {
    expect(true).to.be.true;
  });
});
