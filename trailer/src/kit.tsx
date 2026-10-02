import { AbsoluteFill, Img, interpolate, spring, staticFile, useCurrentFrame, useVideoConfig } from "remotion";
import { body, C, serif } from "./theme";

// Shared look for the planned-feature interface shots: dark sea panels with gold trim, Cinzel headings.

export const useSpring = (delay: number, damping = 15, stiffness = 160) => {
  const frame = useCurrentFrame();
  const { fps } = useVideoConfig();
  return spring({ frame: frame - delay, fps, config: { damping, stiffness } });
};

export const SeaBackdrop: React.FC<{ image?: string; tint?: string; blur?: number }> = ({ image, tint = "rgba(7,24,32,0.55)", blur = 10 }) => {
  const frame = useCurrentFrame();
  return (
    <AbsoluteFill style={{ backgroundColor: C.night }}>
      {image ? (
        <Img src={staticFile(image)} style={{ width: "100%", height: "100%", objectFit: "cover", filter: `blur(${blur}px) saturate(1.1)`, transform: `scale(${1.08 + frame * 0.0004})` }} />
      ) : (
        <AbsoluteFill style={{ background: `radial-gradient(80% 90% at 50% 40%, #155a68 0%, ${C.night} 100%)` }} />
      )}
      <AbsoluteFill style={{ background: tint }} />
    </AbsoluteFill>
  );
};

export const Panel: React.FC<{ style?: React.CSSProperties; children: React.ReactNode }> = ({ style, children }) => (
  <div
    style={{
      background: "linear-gradient(180deg, rgba(16,52,66,0.96) 0%, rgba(9,30,40,0.96) 100%)",
      border: `4px solid ${C.gold}`,
      borderRadius: 28,
      boxShadow: "0 0 0 3px #5b2a07, 0 30px 60px rgba(0,0,0,.55), inset 0 0 40px rgba(0,0,0,.35)",
      color: C.ink,
      ...style,
    }}
  >
    {children}
  </div>
);

export const Heading: React.FC<{ children: React.ReactNode; size?: number; style?: React.CSSProperties }> = ({ children, size = 64, style }) => (
  <div style={{ fontFamily: serif, fontWeight: 800, fontSize: size, color: C.gold, letterSpacing: "0.04em", textShadow: "0 4px 0 #5b2a07, 0 10px 24px rgba(0,0,0,.5)", ...style }}>
    {children}
  </div>
);

/** The big caption that names each beat ("Raid other captains."). */
export const Caption: React.FC<{ text: string; delay?: number; y?: number; size?: number }> = ({ text, delay = 0, y = 900, size = 72 }) => {
  const s = useSpring(delay, 14, 140);
  return (
    <div style={{ position: "absolute", left: 0, right: 0, top: y, textAlign: "center", fontFamily: serif, fontWeight: 800, fontSize: size, color: C.ink, opacity: s, transform: `translateY(${(1 - s) * 30}px)`, textShadow: "0 4px 0 rgba(0,0,0,.55), 0 10px 30px rgba(0,0,0,.7)" }}>
      {text}
    </div>
  );
};

export const Chip: React.FC<{ color: string; children: React.ReactNode; style?: React.CSSProperties }> = ({ color, children, style }) => (
  <div style={{ display: "inline-flex", alignItems: "center", gap: 12, padding: "12px 20px", borderRadius: 14, background: "rgba(0,0,0,.35)", borderLeft: `10px solid ${color}`, fontFamily: body, fontSize: 32, color: C.ink, whiteSpace: "nowrap", ...style }}>
    {children}
  </div>
);

export const Avatar: React.FC<{ name: string; color: string; size?: number; image?: string }> = ({ name, color, size = 96, image }) => (
  <div style={{ width: size, height: size, borderRadius: size / 2, border: `4px solid ${C.gold}`, background: color, overflow: "hidden", display: "grid", placeItems: "center", fontFamily: serif, fontWeight: 800, fontSize: size * 0.42, color: "#fff", flex: "none" }}>
    {image ? <Img src={staticFile(image)} style={{ width: "100%", height: "100%", objectFit: "cover" }} /> : name.slice(0, 1)}
  </div>
);

/** Counts a number up between two frames. */
export const useCount = (from: number, to: number, start: number, end: number) => {
  const frame = useCurrentFrame();
  return Math.round(interpolate(frame, [start, end], [from, to], { extrapolateLeft: "clamp", extrapolateRight: "clamp" }));
};

export const fmt = (n: number) => n.toLocaleString("en-US");
