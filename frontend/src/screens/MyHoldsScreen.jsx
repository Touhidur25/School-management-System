import { api } from "../api.js";
import { effectiveStatus, formatRemaining } from "../format.js";
import { useNow, usePolling } from "../hooks.js";
import { errorMessage } from "../messages.js";
import Notice from "../components/Notice.jsx";
import StatusBadge from "../components/StatusBadge.jsx";

export default function MyHoldsScreen({ studentId }) {
  const { data: holds, error, loading, refresh } = usePolling(
    () => (studentId ? api.myHolds(studentId) : Promise.resolve([])),
    [studentId]
  );
  const now = useNow();

  return (
    <section>
      <h2>আমার আবেদন</h2>

      {!studentId && (
        <Notice type="info">আপনার আবেদনগুলো দেখতে উপরের ঘরে শিক্ষার্থীর 17 অঙ্কের জন্ম নিবন্ধন নম্বর দিন।</Notice>
      )}

      {studentId && error && holds && (
        <Notice type="warning">সার্ভারের সাথে যোগাযোগ করা যাচ্ছে না। সর্বশেষ পাওয়া তথ্য দেখানো হচ্ছে।</Notice>
      )}

      {studentId && error && !holds && (
        <Notice type="error">
          {errorMessage(error)}{" "}
          <button type="button" onClick={refresh}>
            আবার চেষ্টা করুন
          </button>
        </Notice>
      )}

      {studentId && loading && !holds && !error && <p>লোড হচ্ছে…</p>}

      {studentId && holds && holds.length === 0 && (
        <p>
          এখনো কোনো আবেদন নেই। <a href="#/classes">ক্লাস তালিকা দেখুন</a>।
        </p>
      )}

      {studentId && holds && holds.length > 0 && (
        <ul className="cards">
          {holds.map((h) => {
            const status = effectiveStatus(h.status, h.expiresAt, now);
            return (
              <li key={h.holdId} className="card">
                <h3>{h.className}</h3>
                <p>
                  <StatusBadge status={status} />
                  {status === "HELD" && (
                    <span className="countdown-inline">
                      {" "}
                      বাকি সময়: {formatRemaining(Date.parse(h.expiresAt) - now)}
                    </span>
                  )}
                </p>
                <a className="button-link" href={`#/holds/${encodeURIComponent(h.holdId)}`}>
                  বিস্তারিত দেখুন
                </a>
              </li>
            );
          })}
        </ul>
      )}
    </section>
  );
}
