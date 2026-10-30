import { AbsoluteFill, interpolate, useCurrentFrame } from "remotion";
import { colors, font } from "./theme";

/** Zvýraznění místa v záznamu, třeba přepínače klávesnice; souřadnice v pixelech záznamu (před zvětšením). */
export type HighlightSpec = { from: number; to: number; x: number; y: number; label: string };

/** Pulzující kroužek kolem místa a štítek nad ním; x, y už ve výsledném videu. */
export const Highlight: React.FC<{ x: number; y: number; label: string; frames: number }> = ({ x, y, label, frames }) => {
  const frame = useCurrentFrame();
  const opacity = interpolate(frame, [0, 6, frames - 8, frames], [0, 1, 1, 0], { extrapolateLeft: "clamp", extrapolateRight: "clamp" });
  const pulse = 1 + 0.12 * Math.sin((frame / 30) * Math.PI * 3);
  const radius = 78;

  return (
    <AbsoluteFill style={{ opacity, pointerEvents: "none" }}>
      <div
        style={{
          position: "absolute",
          left: x - radius,
          top: y - radius,
          width: radius * 2,
          height: radius * 2,
          borderRadius: "50%",
          border: `7px solid ${colors.letter}`,
          transform: `scale(${pulse})`,
        }}
      />
      <div
        style={{
          position: "absolute",
          left: x,
          top: y - radius - 120,
          transform: "translateX(-50%)",
          padding: "16px 34px",
          borderRadius: 16,
          background: colors.letter,
          color: "#FFFFFF",
          fontFamily: font,
          fontSize: 50,
          fontWeight: 600,
          whiteSpace: "nowrap",
        }}
      >
        {label}
      </div>
    </AbsoluteFill>
  );
};
