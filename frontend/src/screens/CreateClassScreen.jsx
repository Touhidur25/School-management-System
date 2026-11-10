import { useState } from "react";
import { api } from "../api.js";
import { toAsciiDigits } from "../format.js";
import { errorMessage } from "../messages.js";
import Notice from "../components/Notice.jsx";

const MAX_SEATS = 100000;

export default function CreateClassScreen() {
  const [name, setName] = useState("");
  const [seats, setSeats] = useState("");
  const [fieldErrors, setFieldErrors] = useState({});
  const [notice, setNotice] = useState(null);
  const [busy, setBusy] = useState(false);

  function validate() {
    const errors = {};
    const trimmed = name.trim();
    if (!trimmed) errors.name = "ক্লাসের নাম লিখুন।";
    else if (trimmed.length > 200) errors.name = "নাম সর্বোচ্চ 200 অক্ষরের হতে পারে।";

    const digits = toAsciiDigits(seats).trim();
    const count = /^[0-9]+$/.test(digits) ? Number(digits) : NaN;
    if (!Number.isInteger(count) || count < 1 || count > MAX_SEATS) {
      errors.seats = "আসন সংখ্যা 1 থেকে 100000 এর মধ্যে একটি পূর্ণসংখ্যা হতে হবে।";
    }
    return { errors, trimmed, count };
  }

  async function submit(event) {
    event.preventDefault();
    setNotice(null);
    const { errors, trimmed, count } = validate();
    setFieldErrors(errors);
    if (Object.keys(errors).length > 0) return;

    setBusy(true);
    try {
      const created = await api.createClass(trimmed, count);
      setNotice({
        type: "success",
        text: `"${created.name}" ক্লাস তৈরি হয়েছে (আসন: ${created.totalSeats})।`,
      });
      setName("");
      setSeats("");
    } catch (err) {
      setNotice({ type: "error", text: errorMessage(err) });
    } finally {
      setBusy(false);
    }
  }

  return (
    <section>
      <h2>নতুন ক্লাস তৈরি (স্কুল অ্যাডমিন)</h2>
      <p className="muted">প্রতিটি শিফট আলাদা ক্লাস। যেমন "Class 6 – Morning Shift" আর "Class 6 – Day Shift"।</p>

      {notice && (
        <Notice type={notice.type}>
          {notice.text} {notice.type === "success" && <a href="#/admin/applications">সব আবেদন দেখুন</a>}
        </Notice>
      )}

      <form className="card form" onSubmit={submit} noValidate>
        <label htmlFor="class-name">ক্লাসের নাম</label>
        <input
          id="class-name"
          type="text"
          maxLength={200}
          value={name}
          onChange={(e) => setName(e.target.value)}
          aria-invalid={Boolean(fieldErrors.name)}
          placeholder="Class 6 - Morning Shift"
        />
        {fieldErrors.name && <p className="field-error">{fieldErrors.name}</p>}

        <label htmlFor="class-seats">মোট আসন সংখ্যা</label>
        <input
          id="class-seats"
          type="text"
          inputMode="numeric"
          value={seats}
          onChange={(e) => setSeats(e.target.value)}
          aria-invalid={Boolean(fieldErrors.seats)}
          placeholder="120"
        />
        {fieldErrors.seats && <p className="field-error">{fieldErrors.seats}</p>}

        <button type="submit" className="primary" disabled={busy}>
          {busy ? "তৈরি হচ্ছে…" : "ক্লাস তৈরি করুন"}
        </button>
      </form>
    </section>
  );
}
