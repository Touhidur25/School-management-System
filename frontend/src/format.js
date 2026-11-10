export const STATUS_LABELS = {
  HELD: "চলমান (আসন ধরা আছে)",
  CONFIRMED: "ভর্তি নিশ্চিত",
  EXPIRED: "মেয়াদ শেষ",
  CANCELLED: "বাতিল",
};

const BENGALI_DIGITS = "০১২৩৪৫৬৭৮৯";

/** "২০১৫..." -> "2015...", so students can type with a Bengali keyboard. */
export function toAsciiDigits(text) {
  return text.replace(/[০-৯]/g, (d) => String(BENGALI_DIGITS.indexOf(d)));
}

export function isValidStudentId(value) {
  return /^[0-9]{17}$/.test(value ?? "");
}

/** 125000 ms -> "02:05"; an hour or more -> "1:02:05". */
export function formatRemaining(ms) {
  const total = Math.max(0, Math.ceil(ms / 1000));
  const hours = Math.floor(total / 3600);
  const minutes = Math.floor((total % 3600) / 60);
  const seconds = total % 60;
  const mm = String(minutes).padStart(2, "0");
  const ss = String(seconds).padStart(2, "0");
  return hours > 0 ? `${hours}:${mm}:${ss}` : `${mm}:${ss}`;
}

/**
 * The status to show. A HELD application whose time has run out is shown as EXPIRED
 * straight away, without waiting for the next refresh from the server.
 */
export function effectiveStatus(status, expiresAt, nowMs) {
  return status === "HELD" && Date.parse(expiresAt) <= nowMs ? "EXPIRED" : status;
}
