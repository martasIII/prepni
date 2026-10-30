import { AbsoluteFill, Audio, interpolate, Sequence, staticFile, useCurrentFrame } from "remotion";
import { Icon, ShiftKey } from "./Icon";
import { colors, font, mono, sounds } from "./theme";

const appear = (frame: number, from: number) =>
  interpolate(frame, [from, from + 12], [0, 1], { extrapolateLeft: "clamp", extrapolateRight: "clamp" });

export const Outro: React.FC = () => {
  const frame = useCurrentFrame();

  const tap = (at: number) => interpolate(frame, [at, at + 3, at + 8], [0, 1, 0], { extrapolateLeft: "clamp", extrapolateRight: "clamp" });
  const heading = appear(frame, 0);
  const brand = appear(frame, 46);
  const link = appear(frame, 62);

  return (
    <AbsoluteFill style={{ background: colors.background, fontFamily: font, alignItems: "center", justifyContent: "center", gap: 64 }}>
      <Sequence from={20} durationInFrames={10}>
        <Audio src={staticFile(sounds.tap)} volume={0.5} />
      </Sequence>
      <Sequence from={30} durationInFrames={10}>
        <Audio src={staticFile(sounds.tap)} volume={0.5} />
      </Sequence>
      <Sequence from={46} durationInFrames={20}>
        <Audio src={staticFile(sounds.success)} volume={0.45} />
      </Sequence>

      <div style={{ fontSize: 110, fontWeight: 600, color: colors.text, opacity: heading, transform: `translateY(${(1 - heading) * 20}px)` }}>
        Dvakrát Shift a je to.
      </div>

      <div style={{ display: "flex", gap: 40 }}>
        <ShiftKey pressed={tap(20)} />
        <ShiftKey pressed={tap(30)} />
      </div>

      <div style={{ display: "flex", flexDirection: "column", alignItems: "center", gap: 22 }}>
        <div style={{ display: "flex", alignItems: "center", gap: 26, opacity: brand }}>
          <Icon size={84} />
          <span style={{ fontSize: 72, fontWeight: 600, color: colors.text }}>Přepni</span>
          <span style={{ fontSize: 44, color: colors.muted }}>zdarma · open source · Windows</span>
        </div>
        <div style={{ fontFamily: mono, fontSize: 46, color: colors.frame, opacity: link }}>github.com/martasIII/prepni</div>
      </div>
    </AbsoluteFill>
  );
};
