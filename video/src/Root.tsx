import { getVideoMetadata } from "@remotion/media-utils";
import { CalculateMetadataFunction, Composition, staticFile } from "remotion";
import { Full, FullProps } from "./Full";
import { Intro } from "./Intro";
import { Outro } from "./Outro";
import { fps, height, introFrames, outroFrames, width } from "./theme";

// Délka celého videa podle záznamu; když ještě není, ukáže se místo něj 15 s zástupné plochy.
const fullMetadata: CalculateMetadataFunction<FullProps> = async ({ props }) => {
  try {
    const { durationInSeconds } = await getVideoMetadata(staticFile(props.recording));
    const recordingFrames = Math.round(durationInSeconds * fps);
    return { durationInFrames: introFrames + recordingFrames + outroFrames, props: { ...props, recordingFrames, hasRecording: true } };
  } catch {
    const recordingFrames = 15 * fps;
    return { durationInFrames: introFrames + recordingFrames + outroFrames, props: { ...props, recordingFrames, hasRecording: false } };
  }
};

export const Root: React.FC = () => (
  <>
    <Composition id="Intro" component={Intro} durationInFrames={introFrames} fps={fps} width={width} height={height} />
    <Composition id="Outro" component={Outro} durationInFrames={outroFrames} fps={fps} width={width} height={height} />
    <Composition
      id="Full"
      component={Full}
      durationInFrames={introFrames + 15 * fps + outroFrames}
      fps={fps}
      width={width}
      height={height}
      defaultProps={{ recording: "recording.mp4" }}
      calculateMetadata={fullMetadata}
    />
  </>
);
