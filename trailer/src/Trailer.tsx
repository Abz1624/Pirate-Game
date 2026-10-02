import { AbsoluteFill, Audio, interpolate, OffthreadVideo, Sequence, staticFile, useVideoConfig } from "remotion";
import { Captains, CAPTAINS_FRAMES } from "./Captains";
import { EndCard } from "./EndCard";
import { Ledger, LEDGER_FRAMES } from "./Ledger";
import { Bounty, Leaderboard, Market, RaidAlert, RaidSchedule, SailDesigner } from "./Planned";
import { Flash, Overlay, Painting, RaceHud, TitleCard, ZoneTitle } from "./Titles";
import { FPS } from "./theme";

// The edit, as data. Each beat is a clip (a take in public/takes, from its `from` second) or a graphic, with optional
// overlays. Durations in seconds. The player cut and the web3 cut differ only where `web3` is read.

type Beat = { dur: number; clip?: string; from?: number; rate?: number; render?: (web3: boolean) => React.ReactNode; over?: React.ReactNode };

const clip = (name: string, from: number, dur: number, over?: React.ReactNode, rate = 1): Beat => ({ clip: name, from, dur, over, rate });
const gfx = (dur: number, render: (web3: boolean) => React.ReactNode): Beat => ({ dur, render });

export const BEATS: Beat[] = [
  // Cold open
  gfx(4.2, () => <TitleCard lines={["Every pirate sails for the same dream.", "Get rich enough to retire."]} gap={70} />),
  clip("v2_open_low", 1.0, 3.4, <Overlay text="Nobody retires." />),
  // The Voyage (built)
  gfx(CAPTAINS_FRAMES / FPS, () => <Captains />),
  clip("v_open_crane", 0.0, 2.6),
  clip("levelup", 0.6, 2.6),
  clip("v2_weapon_lightning_mast", 2.0, 1.2, <Overlay text="19 weapons." />),
  clip("v2_weapon_powder_kegs", 2.0, 1.1),
  clip("v2_weapon_flying_cutlasses", 2.0, 1.1),
  clip("v_weapon_mortar", 3.0, 1.1),
  clip("v2_crowd_top", 2.0, 1.2),
  clip("v2_evolve", 1.6, 3.0, <><Flash at={66} /><Overlay text="19 evolutions." delay={70} /></>),
  clip("v2_horde_line", 4.0, 2.4, <Overlay text="The sea fights back." />),
  clip("v2_peak_orbit", 0.5, 2.2),
  clip("v2_boss_rise", 2.0, 4.6, <Overlay sub="THE BARNACLE KING" text="“Their treasure is unpaid tax.”" delay={30} />),
  clip("v2_boss_slowmo", 3.0, 3.0),
  clip("v2_sink", 0.5, 3.2),
  gfx(LEDGER_FRAMES / FPS, () => <Ledger />),
  gfx(2.6, () => <TitleCard lines={["You keep the fortune.", "You lose the build."]} gap={40} italic={false} size={80} />),
  gfx(2.0, () => <TitleCard lines={["Then the sea gets personal."]} />),
  // Your island (planned)
  gfx(2.4, () => <Painting src="art/home-island.webp" caption="Build your island." />),
  gfx(3.3, () => <RaidSchedule />),
  clip("a_raid_orbit", 1.0, 3.6, <Overlay text="Raid other captains." />),
  gfx(3.3, () => <RaidAlert />),
  clip("a_ai_patrol", 0.5, 2.8, <Overlay text="Your AI captain holds the line." />),
  // Compete (planned)
  clip("a_race_chase", 1.0, 3.8, <><RaceHud /><Overlay sub="ISLAND RACE" text="One attempt. Fastest time." /></>),
  gfx(1.4, () => <Painting src="art/duel.webp" />),
  clip("a_arena_pair", 2.0, 3.4, <Overlay sub="1V1 ARENA" text="Any weapon. One winner." />),
  clip("a_hunt_wide", 2.0, 2.4, <Overlay sub="HUNT" text="Four captains. One survivor." />),
  clip("a_hunt_orbit", 2.0, 2.0),
  gfx(3.5, (web3) => <Leaderboard web3={web3} />),
  // Make it yours (planned)
  gfx(4.0, () => <SailDesigner />),
  gfx(3.3, (web3) => <Market web3={web3} />),
  gfx(3.7, (web3) => <Bounty web3={web3} />),
  // Teaser
  clip("a_gravewater", 1.0, 3.0, <ZoneTitle name="GRAVEWATER" tint="rgba(20,90,60,.85)" />),
  clip("a_maw_wide", 1.0, 3.6, <ZoneTitle name="DEVIL'S MAW" tint="rgba(140,20,10,.85)" tentacles line="We meet again." speaker="Rook" />),
  gfx(1.6, () => <TitleCard lines={["The Kraken stares.", "“...You remember me.”"]} gap={40} />),
  gfx(5.0, () => <EndCard />),
];

export const totalSeconds = (beats: Beat[]) => beats.reduce((s, b) => s + b.dur, 0);
export const TRAILER_FRAMES = Math.round(totalSeconds(BEATS) * FPS);

const Music: React.FC = () => {
  const { durationInFrames } = useVideoConfig();
  return (
    <Audio
      src={staticFile("audio/bgm.mp3")}
      loop
      volume={(f) => interpolate(f, [0, 30, durationInFrames - 90, durationInFrames], [0, 0.9, 0.9, 0], { extrapolateLeft: "clamp", extrapolateRight: "clamp" })}
    />
  );
};

export const Trailer: React.FC<{ web3?: boolean }> = ({ web3 = false }) => {
  let at = 0;
  return (
    <AbsoluteFill style={{ backgroundColor: "#000" }}>
      {BEATS.map((b, i) => {
        const from = Math.round(at * FPS);
        const dur = Math.round(b.dur * FPS);
        at += b.dur;
        return (
          <Sequence key={i} from={from} durationInFrames={dur}>
            {b.clip ? (
              <AbsoluteFill>
                <OffthreadVideo src={staticFile(`takes/${b.clip}.mp4`)} startFrom={Math.round((b.from ?? 0) * FPS)} muted playbackRate={b.rate ?? 1} style={{ width: "100%", height: "100%", objectFit: "cover" }} />
              </AbsoluteFill>
            ) : (
              b.render?.(web3)
            )}
            {b.over}
          </Sequence>
        );
      })}
      <Music />
    </AbsoluteFill>
  );
};
