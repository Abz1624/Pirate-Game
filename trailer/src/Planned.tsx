import { AbsoluteFill, Img, interpolate, Easing, staticFile, useCurrentFrame } from "remotion";
import { body, C, serif } from "./theme";
import { Avatar, Caption, Chip, fmt, Heading, Panel, SeaBackdrop, useCount, useSpring } from "./kit";

// Interface shots for the planned features (V5 M14-M27). Names of other players are made up for the trailer.

export const RIVALS = [
  { name: "Grimbeard", color: "#7a3b1c" },
  { name: "Salty Jane", color: "#1f6f8b" },
  { name: "Ironjaw", color: "#5a5a6e" },
  { name: "NeptuneNed", color: "#2f7a4a" },
  { name: "Barnaby Bones", color: "#6b3fa0" },
];

/* ---------------------------------------------------------------- Raid schedule (M14) */

const WAVES = [
  { label: "Sharks ×12", color: "#4fb3c8", at: 0.35, row: 0, delay: 18 },
  { label: "Rammers ×6", color: "#e0893a", at: 1.4, row: 1, delay: 40 },
  { label: "Kraken Spawn ×4", color: "#c04a8a", at: 2.5, row: 0, delay: 62 },
  { label: "Barnacle King", color: "#e04a2c", at: 4.0, row: 1, delay: 84 },
];
export const RAID_SCHEDULE_FRAMES = 200;

export const RaidSchedule: React.FC = () => {
  const frame = useCurrentFrame();
  const panel = useSpring(0, 16);
  const trackX = 120, trackW = 1360;
  return (
    <AbsoluteFill>
      <SeaBackdrop image="art/home-island.webp" />
      <Panel style={{ position: "absolute", left: 180, top: 200, width: 1560, height: 480, padding: "40px 60px", transform: `scale(${0.9 + panel * 0.1})`, opacity: panel }}>
        <Heading size={56}>RAID SCHEDULE</Heading>
        <div style={{ fontFamily: body, fontSize: 30, opacity: 0.8, marginTop: 6 }}>What raiders face on your island, minute by minute</div>
        <div style={{ position: "absolute", left: trackX, top: 220, width: trackW }}>
          {/* ruler */}
          <div style={{ height: 4, background: C.gold, opacity: 0.7 }} />
          {[0, 1, 2, 3, 4, 5].map((m) => (
            <div key={m} style={{ position: "absolute", left: (m / 5) * trackW - 30, top: 14, width: 60, textAlign: "center", fontFamily: serif, fontSize: 26, color: C.gold }}>{`${m}:00`}</div>
          ))}
          {[0, 1].map((r) => (
            <div key={r} style={{ position: "absolute", left: 0, top: 80 + r * 110, width: trackW, height: 86, borderRadius: 16, background: "rgba(255,255,255,.06)", border: "2px dashed rgba(255,255,255,.18)" }} />
          ))}
          {WAVES.map((w) => {
            const s = interpolate(frame, [w.delay, w.delay + 16], [0, 1], { extrapolateLeft: "clamp", extrapolateRight: "clamp", easing: Easing.out(Easing.back(1.4)) });
            const x = (w.at / 5) * trackW;
            return (
              <div key={w.label} style={{ position: "absolute", left: x, top: 88 + w.row * 110 - (1 - s) * 220, opacity: Math.min(1, s * 2), transform: `rotate(${(1 - s) * -6}deg)` }}>
                <Chip color={w.color}>{w.label}</Chip>
              </div>
            );
          })}
          {/* playhead */}
          <div style={{ position: "absolute", left: interpolate(frame, [100, 200], [0, trackW * 0.55], { extrapolateLeft: "clamp", extrapolateRight: "clamp" }), top: -10, width: 4, height: 310, background: "#fff", opacity: frame > 100 ? 0.8 : 0, boxShadow: "0 0 18px #fff" }} />
        </div>
      </Panel>
      <Caption text="Write the waves they will face." delay={20} y={760} />
    </AbsoluteFill>
  );
};

/* ---------------------------------------------------------------- Raided notification and revenge (M15, M19) */

export const RAID_ALERT_FRAMES = 200;

export const RaidAlert: React.FC = () => {
  const frame = useCurrentFrame();
  const phone = useSpring(0, 16, 120);
  const note = useSpring(22, 13, 190);
  const revenge = useSpring(70, 10, 200);
  const pulse = 1 + Math.max(0, Math.sin((frame - 90) / 6)) * 0.05 * (frame > 90 ? 1 : 0);
  const shake = frame >= 22 && frame < 34 ? Math.sin(frame * 2.5) * 6 : 0;
  const gold = useCount(0, 1240, 30, 70);
  return (
    <AbsoluteFill>
      <SeaBackdrop image="art/home-island.webp" blur={14} tint="rgba(7,24,32,0.6)" />
      <div style={{ position: "absolute", left: 670, top: 40, width: 580, height: 1000, borderRadius: 70, background: "#0b0f14", border: "14px solid #1c232c", boxShadow: "0 40px 80px rgba(0,0,0,.6)", overflow: "hidden", transform: `translateY(${(1 - phone) * 600}px) rotate(${shake * 0.2}deg)` }}>
        <Img src={staticFile("art/home-island.webp")} style={{ position: "absolute", width: "100%", height: "100%", objectFit: "cover", filter: "brightness(.55)" }} />
        <div style={{ position: "absolute", top: 90, width: "100%", textAlign: "center", fontFamily: body, color: "#fff" }}>
          <div style={{ fontSize: 120, fontWeight: 400, letterSpacing: "-0.02em" }}>3:12</div>
          <div style={{ fontSize: 30, opacity: 0.85 }}>Saturday, 4 October</div>
        </div>
        <div style={{ position: "absolute", left: 24, right: 24, top: 330, padding: 24, borderRadius: 30, background: "rgba(245,240,230,.94)", color: "#1b1b1b", transform: `translateY(${(1 - note) * -260}px) translateX(${shake}px)`, opacity: note, display: "flex", gap: 18 }}>
          <Img src={staticFile("art/logo.webp")} style={{ width: 70, height: 70, objectFit: "contain" }} />
          <div style={{ fontFamily: body, fontSize: 28, lineHeight: 1.25 }}>
            <div style={{ fontWeight: 700, display: "flex", justifyContent: "space-between" }}><span>Rogue Pirates</span><span style={{ opacity: 0.5, fontWeight: 400 }}>now</span></div>
            <div style={{ fontWeight: 700, color: "#9c1d12" }}>You were raided!</div>
            <div>Grimbeard took {fmt(gold)} Gold from your treasury.</div>
          </div>
        </div>
        <div style={{ position: "absolute", left: 50, right: 50, top: 560, display: "flex", flexDirection: "column", alignItems: "center", gap: 18, opacity: revenge, transform: `scale(${(0.6 + revenge * 0.4) * pulse})` }}>
          <Avatar name="Grimbeard" color="#7a3b1c" size={150} />
          <div style={{ fontFamily: serif, fontSize: 30, color: "#fff" }}>Grimbeard · Island level 14</div>
          <div style={{ marginTop: 10, padding: "22px 70px", borderRadius: 22, background: `linear-gradient(180deg, ${C.goldHi}, ${C.gold})`, color: C.brown, fontFamily: serif, fontWeight: 900, fontSize: 56, boxShadow: "0 8px 0 #8a5a0a, 0 16px 30px rgba(0,0,0,.5)" }}>
            REVENGE
          </div>
        </div>
      </div>
    </AbsoluteFill>
  );
};

/* ---------------------------------------------------------------- Weekly leaderboard (M18), wager pot (M24, web3 cut) */

const BOARD = [
  { name: "Salty Jane", kills: 2318 },
  { name: "Grimbeard", kills: 2207 },
  { name: "You", kills: 2154, you: true },
  { name: "Ironjaw", kills: 1990 },
  { name: "Barnaby Bones", kills: 1873 },
];
export const LEADERBOARD_FRAMES = 210;

export const Leaderboard: React.FC<{ web3?: boolean }> = ({ web3 = false }) => {
  const frame = useCurrentFrame();
  const panel = useSpring(0, 16);
  const pot = useSpring(110, 12, 180);
  return (
    <AbsoluteFill>
      <SeaBackdrop image="art/blue-sea.webp" blur={6} />
      <Panel style={{ position: "absolute", left: web3 ? 150 : 460, top: 110, width: 1000, padding: "36px 50px", opacity: panel, transform: `translateY(${(1 - panel) * 60}px)` }}>
        <Heading size={54}>WEEKLY BOUNTY BOARD</Heading>
        <div style={{ fontFamily: body, fontSize: 30, opacity: 0.85, marginTop: 4 }}>Most kills in a single run · resets in 2d 14h 09m</div>
        <div style={{ marginTop: 26, display: "flex", flexDirection: "column", gap: 14 }}>
          {BOARD.map((r, i) => {
            const s = useSpring(14 + i * 9, 15, 190);
            const medal = ["#ffd24a", "#d6dde3", "#d98a4a"][i];
            const kills = useCount(0, r.kills, 18 + i * 9, 70 + i * 9);
            return (
              <div key={r.name} style={{ display: "flex", alignItems: "center", gap: 22, padding: "14px 22px", borderRadius: 18, background: r.you ? "rgba(255,197,61,.18)" : "rgba(0,0,0,.25)", border: r.you ? `3px solid ${C.gold}` : "3px solid transparent", opacity: s, transform: `translateX(${(1 - s) * -80}px)` }}>
                <div style={{ width: 60, fontFamily: serif, fontWeight: 900, fontSize: 44, color: medal ?? C.ink }}>{i + 1}</div>
                <Avatar name={r.name} color={RIVALS.find((x) => x.name === r.name)?.color ?? "#b8322a"} size={70} image={r.you ? "web/captains/rook-portrait.webp" : undefined} />
                <div style={{ flex: 1, fontFamily: serif, fontWeight: 700, fontSize: 38 }}>{r.name}</div>
                <div style={{ fontFamily: serif, fontWeight: 800, fontSize: 40, color: C.gold }}>{fmt(kills)}</div>
              </div>
            );
          })}
        </div>
      </Panel>
      {web3 ? (
        <Panel style={{ position: "absolute", right: 150, top: 230, width: 560, padding: "40px 44px", textAlign: "center", opacity: pot, transform: `scale(${0.7 + pot * 0.3})` }}>
          <Heading size={46}>WAGER POT</Heading>
          <div style={{ fontFamily: body, fontSize: 30, opacity: 0.85, marginTop: 8 }}>Hunt · 4 captains · winner takes all</div>
          <Img src={staticFile("art/chest.webp")} style={{ width: 360, margin: "18px auto 0", borderRadius: 20 }} />
          <div style={{ fontFamily: serif, fontWeight: 900, fontSize: 64, color: C.goldHi, marginTop: 14 }}>4 × 50 SKR</div>
          <div style={{ fontFamily: body, fontSize: 26, opacity: 0.75 }}>Also in SOL, ORE or RUM · 5% house cut</div>
        </Panel>
      ) : null}
      <Caption text={web3 ? "Stake it. Win it all." : "Top the weekly board."} delay={40} y={940} size={64} />
    </AbsoluteFill>
  );
};

/* ---------------------------------------------------------------- Sail designer (M25) */

export const SAIL_DESIGNER_FRAMES = 240;

export const SailDesigner: React.FC = () => {
  const frame = useCurrentFrame();
  const panel = useSpring(0, 16);
  const stroke = (start: number, end: number) => interpolate(frame, [start, end], [1, 0], { extrapolateLeft: "clamp", extrapolateRight: "clamp" });
  const sticker = useSpring(110, 9, 220);
  const swap = interpolate(frame, [168, 186], [0, 1], { extrapolateLeft: "clamp", extrapolateRight: "clamp" });
  const tools = ["Brush", "Fill", "Stickers", "Mirror", "Layers"];
  const palette = ["#111827", "#f5efe0", "#b8322a", "#ffc53d", "#1f7f9a", "#2f7a4a"];
  return (
    <AbsoluteFill>
      <SeaBackdrop image="art/blue-sea.webp" blur={12} />
      <Panel style={{ position: "absolute", left: 120, top: 90, width: 1680, height: 780, padding: 36, opacity: panel }}>
        <Heading size={50}>SAIL DESIGNER</Heading>
        {/* tools */}
        <div style={{ position: "absolute", left: 36, top: 140, display: "flex", flexDirection: "column", gap: 14 }}>
          {tools.map((t, i) => (
            <div key={t} style={{ width: 200, padding: "14px 18px", borderRadius: 14, fontFamily: serif, fontSize: 28, background: (i === 0 && frame < 110) || (i === 2 && frame >= 110 && frame < 150) || (i === 3 && frame >= 150) ? C.gold : "rgba(0,0,0,.3)", color: (i === 0 && frame < 110) || (i === 2 && frame >= 110 && frame < 150) || (i === 3 && frame >= 150) ? C.brown : C.ink }}>{t}</div>
          ))}
          <div style={{ display: "flex", flexWrap: "wrap", gap: 10, width: 200, marginTop: 10 }}>
            {palette.map((c) => <div key={c} style={{ width: 56, height: 56, borderRadius: 12, background: c, border: "3px solid rgba(255,255,255,.4)" }} />)}
          </div>
        </div>
        {/* canvas: the sail */}
        <div style={{ position: "absolute", left: 330, top: 120, width: 620, height: 620, borderRadius: 26, background: "#f2e8d2", boxShadow: "inset 0 0 50px rgba(120,90,40,.35)", overflow: "hidden" }}>
          <svg width={620} height={620} viewBox="0 0 620 620" style={{ position: "absolute", inset: 0 }}>
            <path d="M0 470 C 140 420, 260 520, 620 450 L 620 620 L 0 620 Z" fill="#b8322a" opacity={1 - stroke(10, 40)} />
            <path d="M40 120 C 200 60, 420 60, 580 120" stroke="#111827" strokeWidth={34} fill="none" strokeLinecap="round" pathLength={1} strokeDasharray={1} strokeDashoffset={stroke(30, 60)} />
            <path d="M40 180 C 200 120, 420 120, 580 180" stroke="#ffc53d" strokeWidth={16} fill="none" strokeLinecap="round" pathLength={1} strokeDasharray={1} strokeDashoffset={stroke(55, 85)} />
            {/* mirror guide */}
            <line x1={310} y1={0} x2={310} y2={620} stroke="#1f7f9a" strokeWidth={4} strokeDasharray="14 12" opacity={frame >= 150 ? 0.8 : 0} />
          </svg>
          <Img src={staticFile("web/captains/rook-emblem.webp")} style={{ position: "absolute", left: 160, top: 210, width: 300, filter: "brightness(0) saturate(100%)", opacity: sticker, transform: `scale(${1.6 - sticker * 0.6}) rotate(${(1 - sticker) * 30}deg)` }} />
        </div>
        <div style={{ position: "absolute", left: 980, top: 120, fontFamily: serif, fontSize: 30, color: C.gold }}>LIVE PREVIEW</div>
        {/* preview: a captain sail swaps to the painted look */}
        <div style={{ position: "absolute", left: 980, top: 170, width: 640, height: 560, borderRadius: 26, overflow: "hidden", border: "3px solid rgba(255,255,255,.2)" }}>
          <Img src={staticFile("art/blue-sea.webp")} style={{ position: "absolute", width: "100%", height: "100%", objectFit: "cover" }} />
          <Img src={staticFile("art/ship.webp")} style={{ position: "absolute", left: 70, top: 40, width: 500, filter: `hue-rotate(${swap * 0}deg)` }} />
          <div style={{ position: "absolute", left: 310, top: 110, width: 150, height: 150, borderRadius: 8, background: "#f2e8d2", opacity: swap, overflow: "hidden", transform: "skewY(-4deg)" }}>
            <div style={{ position: "absolute", bottom: 0, width: "100%", height: "26%", background: "#b8322a" }} />
            <Img src={staticFile("web/captains/rook-emblem.webp")} style={{ position: "absolute", left: 35, top: 40, width: 80, filter: "brightness(0)" }} />
          </div>
        </div>
      </Panel>
      <Caption text="Paint your sails." delay={150} y={920} />
    </AbsoluteFill>
  );
};

/* ---------------------------------------------------------------- Marketplace (M26, M26b) */

export const MARKET_FRAMES = 200;

const LISTINGS = [
  { kind: "Sail", title: "Storm Compass", art: "sails/roxie.webp", rum: 4200, sol: "0.8 SOL", by: "Salty Jane" },
  { kind: "Island plot", title: "Smuggler's Cove", art: "art/home-island.webp", rum: 18500, sol: "3.2 SOL", by: "Grimbeard" },
  { kind: "Sail", title: "Ember's Blaze", art: "sails/ember.webp", rum: 3600, sol: "0.6 SOL", by: "Ironjaw" },
];

export const Market: React.FC<{ web3?: boolean }> = ({ web3 = false }) => {
  const frame = useCurrentFrame();
  const sold = useSpring(120, 10, 220);
  return (
    <AbsoluteFill>
      <SeaBackdrop image="art/home-island.webp" blur={16} />
      <div style={{ position: "absolute", top: 70, width: "100%", textAlign: "center" }}>
        <Heading size={64}>THE MARKET</Heading>
      </div>
      <div style={{ position: "absolute", top: 200, left: 0, right: 0, display: "flex", justifyContent: "center", gap: 50 }}>
        {LISTINGS.map((l, i) => {
          const s = useSpring(8 + i * 10, 13, 170);
          const hover = i === 1 ? interpolate(frame, [80, 100], [0, 1], { extrapolateLeft: "clamp", extrapolateRight: "clamp" }) : 0;
          return (
            <Panel key={l.title} style={{ width: 470, padding: 24, opacity: s, transform: `translateY(${(1 - s) * 120 - hover * 24}px) scale(${1 + hover * 0.05})` }}>
              <div style={{ position: "relative", height: 330, borderRadius: 18, overflow: "hidden", background: "#0b2a33" }}>
                <Img src={staticFile(l.art)} style={{ width: "100%", height: "100%", objectFit: "cover" }} />
                {i === 1 ? (
                  <div style={{ position: "absolute", left: "50%", top: "50%", padding: "10px 34px", border: "8px solid #2e7a4c", color: "#2e7a4c", background: "rgba(255,255,255,.9)", fontFamily: serif, fontWeight: 900, fontSize: 64, borderRadius: 14, opacity: sold, transform: `translate(-50%,-50%) rotate(-10deg) scale(${2 - sold})` }}>SOLD</div>
                ) : null}
              </div>
              <div style={{ fontFamily: body, fontSize: 26, opacity: 0.75, marginTop: 14 }}>{l.kind} · by {l.by}</div>
              <div style={{ fontFamily: serif, fontWeight: 800, fontSize: 40 }}>{l.title}</div>
              <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginTop: 12 }}>
                <div style={{ fontFamily: serif, fontWeight: 900, fontSize: 40, color: C.goldHi }}>{web3 ? l.sol : `${fmt(l.rum)} RUM`}</div>
                <div style={{ padding: "10px 24px", borderRadius: 12, background: C.gold, color: C.brown, fontFamily: serif, fontWeight: 900, fontSize: 28 }}>{web3 ? "BUY NFT" : "BUY"}</div>
              </div>
              {web3 ? <div style={{ fontFamily: body, fontSize: 24, opacity: 0.75, marginTop: 8 }}>5% royalty to the creator · tradable on Solana marketplaces</div> : null}
            </Panel>
          );
        })}
      </div>
      <Caption text={web3 ? "Mint it. Trade it. Creators earn 5%." : "Own it. Trade it."} delay={30} y={930} size={64} />
    </AbsoluteFill>
  );
};

/* ---------------------------------------------------------------- Brand bounty island (M27): Indies on Solana */

export const BOUNTY_FRAMES = 220;
const SPLIT = [25, 18, 13, 10, 8, 7, 6, 5, 4, 4];

export const Bounty: React.FC<{ web3?: boolean }> = ({ web3 = false }) => {
  const frame = useCurrentFrame();
  const banner = useSpring(0, 14, 140);
  const board = useSpring(40, 16, 160);
  return (
    <AbsoluteFill>
      <SeaBackdrop image="art/blue-sea.webp" blur={4} tint="rgba(7,24,32,0.45)" />
      <div style={{ position: "absolute", left: 120, top: 120, width: 820, transform: `translateY(${(1 - banner) * -300}px)`, opacity: banner }}>
        <Panel style={{ padding: "34px 40px", background: "linear-gradient(180deg, rgba(20,12,40,.96), rgba(9,8,20,.96))", borderColor: "#a07cff" }}>
          <div style={{ fontFamily: serif, fontSize: 28, letterSpacing: "0.3em", color: "#c9b5ff" }}>BOUNTY ISLAND</div>
          <div style={{ display: "flex", alignItems: "center", gap: 26, marginTop: 14 }}>
            <Img src={staticFile("brand/indies-mark.svg")} style={{ width: 150, height: 150, background: "#0b0b10", borderRadius: 24 }} />
            <div>
              <div style={{ fontFamily: '"Arial Black", "Helvetica Neue", Arial, sans-serif', fontWeight: 900, fontSize: 66, lineHeight: 0.95, color: "#fff" }}>INDIES<br /><span style={{ fontSize: 40 }}>ON</span> SOLANA</div>
            </div>
          </div>
          <div style={{ fontFamily: body, fontSize: 32, marginTop: 20, color: C.ink }}>Sponsored waters. Their banners, their bounty boss, their limited sail.</div>
          <div style={{ fontFamily: serif, fontWeight: 900, fontSize: 52, color: C.goldHi, marginTop: 14 }}>{web3 ? "Prize pool in SOL" : "Prize pool: 500,000 RUM"}</div>
        </Panel>
      </div>
      <Panel style={{ position: "absolute", right: 120, top: 120, width: 760, padding: "30px 40px", opacity: board, transform: `translateX(${(1 - board) * 200}px)` }}>
        <Heading size={44}>TOP 10 HUNTERS SPLIT IT</Heading>
        <div style={{ display: "flex", flexDirection: "column", gap: 9, marginTop: 20 }}>
          {SPLIT.map((p, i) => {
            const w = interpolate(frame, [60 + i * 5, 100 + i * 5], [0, p / 25], { extrapolateLeft: "clamp", extrapolateRight: "clamp", easing: Easing.out(Easing.cubic) });
            return (
              <div key={i} style={{ display: "flex", alignItems: "center", gap: 16 }}>
                <div style={{ width: 46, fontFamily: serif, fontWeight: 800, fontSize: 28, color: i < 3 ? C.gold : C.ink }}>#{i + 1}</div>
                <div style={{ flex: 1, height: 30, borderRadius: 8, background: "rgba(255,255,255,.08)" }}>
                  <div style={{ width: `${w * 100}%`, height: "100%", borderRadius: 8, background: i < 3 ? `linear-gradient(90deg, ${C.gold}, ${C.goldHi})` : "#a07cff" }} />
                </div>
                <div style={{ width: 70, textAlign: "right", fontFamily: serif, fontWeight: 800, fontSize: 28 }}>{p}%</div>
              </div>
            );
          })}
        </div>
      </Panel>
      <Caption text="Bounty islands. Top hunters split the prize." delay={50} y={940} size={60} />
    </AbsoluteFill>
  );
};
