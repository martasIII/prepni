import { AbsoluteFill, interpolate, OffthreadVideo, Sequence, staticFile, useCurrentFrame } from "remotion";
import { Intro } from "./Intro";
import { Outro } from "./Outro";
import { colors, fadeFrames, font, introFrames, outroFrames } from "./theme";

export type FullProps = {
  /** Záznam obrazovky v public/, třeba „recording.mp4“. */
  recording: string;
  /** Doplní calculateMetadata podle skutečné délky záznamu. */
  recordingFrames?: number;
  hasRecording?: boolean;
};

/** Prolínání okrajů úseku: na začátku a na konci se rozsvítí a zhasne. */
const Fade: React.FC<{ frames: number; children: React.ReactNode }> = ({ frames, children }) => {
  const frame = useCurrentFrame();
  const opacity = interpolate(frame, [0, fadeFrames, frames - fadeFrames, frames], [0, 1, 1, 0], {
    extrapolateLeft: "clamp",
    extrapolateRight: "clamp",
  });
  return <AbsoluteFill style={{ opacity }}>{children}</AbsoluteFill>;
};

const Placeholder: React.FC = () => (
  <AbsoluteFill style={{ background: colors.face, fontFamily: font, alignItems: "center", justifyContent: "center", color: colors.muted, fontSize: 56 }}>
    Sem přijde záznam obrazovky: public/recording.mp4
  </AbsoluteFill>
);

export const Full: React.FC<FullProps> = ({ recording, recordingFrames = 450, hasRecording = false }) => (
  <AbsoluteFill style={{ background: colors.background }}>
    <Sequence durationInFrames={introFrames}>
      <Fade frames={introFrames}>
        <Intro />
      </Fade>
    </Sequence>

    <Sequence from={introFrames} durationInFrames={recordingFrames}>
      <Fade frames={recordingFrames}>
        {hasRecording ? (
          <OffthreadVideo src={staticFile(recording)} muted style={{ width: "100%", height: "100%", objectFit: "contain" }} />
        ) : (
          <Placeholder />
        )}
      </Fade>
    </Sequence>

    <Sequence from={introFrames + recordingFrames} durationInFrames={outroFrames}>
      <Fade frames={outroFrames}>
        <Outro />
      </Fade>
    </Sequence>
  </AbsoluteFill>
);
