import { Composition } from "remotion";
import { Captains, CAPTAINS_FRAMES } from "./Captains";
import { Ledger, LEDGER_FRAMES } from "./Ledger";
import { EndCard, END_FRAMES } from "./EndCard";
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
    <Composition id="Captains" component={withFonts(Captains)} durationInFrames={CAPTAINS_FRAMES} fps={FPS} width={W} height={H} />
  </>
);
