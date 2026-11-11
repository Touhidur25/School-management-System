import { API_URL } from "./config.js";

// Difference between the server's clock and this browser's clock, learned from the
// `Date` header of every response. Used so the countdown matches the server's expiry
// even when the computer's clock is a little off.
let clockOffsetMs = 0;
export const serverNow = () => Date.now() + clockOffsetMs;

export class ApiError extends Error {
  constructor(status, code, message) {
    super(message || code);
    this.status = status;
    this.code = code;
  }
}

async function request(method, path, { studentId, body } = {}) {
  const headers = {};
  if (studentId) headers["X-Student-Id"] = studentId;
  if (body !== undefined) headers["Content-Type"] = "application/json";

  let response;
  try {
    response = await fetch(`${API_URL}${path}`, {
      method,
      headers,
      body: body === undefined ? undefined : JSON.stringify(body),
      cache: "no-store",
    });
  } catch {
    throw new ApiError(0, "network_error", "Could not reach the server.");
  }

  const serverDate = Date.parse(response.headers.get("Date") ?? "");
  if (!Number.isNaN(serverDate)) clockOffsetMs = serverDate - Date.now();

  if (response.status === 204) return null;

  let data = null;
  try {
    data = await response.json();
  } catch {
    // Not JSON (for example an error page from a proxy); handled below.
  }

  if (!response.ok) {
    throw new ApiError(
      response.status,
      data?.error?.code ?? "unknown_error",
      data?.error?.message
    );
  }
  return data;
}

const enc = encodeURIComponent;

export const api = {
  listClasses: () => request("GET", "/classes"),
  getClass: (classId) => request("GET", `/classes/${enc(classId)}`),
  createClass: (name, totalSeats) => request("POST", "/classes", { body: { name, totalSeats } }),
  applyForSeat: (classId, studentId) => request("POST", `/classes/${enc(classId)}/holds`, { studentId }),
  getHold: (holdId) => request("GET", `/holds/${enc(holdId)}`),
  confirmHold: (holdId, studentId) => request("POST", `/holds/${enc(holdId)}/confirm`, { studentId }),
  cancelHold: (holdId, studentId) => request("DELETE", `/holds/${enc(holdId)}`, { studentId }),
  myHolds: (studentId) => request("GET", "/me/holds", { studentId }),
  adminCancelHold: (holdId) => request("POST", `/admin/holds/${enc(holdId)}/cancel`),
  notifications: (studentId) => request("GET", "/me/notifications", { studentId }),
  markNotificationRead: (notificationId, studentId) =>
    request("POST", `/me/notifications/${enc(notificationId)}/read`, { studentId }),
  markAllNotificationsRead: (studentId) => request("POST", "/me/notifications/read-all", { studentId }),
  adminHolds: (classId) =>
    request("GET", classId ? `/admin/holds?classId=${enc(classId)}` : "/admin/holds"),
};
