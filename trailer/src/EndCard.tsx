import { AbsoluteFill, Img, interpolate, spring, staticFile, useCurrentFrame, useVideoConfig } from "remotion";
import { body, C, serif } from "./theme";

// Logo, the tagline, the call to action. Chippy sneaks in and pockets a coin.
export const END_FRAMES = 300;

export const EndCard: React.FC = () => {
  const frame = useCurrentFrame();
  const { fps } = useVideoConfig();
  const logo = spring({ frame, fps, config: { damping: 11, stiffness: 120 } });
  const tag = spring({ frame: frame - 34, fps, config: { damping: 16 } });
  const cta = spring({ frame: frame - 60, fps, config: { damping: 14 } });
  const chippy = spring({ frame: frame - 120, fps, config: { damping: 13, stiffness: 160 } });
  const bob = Math.sin(frame / 18) * 6;
  const octo = interpolate(frame, [0, 40], [180, 0], { extrapolateRight: "clamp" });
  return (
    <AbsoluteFill style={{ background: `radial-gradient(70% 80% at 50% 38%, #1d6b7a 0%, #0b3442 55%, ${C.night} 100%)` }}>
      <Img src={staticFile("web/octo-left.webp")} style={{ position: "absolute", left: -60 - octo, bottom: -40, height: 640, opacity: 0.95 }} />
      <Img src={staticFile("web/octo-right.webp")} style={{ position: "absolute", right: -60 - octo, bottom: -40, height: 640, opacity: 0.95 }} />
      <AbsoluteFill style={{ alignItems: "center", paddingTop: 110 }}>
        <Img src={staticFile("art/logo.webp")} style={{ width: 900, transform: `scale(${0.6 + logo * 0.4}) translateY(${bob}px)`, opacity: Math.min(1, logo * 1.5), filter: "drop-shadow(0 20px 40px rgba(0,0,0,.5))" }} />
        <div style={{ marginTop: 30, fontFamily: body, fontStyle: "italic", fontSize: 60, color: C.ink, opacity: tag, textShadow: "0 4px 16px rgba(0,0,0,.6)" }}>
          The sea keeps receipts.
        </div>
        <div style={{ marginTop: 34, display: "flex", flexDirection: "column", alignItems: "center", gap: 10, opacity: cta, transform: `translateY(${(1 - cta) * 30}px)` }}>
          <div style={{ fontFamily: serif, fontWeight: 700, fontSize: 34, color: C.ink, letterSpacing: "0.22em" }}>PLAY FREE IN YOUR BROWSER</div>
          <div style={{ fontFamily: serif, fontWeight: 900, fontSize: 84, color: C.gold, textShadow: "0 5px 0 #5b2a07, 0 12px 30px rgba(0,0,0,.6)" }}>roguepirates.fun</div>
        </div>
      </AbsoluteFill>
      <Img src={staticFile("art/chippy.webp")} style={{ position: "absolute", left: 330, bottom: interpolate(chippy, [0, 1], [-420, -70]), height: 360, transform: `scaleX(-1) rotate(${(1 - chippy) * -12}deg)` }} />
    </AbsoluteFill>
  );
};
