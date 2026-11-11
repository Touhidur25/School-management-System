import { test } from "node:test";
import assert from "node:assert/strict";
import { parseRoute } from "../src/routes.js";

test("student pages", () => {
  assert.deepEqual(parseRoute(""), { name: "classes" });
  assert.deepEqual(parseRoute("#/classes"), { name: "classes" });
  assert.deepEqual(parseRoute("#/my"), { name: "my" });
  assert.deepEqual(parseRoute("#/notifications"), { name: "notifications" });
});

test("application page carries the hold id", () => {
  assert.deepEqual(parseRoute("#/holds/h_123"), { name: "hold", holdId: "h_123" });
  assert.deepEqual(parseRoute("#/holds/h%5F9"), { name: "hold", holdId: "h_9" });
});

test("admin pages are only reachable through their own addresses", () => {
  assert.deepEqual(parseRoute("#/admin"), { name: "admin-classes" });
  assert.deepEqual(parseRoute("#/admin/applications"), { name: "admin-applications" });
});

test("students have no route to class creation any more: the old address shows the class list", () => {
  assert.deepEqual(parseRoute("#/new-class"), { name: "classes" });
});

test("bad or unknown addresses fall back to the class list instead of breaking", () => {
  assert.deepEqual(parseRoute("#/holds"), { name: "classes" });
  assert.deepEqual(parseRoute("#/holds/%E0%A4%A"), { name: "classes" }); // malformed escape
  assert.deepEqual(parseRoute("#/nothing/here"), { name: "classes" });
});
