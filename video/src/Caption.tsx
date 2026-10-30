import { AbsoluteFill, interpolate, useCurrentFrame } from "remotion";
import { ShiftKey } from "./Icon";
import { colors, font } from "./theme";

/** Titulek k záznamu: „shift“ ukáže dvě klávesy Shift, jinak text na štítku. */
export type CaptionSpec = { from: number; to: number; text: string };

export const Caption: React.FC<{ text: string; frames: number }> = ({ text, frames }) => {
  const frame = useCurrentFrame();
  const opacity = interpolate(frame, [0, 6, frames - 6, frames], [0, 1, 1, 0], { extrapolateLeft: "clamp", extrapolateRight: "clamp" });
  const tap = (at: number) => interpolate(frame, [at, at + 3, at + 7], [0, 1, 0], { extrapolateLeft: "clamp", extrapolateRight: "clamp" });

  return (
    <AbsoluteFill style={{ alignItems: "center", justifyContent: "center", paddingTop: 140, opacity }}>
      {text === "shift" ? (
        <div style={{ display: "flex", alignItems: "center", gap: 28, fontFamily: font, fontSize: 64, color: colors.frame, fontWeight: 600 }}>
          <ShiftKey pressed={tap(6)} />
          +
          <ShiftKey pressed={tap(14)} />
        </div>
      ) : (
        <div
          style={{
            padding: "22px 48px",
            borderRadius: 18,
            background: colors.frame,
            color: "#FFFFFF",
            fontFamily: font,
            fontSize: 60,
            fontWeight: 600,
          }}
        >
          {text}
        </div>
      )}
    </AbsoluteFill>
  );
};
