import { AbsoluteFill, interpolate, spring, useCurrentFrame, useVideoConfig } from "remotion";
import { Icon, ShiftKey } from "./Icon";
import { colors, font } from "./theme";

// Název napsaný na anglické klávesnici: „P5epni“. Dvakrát Shift a z pětky je „ř“.
const flipAt = 42;

export const Intro: React.FC = () => {
  const frame = useCurrentFrame();
  const { fps } = useVideoConfig();

  const iconIn = spring({ frame, fps, config: { damping: 14 } });
  const titleIn = interpolate(frame, [6, 18], [0, 1], { extrapolateLeft: "clamp", extrapolateRight: "clamp" });

  // Pětka se „překlopí“ na ř: smrskne se na čáru, vymění a zase rozbalí.
  const flip = interpolate(frame, [flipAt - 6, flipAt, flipAt + 6], [1, 0, 1], { extrapolateLeft: "clamp", extrapolateRight: "clamp" });
  const fixed = frame >= flipAt;

  // Dvě klepnutí na Shift těsně před překlopením.
  const tap = (at: number) => interpolate(frame, [at, at + 3, at + 7], [0, 1, 0], { extrapolateLeft: "clamp", extrapolateRight: "clamp" });
  const keysOpacity = interpolate(frame, [22, 28, 54, 62], [0, 1, 1, 0], { extrapolateLeft: "clamp", extrapolateRight: "clamp" });
  const subtitle = interpolate(frame, [62, 74], [0, 1], { extrapolateLeft: "clamp", extrapolateRight: "clamp" });

  return (
    <AbsoluteFill style={{ background: colors.background, fontFamily: font, alignItems: "center", justifyContent: "center" }}>
      <div style={{ display: "flex", alignItems: "center", gap: 56 }}>
        <div style={{ transform: `scale(${0.6 + 0.4 * iconIn})`, opacity: iconIn }}>
          <Icon size={200} />
        </div>
        <div style={{ fontSize: 190, fontWeight: 600, color: colors.text, opacity: titleIn, letterSpacing: -2 }}>
          P
          <span
            style={{
              display: "inline-block",
              transform: `scaleY(${flip})`,
              color: fixed ? colors.letter : colors.digit,
            }}
          >
            {fixed ? "ř" : "5"}
          </span>
          epni
        </div>
      </div>

      <div style={{ position: "absolute", top: 720, height: 120, display: "flex", alignItems: "center", justifyContent: "center" }}>
        <div style={{ position: "absolute", display: "flex", gap: 28, opacity: keysOpacity }}>
          <ShiftKey pressed={tap(28)} scale={0.8} />
          <ShiftKey pressed={tap(36)} scale={0.8} />
        </div>
        <div style={{ position: "absolute", whiteSpace: "nowrap", fontSize: 56, color: colors.muted, opacity: subtitle, transform: `translateY(${(1 - subtitle) * 16}px)` }}>
          Píšeš česky na anglické klávesnici?
        </div>
      </div>
    </AbsoluteFill>
  );
};
