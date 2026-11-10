import { useState } from "react";
import { isValidStudentId, toAsciiDigits } from "../format.js";

/** The student's identity: their 17-digit birth registration number, shown at the top of every page. */
export default function StudentIdBar({ studentId, onChange }) {
  const [draft, setDraft] = useState(studentId ?? "");

  function handleChange(event) {
    const digits = toAsciiDigits(event.target.value).replace(/[^0-9]/g, "").slice(0, 17);
    setDraft(digits);
    onChange(isValidStudentId(digits) ? digits : null);
  }

  return (
    <div className="student-bar">
      <label htmlFor="student-id">শিক্ষার্থীর জন্ম নিবন্ধন নম্বর</label>
      <input
        id="student-id"
        type="text"
        inputMode="numeric"
        autoComplete="off"
        placeholder="17 অঙ্কের নম্বর"
        value={draft}
        onChange={handleChange}
      />
      <span className={studentId ? "hint hint-ok" : "hint"} aria-live="polite">
        {studentId ? "✓ নম্বর সংরক্ষিত" : `${draft.length}/17 অঙ্ক`}
      </span>
    </div>
  );
}
