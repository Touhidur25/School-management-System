import { useState } from "react";
import { api } from "../api.js";
import { usePolling } from "../hooks.js";
import { errorMessage } from "../messages.js";
import { navigate } from "../router.js";
import Notice from "../components/Notice.jsx";

export default function ClassListScreen({ studentId }) {
  // Seat counts refresh by themselves every few seconds.
  const { data: classes, error, loading, refresh } = usePolling(() => api.listClasses(), []);
  const [applyingId, setApplyingId] = useState(null);
  const [notice, setNotice] = useState(null);

  async function apply(cls) {
    setNotice(null);
    setApplyingId(cls.id);
    try {
      const hold = await api.applyForSeat(cls.id, studentId);
      navigate(`/holds/${hold.holdId}`);
    } catch (err) {
      setNotice({ type: "error", text: errorMessage(err), code: err.code });
      refresh();
    } finally {
      setApplyingId(null);
    }
  }

  return (
    <section>
      <h2>ক্লাস তালিকা</h2>

      {!studentId && (
        <Notice type="info">আবেদন করতে আগে উপরের ঘরে শিক্ষার্থীর 17 অঙ্কের জন্ম নিবন্ধন নম্বর দিন।</Notice>
      )}

      {notice && (
        <Notice type={notice.type}>
          {notice.text} {notice.code === "already_applied" && <a href="#/my">আমার আবেদন দেখুন</a>}
        </Notice>
      )}

      {error && classes && (
        <Notice type="warning">সার্ভারের সাথে যোগাযোগ করা যাচ্ছে না। সর্বশেষ পাওয়া তথ্য দেখানো হচ্ছে।</Notice>
      )}

      {error && !classes && (
        <Notice type="error">
          {errorMessage(error)}{" "}
          <button type="button" onClick={refresh}>
            আবার চেষ্টা করুন
          </button>
        </Notice>
      )}

      {loading && !classes && !error && <p>লোড হচ্ছে…</p>}

      {classes && classes.length === 0 && (
        <p>এখনো কোনো ক্লাস চালু হয়নি। পরে আবার দেখুন।</p>
      )}

      {classes && classes.length > 0 && (
        <ul className="cards">
          {classes.map((c) => {
            const full = c.availableSeats <= 0;
            return (
              <li key={c.id} className="card">
                <h3>{c.name}</h3>
                <dl className="stats">
                  <div>
                    <dt>মোট আসন</dt>
                    <dd>{c.totalSeats}</dd>
                  </div>
                  <div>
                    <dt>আবেদনে ধরা আছে</dt>
                    <dd>{c.heldSeats}</dd>
                  </div>
                  <div>
                    <dt>ভর্তি নিশ্চিত</dt>
                    <dd>{c.confirmedSeats}</dd>
                  </div>
                  <div>
                    <dt>খালি আসন</dt>
                    <dd className={full ? "zero" : "available"}>{c.availableSeats}</dd>
                  </div>
                </dl>
                <button
                  type="button"
                  className="primary"
                  disabled={!studentId || full || applyingId !== null}
                  onClick={() => apply(c)}
                >
                  {applyingId === c.id ? "আবেদন হচ্ছে…" : full ? "আসন নেই" : "আবেদন করুন"}
                </button>
              </li>
            );
          })}
        </ul>
      )}
    </section>
  );
}
