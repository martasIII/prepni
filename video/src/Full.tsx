import { AbsoluteFill, Audio, interpolate, OffthreadVideo, Sequence, staticFile, useCurrentFrame, useVideoConfig } from "remotion";
import { Caption, CaptionSpec } from "./Caption";
import { Highlight, HighlightSpec } from "./Highlight";
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
  /** Zvýraznění v záznamu (čas jako u titulků, souřadnice v pixelech záznamu), třeba přepnutí klávesnice. */
  highlights?: HighlightSpec[];
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

/** Kam se záznam ve videu vejde: zvětšení a posun tak, aby výřez vyplnil video a byl uprostřed. */
const placement = (crop: Crop | null | undefined, width: number, height: number) => {
  const area = crop ?? { x: 0, y: 0, width, height };
  const scale = Math.min(1920 / area.width, 1080 / area.height);
  return {
    scale,
    left: (1920 - area.width * scale) / 2 - area.x * scale,
    top: (1080 - area.height * scale) / 2 - area.y * scale,
  };
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
  const { scale, left, top } = placement(crop, width, height);

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

/** Znělka pod celým videem: pod záznamem tišší, v závěru jen o kousek hlasitější (víc trhalo uši), na začátku a na konci se ztratí. */
const Music: React.FC<{ recordingFrames: number }> = ({ recordingFrames }) => {
  const { durationInFrames } = useVideoConfig();
  const recordingEnd = introFrames + recordingFrames;

  return (
    <Audio
      src={staticFile(sounds.music)}
      volume={(frame) => {
        const duck = interpolate(frame, [introFrames - 10, introFrames + 10, recordingEnd - 10, recordingEnd + 10], [0.85, 0.45, 0.45, 0.5], {
          extrapolateLeft: "clamp",
          extrapolateRight: "clamp",
        });
        const fade = interpolate(frame, [0, 8, durationInFrames - 50, durationInFrames], [0, 1, 1, 0], {
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
  highlights = [],
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
        {highlights.map((highlight, i) => {
          const { scale, left, top } = placement(crop, recordingWidth, recordingHeight);
          const from = Math.round(((highlight.from - trimStart) / speed) * 30);
          const frames = Math.round(((highlight.to - highlight.from) / speed) * 30);
          return (
            <Sequence key={`h${i}`} from={from} durationInFrames={frames}>
              <Highlight x={left + highlight.x * scale} y={top + highlight.y * scale} label={highlight.label} frames={frames} />
            </Sequence>
          );
        })}
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
