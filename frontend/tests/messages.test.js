import { test } from "node:test";
import assert from "node:assert/strict";
import { errorMessage, notificationText } from "../src/messages.js";

const BENGALI = /[\u0980-\u09FF]/;

// Every error code the backend can send, plus network_error which the UI raises itself.
// Keep in sync with the `new ApiException(...)` calls in the backend.
const BACKEND_CODES = [
  "invalid_name",
  "invalid_seats",
  "missing_student_id",
  "invalid_student_id",
  "invalid_request",
  "class_not_found",
  "hold_not_found",
  "notification_not_found",
  "already_applied",
  "no_seats",
  "hold_expired",
  "hold_cancelled",
  "already_confirmed",
  "not_your_hold",
  "not_your_notification",
  "server_error",
  "network_error",
];

test("every backend error code has its own Bengali message", () => {
  const fallback = errorMessage({ code: "something_new" });
  for (const code of BACKEND_CODES) {
    const text = errorMessage({ code });
    assert.match(text, BENGALI, `${code} has no Bengali message`);
    assert.notEqual(text, fallback, `${code} falls back to the generic message`);
  }
});

test("an unknown error shows a generic Bengali message, never raw text or a blank", () => {
  for (const error of [{ code: "weird" }, {}, null, undefined, new Error("boom")]) {
    assert.match(errorMessage(error), BENGALI);
  }
});

test("a cancelled admission notification names the class and says admission was cancelled", () => {
  const text = notificationText({ className: "Class 6 - Morning Shift", previousStatus: "CONFIRMED" });
  assert.ok(text.includes("Class 6 - Morning Shift"));
  assert.ok(text.includes("ভর্তি বাতিল"));
});

test("a cancelled application notification says the application was cancelled", () => {
  const text = notificationText({ className: "Class 7", previousStatus: "HELD" });
  assert.ok(text.includes("Class 7"));
  assert.ok(text.includes("আবেদন বাতিল"));
});
