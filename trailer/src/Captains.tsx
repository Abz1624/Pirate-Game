import {
  AbsoluteFill,
  Img,
  interpolate,
  Sequence,
  spring,
  staticFile,
  useCurrentFrame,
  useVideoConfig,
  Easing,
} from "remotion";
import { body, C, serif } from "./theme";

// Captain data: abilities from roguepirates.fun, roles and lines from the Narrative Bible.
// `h` is the source art height on the site, which keeps every Captain at the same scale.
export const CAPTAINS = [
  { key: "rook", name: "Rook", role: "The Generalist", h: 1054, tint: "#b8322a", ability: "Exactly As Planned", lead: "Your ship is sinking.", line: "Why are we leaving?" },
  { key: "richie", name: "Richie", role: "The Smuggler", h: 1047, tint: "#6b3fa0", ability: "Golden Hoard", lead: "", line: "The ship looks fine." },
  { key: "hank", name: "Hank", role: "The Tank", h: 1041, tint: "#2f7a4a", ability: "Stand Your Ground", lead: "Roxie: Dodge!", line: "No." },
  { key: "roxie", name: "Roxie", role: "The Stormchaser", h: 1080, tint: "#1f7f9a", ability: "Close Call", lead: "", line: "Again." },
  { key: "sparky", name: "Sparky", role: "The Powderrunner", h: 981, tint: "#c8641c", ability: "Powder Trail", lead: "", line: "I have an idea." },
  { key: "ember", name: "Ember", role: "The Firekeeper", h: 1077, tint: "#c23a1c", ability: "Slow Burn", lead: "", line: "Let it spread." },
] as const;

type Captain = (typeof CAPTAINS)[number];

export const CARD_FRAMES = 50;
export const LINEUP_FRAMES = 70;
export const CAPTAINS_FRAMES = CAPTAINS.length * CARD_FRAMES + LINEUP_FRAMES;

const WaveRing: React.FC<{ tint: string; t: number }> = ({ tint, t }) => (
  <svg width={900} height={260} viewBox="0 0 900 260" style={{ position: "absolute", left: 120, top: 830 }}>
    <defs>
      <radialGradient id="foam" cx="50%" cy="50%" r="50%">
        <stop offset="62%" stopColor="#ffffff" stopOpacity={0} />
        <stop offset="80%" stopColor="#bfefff" stopOpacity={0.85} />
        <stop offset="92%" stopColor={tint} stopOpacity={0.6} />
        <stop offset="100%" stopColor="#000" stopOpacity={0} />
      </radialGradient>
    </defs>
    <ellipse cx={450} cy={130} rx={380 + t * 40} ry={88 + t * 10} fill="url(#foam)" opacity={0.9} />
  </svg>
);

const Card: React.FC<{ cap: Captain; index: number }> = ({ cap, index }) => {
  const frame = useCurrentFrame();
  const { fps } = useVideoConfig();

  // Diagonal wipe in over 7 frames.
  const wipe = interpolate(frame, [0, 7], [0, 100], { extrapolateRight: "clamp", easing: Easing.out(Easing.cubic) });
  const clip = `polygon(0 0, ${wipe * 1.3}% 0, ${wipe * 1.3 - 30}% 100%, 0 100%)`;

  const enter = spring({ frame: frame - 2, fps, config: { damping: 16, stiffness: 140 } });
  const drift = interpolate(frame, [0, CARD_FRAMES], [0, 1]);
  const fromLeft = index % 2 === 0;

  const charH = cap.h * 0.92;
  const charX = interpolate(enter, [0, 1], [fromLeft ? -260 : 260, 0]) + drift * 18;
  const textIn = spring({ frame: frame - 6, fps, config: { damping: 18, stiffness: 160 } });
  const lineIn = spring({ frame: frame - 16, fps, config: { damping: 14, stiffness: 180 } });

  return (
    <AbsoluteFill style={{ clipPath: index === 0 ? undefined : clip }}>
      <AbsoluteFill
        style={{
          background: `radial-gradient(85% 95% at 32% 45%, ${cap.tint} 0%, #0b2f3a 52%, ${C.night} 100%)`,
        }}
      />
      {/* light shafts */}
      <AbsoluteFill
        style={{
          background:
            "repeating-linear-gradient(105deg, rgba(255,255,255,0.05) 0 60px, rgba(255,255,255,0) 60px 180px)",
          transform: `translateX(${-60 + drift * 60}px)`,
        }}
      />
      {/* emblem behind the Captain */}
      <Img
        src={staticFile(`web/captains/${cap.key}-emblem.webp`)}
        style={{
          position: "absolute",
          left: 250,
          top: 120,
          width: 640,
          opacity: 0.16,
          transform: `rotate(${-8 + drift * 10}deg) scale(${1 + drift * 0.06})`,
          filter: "brightness(3)",
        }}
      />
      <WaveRing tint={cap.tint} t={drift} />
      <Img
        src={staticFile(`web/captains/${cap.key}-character.webp`)}
        style={{
          position: "absolute",
          height: charH,
          left: 580 - (charH * 0.78) / 2 + charX,
          bottom: 60,
          opacity: enter,
          filter: "drop-shadow(0 24px 30px rgba(0,0,0,0.55))",
        }}
      />
      {/* text block */}
      <div
        style={{
          position: "absolute",
          left: 1060,
          top: 300,
          width: 780,
          display: "flex",
          flexDirection: "column",
          gap: 14,
          transform: `translateX(${(1 - textIn) * 80}px)`,
          opacity: textIn,
        }}
      >
        <div style={{ fontFamily: serif, fontSize: 30, letterSpacing: "0.32em", color: C.ink, opacity: 0.8, textTransform: "uppercase" }}>
          {cap.role}
        </div>
        <div style={{ display: "flex", alignItems: "center", gap: 26 }}>
          <Img src={staticFile(`web/captains/${cap.key}-emblem.webp`)} style={{ width: 120 }} />
          <div
            style={{
              fontFamily: serif,
              fontWeight: 800,
              fontSize: 168,
              lineHeight: 0.9,
              background: `linear-gradient(180deg, ${C.goldHi} 0%, ${C.gold} 55%, #c98a12 100%)`,
              WebkitBackgroundClip: "text",
              color: "transparent",
              filter: "drop-shadow(0 6px 0 #5b2a07) drop-shadow(0 12px 20px rgba(0,0,0,.5))",
            }}
          >
            {cap.name.toUpperCase()}
          </div>
        </div>
        <div style={{ fontFamily: serif, fontSize: 34, color: C.gold, letterSpacing: "0.08em", marginTop: 6 }}>
          {cap.ability.toUpperCase()}
        </div>
        <div
          style={{
            marginTop: 26,
            fontFamily: body,
            fontStyle: "italic",
            color: C.ink,
            transform: `scale(${0.85 + lineIn * 0.15})`,
            transformOrigin: "left center",
            opacity: lineIn,
          }}
        >
          {cap.lead ? <div style={{ fontSize: 34, opacity: 0.75 }}>{cap.lead}</div> : null}
          <div style={{ fontSize: 64, textShadow: "0 4px 18px rgba(0,0,0,.6)" }}>“{cap.line}”</div>
        </div>
      </div>
    </AbsoluteFill>
  );
};

const Lineup: React.FC = () => {
  const frame = useCurrentFrame();
  const { fps } = useVideoConfig();
  const title = spring({ frame: frame - 18, fps, config: { damping: 14 } });
  const zoom = interpolate(frame, [0, LINEUP_FRAMES], [1.04, 1]);
  return (
    <AbsoluteFill style={{ background: `radial-gradient(70% 80% at 50% 40%, #12505e 0%, ${C.night} 100%)` }}>
      <AbsoluteFill style={{ transform: `scale(${zoom})`, justifyContent: "center", alignItems: "center", gap: 60 }}>
        <div style={{ display: "flex", gap: 34 }}>
          {CAPTAINS.map((cap, i) => {
            const s = spring({ frame: frame - i * 3, fps, config: { damping: 12, stiffness: 170 } });
            return (
              <div key={cap.key} style={{ position: "relative", width: 250, height: 250, transform: `translateY(${(1 - s) * 120}px) scale(${s})` }}>
                <Img src={staticFile(`web/captains/${cap.key}-portrait.webp`)} style={{ position: "absolute", inset: 18, width: 214, height: 214, borderRadius: 14 }} />
                <Img src={staticFile("web/captains/frame-gold.webp")} style={{ position: "absolute", inset: 0, width: 250, height: 250 }} />
                <div style={{ position: "absolute", bottom: -58, width: "100%", textAlign: "center", fontFamily: serif, fontWeight: 700, fontSize: 34, color: C.ink }}>
                  {cap.name}
                </div>
              </div>
            );
          })}
        </div>
        <div style={{ marginTop: 60, fontFamily: serif, fontWeight: 800, fontSize: 92, color: C.gold, opacity: title, transform: `scale(${0.9 + title * 0.1})`, textShadow: "0 6px 0 #5b2a07, 0 14px 30px rgba(0,0,0,.6)" }}>
          PICK YOUR CAPTAIN
        </div>
      </AbsoluteFill>
    </AbsoluteFill>
  );
};

export const Captains: React.FC = () => (
  <AbsoluteFill style={{ backgroundColor: C.night }}>
    {CAPTAINS.map((cap, i) => (
      <Sequence key={cap.key} from={i * CARD_FRAMES} durationInFrames={CARD_FRAMES + 8}>
        <Card cap={cap} index={i} />
      </Sequence>
    ))}
    <Sequence from={CAPTAINS.length * CARD_FRAMES} durationInFrames={LINEUP_FRAMES}>
      <Lineup />
    </Sequence>
  </AbsoluteFill>
);
