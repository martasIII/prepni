import { AbsoluteFill, Audio, interpolate, Sequence, staticFile, useCurrentFrame } from "remotion";
import { Icon, ShiftKey } from "./Icon";
import { colors, font, mono, sounds } from "./theme";

const appear = (frame: number, from: number) =>
  interpolate(frame, [from, from + 12], [0, 1], { extrapolateLeft: "clamp", extrapolateRight: "clamp" });

/** Závěr: co to dělá, že je to zdarma, kde to stáhnout a kdo to udělal; odkaz zůstane až do konce. */
export const Outro: React.FC = () => {
  const frame = useCurrentFrame();

  const tap = (at: number) => interpolate(frame, [at, at + 3, at + 8], [0, 1, 0], { extrapolateLeft: "clamp", extrapolateRight: "clamp" });
  const heading = appear(frame, 0);
  const brand = appear(frame, 40);
  const free = appear(frame, 52);
  const link = appear(frame, 64);
  const author = appear(frame, 90);

  return (
    <AbsoluteFill style={{ background: colors.background, fontFamily: font, alignItems: "center", justifyContent: "center", gap: 46 }}>
      <Sequence from={20} durationInFrames={10}>
        <Audio src={staticFile(sounds.tap)} volume={0.5} />
      </Sequence>
      <Sequence from={30} durationInFrames={10}>
        <Audio src={staticFile(sounds.tap)} volume={0.5} />
      </Sequence>
      <Sequence from={40} durationInFrames={20}>
        <Audio src={staticFile(sounds.success)} volume={0.45} />
      </Sequence>

      <div style={{ fontSize: 96, fontWeight: 600, color: colors.text, opacity: heading, transform: `translateY(${(1 - heading) * 20}px)` }}>
        Dvakrát Shift a je to.
      </div>

      <div style={{ display: "flex", gap: 36 }}>
        <ShiftKey pressed={tap(20)} scale={0.85} />
        <ShiftKey pressed={tap(30)} scale={0.85} />
      </div>

      <div style={{ display: "flex", alignItems: "center", gap: 30, opacity: brand }}>
        <Icon size={96} />
        <span style={{ fontSize: 80, fontWeight: 600, color: colors.text }}>Přepni</span>
        <span
          style={{
            opacity: free,
            transform: `scale(${0.9 + 0.1 * free})`,
            padding: "12px 30px",
            borderRadius: 999,
            background: colors.letter,
            color: "#FFFFFF",
            fontSize: 46,
            fontWeight: 600,
          }}
        >
          Zdarma · open source
        </span>
      </div>

      <div style={{ fontFamily: mono, fontSize: 58, fontWeight: 600, color: colors.frame, opacity: link }}>github.com/martasIII/prepni</div>

      <div style={{ fontSize: 40, color: colors.muted, opacity: author }}>Pro Windows · vytvořil Martin Hájek · martinhajek.dev</div>
    </AbsoluteFill>
  );
};
