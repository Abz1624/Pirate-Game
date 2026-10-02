import { AbsoluteFill, Img, interpolate, staticFile, useCurrentFrame, useVideoConfig } from "remotion";
import { body, C, serif } from "./theme";
import { useSpring } from "./kit";

/** Black title card, lines fading in one after another. */
export const TitleCard: React.FC<{ lines: string[]; gap?: number; italic?: boolean; size?: number; bg?: string }> = ({ lines, gap = 50, italic = true, size = 70, bg = "#050a0c" }) => {
  const frame = useCurrentFrame();
  const { durationInFrames } = useVideoConfig();
  const out = interpolate(frame, [durationInFrames - 10, durationInFrames], [1, 0], { extrapolateLeft: "clamp", extrapolateRight: "clamp" });
  return (
    <AbsoluteFill style={{ background: bg, alignItems: "center", justifyContent: "center", gap: 26, opacity: out }}>
      {lines.map((l, i) => {
        const o = interpolate(frame, [i * gap, i * gap + 18], [0, 1], { extrapolateLeft: "clamp", extrapolateRight: "clamp" });
        return (
          <div key={l} style={{ fontFamily: italic ? body : serif, fontStyle: italic ? "italic" : "normal", fontWeight: italic ? 400 : 800, fontSize: size, color: C.ink, opacity: o, transform: `translateY(${(1 - o) * 14}px)` }}>
            {l}
          </div>
        );
      })}
    </AbsoluteFill>
  );
};

/** Caption over footage: lower third, Cinzel, with a soft shadow band. */
export const Overlay: React.FC<{ text: string; sub?: string; delay?: number; top?: boolean }> = ({ text, sub, delay = 4, top = false }) => {
  const s = useSpring(delay, 14, 150);
  return (
    <AbsoluteFill style={{ justifyContent: top ? "flex-start" : "flex-end", alignItems: "center", paddingBottom: 90, paddingTop: 70 }}>
      <div style={{ position: "absolute", left: 0, right: 0, [top ? "top" : "bottom"]: 0, height: 320, background: `linear-gradient(${top ? "180deg" : "0deg"}, rgba(0,0,0,.55), rgba(0,0,0,0))` } as React.CSSProperties} />
      <div style={{ position: "relative", textAlign: "center", opacity: s, transform: `translateY(${(1 - s) * 24}px)` }}>
        {sub ? <div style={{ fontFamily: serif, fontWeight: 700, fontSize: 34, letterSpacing: "0.3em", color: C.gold, marginBottom: 6 }}>{sub}</div> : null}
        <div style={{ fontFamily: serif, fontWeight: 800, fontSize: 76, color: C.ink, textShadow: "0 4px 0 rgba(0,0,0,.6), 0 10px 30px rgba(0,0,0,.7)" }}>{text}</div>
      </div>
    </AbsoluteFill>
  );
};

/** A quick white flash (evolution, boss rise). */
export const Flash: React.FC<{ at?: number; length?: number }> = ({ at = 0, length = 12 }) => {
  const frame = useCurrentFrame();
  const o = interpolate(frame, [at, at + 2, at + length], [0, 0.9, 0], { extrapolateLeft: "clamp", extrapolateRight: "clamp" });
  return <AbsoluteFill style={{ background: "#fff", opacity: o }} />;
};

/** Island Race HUD: stopwatch and the gap to the leader's ghost. */
export const RaceHud: React.FC = () => {
  const frame = useCurrentFrame();
  const { fps } = useVideoConfig();
  const t = 83.4 + frame / fps;
  const mm = Math.floor(t / 60), ss = (t % 60).toFixed(2).padStart(5, "0");
  const gap = (0.8 - frame / fps * 0.12).toFixed(1);
  const s = useSpring(0, 16);
  return (
    <AbsoluteFill style={{ alignItems: "center", paddingTop: 50 }}>
      <div style={{ display: "flex", gap: 24, alignItems: "center", opacity: s, transform: `translateY(${(1 - s) * -60}px)` }}>
        <div style={{ padding: "14px 34px", borderRadius: 18, background: "rgba(9,30,40,.85)", border: `3px solid ${C.gold}`, fontFamily: serif, fontWeight: 800, fontSize: 64, color: C.ink, fontVariantNumeric: "tabular-nums" }}>{`${mm}:${ss}`}</div>
        <div style={{ padding: "12px 24px", borderRadius: 14, background: "rgba(140,255,240,.18)", border: "2px solid rgba(140,255,240,.7)", fontFamily: body, fontSize: 32, color: "#cffff8" }}>Leader's ghost +{gap}s</div>
      </div>
    </AbsoluteFill>
  );
};

/** Zone teaser title over footage, with optional tentacles rising (Devil's Maw). */
export const ZoneTitle: React.FC<{ name: string; tint: string; tentacles?: boolean; line?: string; speaker?: string }> = ({ name, tint, tentacles = false, line, speaker }) => {
  const frame = useCurrentFrame();
  const s = useSpring(6, 14, 120);
  const rise = interpolate(frame, [10, 50], [600, 0], { extrapolateLeft: "clamp", extrapolateRight: "clamp" });
  const lineIn = useSpring(40, 16, 150);
  return (
    <AbsoluteFill>
      <AbsoluteFill style={{ background: `radial-gradient(80% 80% at 50% 50%, rgba(0,0,0,0) 40%, ${tint} 100%)` }} />
      {tentacles ? (
        <>
          <Img src={staticFile("web/octo-left.webp")} style={{ position: "absolute", left: -40, bottom: -60, height: 760, transform: `translateY(${rise}px)` }} />
          <Img src={staticFile("web/octo-right.webp")} style={{ position: "absolute", right: -40, bottom: -60, height: 760, transform: `translateY(${rise * 1.2}px)` }} />
        </>
      ) : null}
      <AbsoluteFill style={{ alignItems: "center", justifyContent: "center" }}>
        <div style={{ fontFamily: serif, fontWeight: 900, fontSize: 150, letterSpacing: "0.08em", color: C.ink, opacity: s, transform: `scale(${1.15 - s * 0.15})`, textShadow: `0 0 40px ${tint}, 0 8px 0 rgba(0,0,0,.6)` }}>{name}</div>
        {line ? (
          <div style={{ marginTop: 20, fontFamily: body, fontStyle: "italic", fontSize: 50, color: C.ink, opacity: lineIn, textShadow: "0 4px 20px rgba(0,0,0,.8)" }}>
            {speaker ? <span style={{ color: C.gold }}>{speaker}: </span> : null}“{line}”
          </div>
        ) : null}
      </AbsoluteFill>
    </AbsoluteFill>
  );
};

/** A painting held on screen with a slow push in (concept art beats). */
export const Painting: React.FC<{ src: string; caption?: string; sub?: string }> = ({ src, caption, sub }) => {
  const frame = useCurrentFrame();
  const { durationInFrames } = useVideoConfig();
  const z = interpolate(frame, [0, durationInFrames], [1.0, 1.12]);
  return (
    <AbsoluteFill style={{ backgroundColor: C.night }}>
      <Img src={staticFile(src)} style={{ width: "100%", height: "100%", objectFit: "cover", transform: `scale(${z})` }} />
      {caption ? <Overlay text={caption} sub={sub} /> : null}
    </AbsoluteFill>
  );
};
