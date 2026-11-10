import { useState } from "react";
import { api } from "../api.js";
import { effectiveStatus, formatRemaining, STATUS_LABELS } from "../format.js";
import { useNow, usePolling } from "../hooks.js";
import { errorMessage } from "../messages.js";
import Notice from "../components/Notice.jsx";
import StatusBadge from "../components/StatusBadge.jsx";

const formatDateTime = (iso) =>
  new Date(iso).toLocaleString([], { dateStyle: "medium", timeStyle: "medium" });

/** Admin: every application - who applied where, when, and what became of it. */
export default function AdminApplicationsScreen() {
  const [classId, setClassId] = useState("");
  const { data: classes } = usePolling(() => api.listClasses(), []);
  const { data: rows, error, loading, refresh } = usePolling(
    () => api.adminHolds(classId || undefined),
    [classId]
  );
  const now = useNow();
  const [notice, setNotice] = useState(null);
  const [cancellingId, setCancellingId] = useState(null);

  async function cancelAdmission(row) {
    const what = row.shownStatus === "CONFIRMED" ? "ভর্তি" : "আবেদন";
    const ok = window.confirm(
      `${what} বাতিল করবেন?\nশিক্ষার্থী: ${row.studentId}\nক্লাস: ${row.className}\n\nআসনটি খালি হয়ে যাবে এবং শিক্ষার্থীকে বিজ্ঞপ্তি পাঠানো হবে।`
    );
    if (!ok) return;

    setNotice(null);
    setCancellingId(row.holdId);
    try {
      await api.adminCancelHold(row.holdId);
      setNotice({ type: "success", text: `${what} বাতিল করা হয়েছে। শিক্ষার্থীকে বিজ্ঞপ্তি পাঠানো হয়েছে।` });
    } catch (err) {
      setNotice({ type: "error", text: errorMessage(err) });
    } finally {
      setCancellingId(null);
      refresh();
    }
  }

  const shown = (rows ?? []).map((r) => ({ ...r, shownStatus: effectiveStatus(r.status, r.expiresAt, now) }));
  const count = (status) => shown.filter((r) => r.shownStatus === status).length;

  return (
    <section>
      <h2>সব আবেদন</h2>
      <p className="muted">
        কোন শিক্ষার্থী কোন ক্লাসে কখন আবেদন করেছে এবং এখন কী অবস্থায় আছে। মেয়াদ শেষ হওয়া (ভর্তি ফি না দেওয়া) এবং
        বাতিল আবেদনও এখানে দেখা যায়। তালিকা নিজে থেকেই হালনাগাদ হয়।
      </p>

      <div className="filter">
        <label htmlFor="class-filter">ক্লাস</label>
        <select id="class-filter" value={classId} onChange={(e) => setClassId(e.target.value)}>
          <option value="">সব ক্লাস</option>
          {(classes ?? []).map((c) => (
            <option key={c.id} value={c.id}>
              {c.name}
            </option>
          ))}
        </select>
      </div>

      {notice && <Notice type={notice.type}>{notice.text}</Notice>}

      {error && rows && (
        <Notice type="warning">সার্ভারের সাথে যোগাযোগ করা যাচ্ছে না। সর্বশেষ পাওয়া তথ্য দেখানো হচ্ছে।</Notice>
      )}

      {error && !rows && (
        <Notice type="error">
          {errorMessage(error)}{" "}
          <button type="button" onClick={refresh}>
            আবার চেষ্টা করুন
          </button>
        </Notice>
      )}

      {loading && !rows && !error && <p>লোড হচ্ছে…</p>}

      {rows && (
        <>
          <ul className="summary" aria-label="সারসংক্ষেপ">
            <li>
              মোট আবেদন <strong data-testid="count-total">{shown.length}</strong>
            </li>
            {["HELD", "CONFIRMED", "EXPIRED", "CANCELLED"].map((status) => (
              <li key={status}>
                {STATUS_LABELS[status]} <strong data-testid={`count-${status}`}>{count(status)}</strong>
              </li>
            ))}
          </ul>

          {shown.length === 0 ? (
            <p>এখনো কোনো আবেদন নেই।</p>
          ) : (
            <div className="table-wrap">
              <table>
                <thead>
                  <tr>
                    <th>জন্ম নিবন্ধন নম্বর</th>
                    <th>ক্লাস</th>
                    <th>অবস্থা</th>
                    <th>আবেদনের সময়</th>
                    <th>বাকি সময় / ভর্তি নিশ্চিতের সময়</th>
                    <th>অ্যাকশন</th>
                  </tr>
                </thead>
                <tbody>
                  {shown.map((r) => (
                    <tr key={r.holdId} data-status={r.shownStatus}>
                      <td className="mono">{r.studentId}</td>
                      <td>{r.className}</td>
                      <td>
                        <StatusBadge status={r.shownStatus} />
                      </td>
                      <td>{formatDateTime(r.appliedAt)}</td>
                      <td>
                        {r.shownStatus === "HELD" && formatRemaining(Date.parse(r.expiresAt) - now)}
                        {r.shownStatus === "CONFIRMED" && r.confirmedAt && formatDateTime(r.confirmedAt)}
                      </td>
                      <td>
                        {(r.shownStatus === "CONFIRMED" || r.shownStatus === "HELD") && (
                          <button
                            type="button"
                            className="danger"
                            disabled={cancellingId !== null}
                            onClick={() => cancelAdmission(r)}
                          >
                            {cancellingId === r.holdId
                              ? "অপেক্ষা করুন…"
                              : r.shownStatus === "CONFIRMED"
                                ? "ভর্তি বাতিল"
                                : "আবেদন বাতিল"}
                          </button>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </>
      )}
    </section>
  );
}
