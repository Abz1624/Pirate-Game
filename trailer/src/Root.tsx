import { Composition } from "remotion";
import { Captains, CAPTAINS_FRAMES } from "./Captains";
import { Ledger, LEDGER_FRAMES } from "./Ledger";
import { EndCard, END_FRAMES } from "./EndCard";
import { Bounty, BOUNTY_FRAMES, Leaderboard, LEADERBOARD_FRAMES, Market, MARKET_FRAMES, RaidAlert, RAID_ALERT_FRAMES, RaidSchedule, RAID_SCHEDULE_FRAMES, SailDesigner, SAIL_DESIGNER_FRAMES } from "./Planned";
import { Trailer, TRAILER_FRAMES } from "./Trailer";
import { FontFaces, FPS, H, W } from "./theme";

const withFonts = (C: React.FC) => () => (
  <>
    <FontFaces />
    <C />
  </>
);

export const Root: React.FC = () => (
  <>
    <Composition id="Ledger" component={withFonts(Ledger)} durationInFrames={LEDGER_FRAMES} fps={FPS} width={W} height={H} />
    <Composition id="EndCard" component={withFonts(EndCard)} durationInFrames={END_FRAMES} fps={FPS} width={W} height={H} />
    <Composition id="Trailer" component={withFonts(() => <Trailer />)} durationInFrames={TRAILER_FRAMES} fps={FPS} width={W} height={H} />
    <Composition id="TrailerWeb3" component={withFonts(() => <Trailer web3 />)} durationInFrames={TRAILER_FRAMES} fps={FPS} width={W} height={H} />
    <Composition id="RaidSchedule" component={withFonts(RaidSchedule)} durationInFrames={RAID_SCHEDULE_FRAMES} fps={FPS} width={W} height={H} />
    <Composition id="RaidAlert" component={withFonts(RaidAlert)} durationInFrames={RAID_ALERT_FRAMES} fps={FPS} width={W} height={H} />
    <Composition id="Leaderboard" component={withFonts(() => <Leaderboard />)} durationInFrames={LEADERBOARD_FRAMES} fps={FPS} width={W} height={H} />
    <Composition id="LeaderboardWeb3" component={withFonts(() => <Leaderboard web3 />)} durationInFrames={LEADERBOARD_FRAMES} fps={FPS} width={W} height={H} />
    <Composition id="SailDesigner" component={withFonts(SailDesigner)} durationInFrames={SAIL_DESIGNER_FRAMES} fps={FPS} width={W} height={H} />
    <Composition id="Market" component={withFonts(() => <Market />)} durationInFrames={MARKET_FRAMES} fps={FPS} width={W} height={H} />
    <Composition id="MarketWeb3" component={withFonts(() => <Market web3 />)} durationInFrames={MARKET_FRAMES} fps={FPS} width={W} height={H} />
    <Composition id="Bounty" component={withFonts(() => <Bounty />)} durationInFrames={BOUNTY_FRAMES} fps={FPS} width={W} height={H} />
    <Composition id="BountyWeb3" component={withFonts(() => <Bounty web3 />)} durationInFrames={BOUNTY_FRAMES} fps={FPS} width={W} height={H} />
    <Composition id="Captains" component={withFonts(Captains)} durationInFrames={CAPTAINS_FRAMES} fps={FPS} width={W} height={H} />
  </>
);
