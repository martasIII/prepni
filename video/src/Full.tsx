import { AbsoluteFill, Audio, interpolate, OffthreadVideo, Sequence, staticFile, useCurrentFrame, useVideoConfig } from "remotion";
import { Caption, CaptionSpec } from "./Caption";
import { Intro } from "./Intro";
import { Outro } from "./Outro";
import { colors, fadeFrames, font, introFrames, outroFrames, sounds } from "./theme";

/** Výřez záznamu v jeho pixelech; zvětší se na celé video a okraje se zakryjí pozadím. */
export type Crop = { x: number; y: number; width: number; height: number };

export type FullProps = {
  /** Záznam obrazovky v public/, třeba „recording.mp4“. */
  recording: string;
  /** Výřez záznamu, třeba malý Notepad nad hlavním panelem s přepínačem klávesnice; null = celý záznam. */
  crop?: Crop | null;
  /** Zrychlení záznamu (1,5 = o polovinu rychleji), ať psaní netrvá dlouho. */
  speed?: number;
  /** Kolik sekund záznamu přeskočit na začátku. */
  trimStart?: number;
  /** Titulky v sekundách původního záznamu (před zrychlením), třeba dvojí Shift ve chvíli, kdy ho uživatel dal. */
  captions?: CaptionSpec[];
  /** Rozměry záznamu; doplní calculateMetadata. */
  recordingWidth?: number;
  recordingHeight?: number;
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

/** Záznam zvětšený tak, aby výřez vyplnil video, a vycentrovaný. */
const Recording: React.FC<{ src: string; crop?: Crop | null; width: number; height: number; speed: number; trimStart: number }> = ({
  src,
  crop,
  width,
  height,
  speed,
  trimStart,
}) => {
  const area = crop ?? { x: 0, y: 0, width, height };
  const scale = Math.min(1920 / area.width, 1080 / area.height);
  const left = (1920 - area.width * scale) / 2 - area.x * scale;
  const top = (1080 - area.height * scale) / 2 - area.y * scale;

  return (
    <AbsoluteFill style={{ background: colors.background, overflow: "hidden" }}>
      <OffthreadVideo
        src={src}
        muted
        playbackRate={speed}
        startFrom={Math.round(trimStart * 30)}
        style={{ position: "absolute", left, top, width: width * scale, height: height * scale }}
      />
    </AbsoluteFill>
  );
};

/** Znělka pod celým videem: pod záznamem tišší, na začátku a na konci se ztratí. */
const Music: React.FC<{ recordingFrames: number }> = ({ recordingFrames }) => {
  const { durationInFrames } = useVideoConfig();
  const recordingEnd = introFrames + recordingFrames;

  return (
    <Audio
      src={staticFile(sounds.music)}
      volume={(frame) => {
        const duck = interpolate(frame, [introFrames - 10, introFrames + 10, recordingEnd - 10, recordingEnd + 10], [0.85, 0.45, 0.45, 0.85], {
          extrapolateLeft: "clamp",
          extrapolateRight: "clamp",
        });
        const fade = interpolate(frame, [0, 8, durationInFrames - 30, durationInFrames], [0, 1, 1, 0], {
          extrapolateLeft: "clamp",
          extrapolateRight: "clamp",
        });
        return duck * fade;
      }}
    />
  );
};

const Placeholder: React.FC = () => (
  <AbsoluteFill style={{ background: colors.face, fontFamily: font, alignItems: "center", justifyContent: "center", color: colors.muted, fontSize: 56 }}>
    Sem přijde záznam obrazovky: public/recording.mp4
  </AbsoluteFill>
);

export const Full: React.FC<FullProps> = ({
  recording,
  crop,
  speed = 1,
  trimStart = 0,
  captions = [],
  recordingWidth = 1920,
  recordingHeight = 1080,
  recordingFrames = 450,
  hasRecording = false,
}) => (
  <AbsoluteFill style={{ background: colors.background }}>
    <Music recordingFrames={recordingFrames} />

    <Sequence durationInFrames={introFrames}>
      <Fade frames={introFrames}>
        <Intro />
      </Fade>
    </Sequence>

    <Sequence from={introFrames} durationInFrames={recordingFrames}>
      <Fade frames={recordingFrames}>
        {hasRecording ? (
          <Recording
            src={staticFile(recording)}
            crop={crop}
            width={recordingWidth}
            height={recordingHeight}
            speed={speed}
            trimStart={trimStart}
          />
        ) : (
          <Placeholder />
        )}
        {captions.map((caption, i) => {
          const from = Math.round(((caption.from - trimStart) / speed) * 30);
          const frames = Math.round(((caption.to - caption.from) / speed) * 30);
          return (
            <Sequence key={i} from={from} durationInFrames={frames}>
              <Caption text={caption.text} frames={frames} />
            </Sequence>
          );
        })}
      </Fade>
    </Sequence>

    <Sequence from={introFrames + recordingFrames} durationInFrames={outroFrames}>
      <Fade frames={outroFrames}>
        <Outro />
      </Fade>
    </Sequence>
  </AbsoluteFill>
);
