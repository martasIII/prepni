import { colors, font } from "./theme";

/** Ikonka „5 / ř“ stejně jako v aplikaci (assets/prepni.svg). */
export const Icon: React.FC<{ size: number }> = ({ size }) => (
  <svg width={size} height={size} viewBox="0 0 32 32">
    <rect x="1" y="1" width="30" height="30" rx="7" fill={colors.frame} />
    <rect x="4.5" y="4" width="23" height="23" rx="4" fill={colors.face} />
    <path d="M6 25.5 L26 5.5" stroke={colors.frame} strokeWidth="1.2" />
    <text x="10.5" y="15" textAnchor="middle" fontFamily={font} fontSize="10.5" fontWeight="600" fill={colors.digit}>
      5
    </text>
    <text x="21.5" y="24.5" textAnchor="middle" fontFamily={font} fontSize="10.5" fontWeight="600" fill={colors.letter}>
      ř
    </text>
  </svg>
);

/** Klávesa Shift; <c>pressed</c> 0 až 1 ji promáčkne. */
export const ShiftKey: React.FC<{ pressed: number; scale?: number; label?: string }> = ({ pressed, scale = 1, label = "Shift" }) => (
  <div
    style={{
      display: "flex",
      alignItems: "center",
      gap: 14 * scale,
      padding: `${18 * scale}px ${34 * scale}px`,
      borderRadius: 16 * scale,
      background: "#FFFFFF",
      border: `${2 * scale}px solid ${colors.frame}`,
      borderBottomWidth: (8 - 5 * pressed) * scale,
      transform: `translateY(${5 * pressed * scale}px)`,
      color: colors.digit,
      fontFamily: font,
      fontSize: 44 * scale,
      fontWeight: 600,
    }}
  >
    <span style={{ fontSize: 50 * scale }}>⇧</span>
    {label}
  </div>
);
