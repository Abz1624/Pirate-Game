import { AbsoluteFill, Easing, Img, interpolate, spring, staticFile, useCurrentFrame, useVideoConfig } from "remotion";
import { body, C, serif } from "./theme";

// Penny reads the run's ledger on the scroll. Beat: the good news, a pause, the repair bill.
export const LEDGER_FRAMES = 200;

const Row: React.FC<{ label: string; value: string; at: number; color?: string; strike?: boolean }> = ({ label, value, at, color = "#3b1d05", strike }) => {
  const frame = useCurrentFrame();
  const { fps } = useVideoConfig();
  const s = spring({ frame: frame - at, fps, config: { damping: 15, stiffness: 170 } });
  return (
    <div style={{ display: "flex", justifyContent: "space-between", alignItems: "baseline", opacity: s, transform: `translateY(${(1 - s) * 24}px)`, fontFamily: body, fontSize: 54, color }}>
      <span>{label}</span>
      <span style={{ fontFamily: serif, fontWeight: 800, textDecoration: strike ? "line-through" : undefined }}>{value}</span>
    </div>
  );
};

export const Ledger: React.FC = () => {
  const frame = useCurrentFrame();
  const { fps } = useVideoConfig();
  const unroll = interpolate(frame, [0, 18], [0.04, 1], { extrapolateRight: "clamp", easing: Easing.out(Easing.cubic) });
  const penny = spring({ frame: frame - 6, fps, config: { damping: 14 } });
  const stampIn = spring({ frame: frame - 150, fps, config: { damping: 9, stiffness: 220 } });
  // Counting gold up to 2,400.
  const gold = Math.round(interpolate(frame, [26, 60], [0, 2400], { extrapolateLeft: "clamp", extrapolateRight: "clamp" }));
  return (
    <AbsoluteFill style={{ background: `radial-gradient(80% 90% at 40% 40%, #1a5866 0%, ${C.night} 100%)` }}>
      <div style={{ position: "absolute", left: 470, top: 120, width: 1380, height: 776, transform: `scaleX(${unroll})`, transformOrigin: "center" }}>
        <Img src={staticFile("art/scroll.webp")} style={{ width: "100%", height: "100%" }} />
        <div style={{ position: "absolute", left: 200, right: 200, top: 120, display: "flex", flexDirection: "column", gap: 26, opacity: unroll > 0.9 ? 1 : 0 }}>
          <div style={{ fontFamily: serif, fontWeight: 800, fontSize: 64, color: C.brown, textAlign: "center", letterSpacing: "0.06em" }}>THE LEDGER</div>
          <Row label="Plunder" value={`${gold.toLocaleString("en-US")} Gold`} at={24} />
          <Row label="Krakens annoyed" value="3" at={70} />
          <Row label="Repairs" value="2,650 Gold" at={112} color="#9c1d12" />
          <div style={{ height: 3, background: "#7a4a1c", opacity: 0.6 }} />
          <Row label="Balance" value="-250 Gold" at={130} color="#9c1d12" />
        </div>
        <div style={{ position: "absolute", left: "50%", marginLeft: -150, bottom: 40, fontFamily: serif, fontWeight: 900, fontSize: 70, color: "#b3241a", border: "8px solid #b3241a", padding: "4px 26px", borderRadius: 14, transform: `rotate(-12deg) scale(${2 - stampIn})`, opacity: stampIn }}>
          SUNK
        </div>
      </div>
      <Img src={staticFile("art/penny.webp")} style={{ position: "absolute", left: -40 + (1 - penny) * -300, bottom: -40, height: 1000, filter: "drop-shadow(0 20px 30px rgba(0,0,0,.5))" }} />
      <div style={{ position: "absolute", left: 120, top: 70, fontFamily: body, fontStyle: "italic", fontSize: 46, color: C.ink, opacity: interpolate(frame, [10, 20, 100, 110], [0, 1, 1, 0], { extrapolateLeft: "clamp", extrapolateRight: "clamp" }) }}>
        Penny: “Good news. We made 2,400 Gold.”
      </div>
      <div style={{ position: "absolute", left: 120, top: 70, fontFamily: body, fontStyle: "italic", fontSize: 46, color: C.ink, opacity: interpolate(frame, [112, 122], [0, 1], { extrapolateLeft: "clamp", extrapolateRight: "clamp" }) }}>
        Penny: “Repairs cost 2,650.”
      </div>
    </AbsoluteFill>
  );
};
