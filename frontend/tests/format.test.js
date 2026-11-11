import { test } from "node:test";
import assert from "node:assert/strict";
import {
  STATUS_LABELS,
  effectiveStatus,
  formatRemaining,
  isValidStudentId,
  toAsciiDigits,
} from "../src/format.js";

test("toAsciiDigits converts Bengali digits and leaves everything else alone", () => {
  assert.equal(toAsciiDigits("২০১৫২৬৯১২৩৪৫৬৭৮৯০"), "20152691234567890");
  assert.equal(toAsciiDigits("১২ab৩"), "12ab3");
  assert.equal(toAsciiDigits("2015"), "2015");
});

test("isValidStudentId accepts exactly 17 ASCII digits", () => {
  assert.equal(isValidStudentId("20152691234567890"), true);
  assert.equal(isValidStudentId("2015269123456789"), false); // 16
  assert.equal(isValidStudentId("201526912345678901"), false); // 18
  assert.equal(isValidStudentId("2015269123456789a"), false);
  assert.equal(isValidStudentId(""), false);
  assert.equal(isValidStudentId(null), false);
  assert.equal(isValidStudentId(undefined), false);
});

test("formatRemaining shows mm:ss, and h:mm:ss from one hour", () => {
  assert.equal(formatRemaining(0), "00:00");
  assert.equal(formatRemaining(-5000), "00:00");
  assert.equal(formatRemaining(125000), "02:05");
  assert.equal(formatRemaining(600000), "10:00");
  assert.equal(formatRemaining(3725000), "1:02:05");
});

test("formatRemaining rounds partial seconds up, so 0 is only shown when time is really up", () => {
  assert.equal(formatRemaining(1), "00:01");
  assert.equal(formatRemaining(59001), "01:00");
});

test("effectiveStatus turns a HELD application past its time into EXPIRED", () => {
  const expiresAt = "2026-11-01T04:10:00Z";
  const at = Date.parse(expiresAt);
  assert.equal(effectiveStatus("HELD", expiresAt, at - 1), "HELD");
  assert.equal(effectiveStatus("HELD", expiresAt, at), "EXPIRED");
  assert.equal(effectiveStatus("HELD", expiresAt, at + 60000), "EXPIRED");
});

test("effectiveStatus never changes the other statuses", () => {
  const expiresAt = "2026-11-01T04:10:00Z";
  const later = Date.parse(expiresAt) + 86400000;
  for (const status of ["CONFIRMED", "CANCELLED", "EXPIRED"]) {
    assert.equal(effectiveStatus(status, expiresAt, later), status);
  }
});

test("every status has a label", () => {
  for (const status of ["HELD", "CONFIRMED", "EXPIRED", "CANCELLED"]) {
    assert.ok(STATUS_LABELS[status], `missing label for ${status}`);
  }
});
