import { useEffect, useState } from "react";
import { api } from "../api.js";
import { effectiveStatus, formatRemaining } from "../format.js";
import { useNow, usePolling } from "../hooks.js";
import { errorMessage } from "../messages.js";
import Notice from "../components/Notice.jsx";
import StatusBadge from "../components/StatusBadge.jsx";

export default function HoldScreen({ holdId, studentId }) {
  const { data, error, loading, refresh } = usePolling(async () => {
    const hold = await api.getHold(holdId);
    const cls = await api.getClass(hold.classId);
    return { hold, cls };
  }, [holdId]);
  const now = useNow();
  const [busy, setBusy] = useState(false);
  const [notice, setNotice] = useState(null);

  const hold = data?.hold;
  const status = hold ? effectiveStatus(hold.status, hold.expiresAt, now) : null;
  const remainingMs = hold ? Math.max(0, Date.parse(hold.expiresAt) - now) : 0;
  const expiredLocally = hold?.status === "HELD" && status === "EXPIRED";

  // The moment the countdown ends, ask the server so everything agrees.
  useEffect(() => {
    if (expiredLocally) refresh();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [expiredLocally]);

  async function confirm() {
    setBusy(true);
    setNotice(null);
    try {
      await api.confirmHold(holdId, studentId);
      setNotice({ type: "success", text: "ভর্তি ফি জমা হয়েছে। আপনার ভর্তি নিশ্চিত হয়েছে।" });
    } catch (err) {
      setNotice({ type: "error", text: errorMessage(err) });
    } finally {
      setBusy(false);
      refresh();
    }
  }

  async function cancel() {
    if (!window.confirm("আবেদনটি বাতিল করতে চান? আসনটি অন্যদের জন্য খুলে যাবে।")) return;
    setBusy(true);
    setNotice(null);
    try {
      await api.cancelHold(holdId, studentId);
      setNotice({ type: "info", text: "আবেদন বাতিল করা হয়েছে।" });
    } catch (err) {
      setNotice({ type: "error", text: errorMessage(err) });
    } finally {
      setBusy(false);
      refresh();
    }
  }

  const canAct = status === "HELD" && Boolean(studentId) && !busy;

  return (
    <section>
      <h2>আবেদনের বিবরণ</h2>

      {loading && !data && !error && <p>লোড হচ্ছে…</p>}

      {error && !data && (
        <Notice type="error">
          {errorMessage(error)}{" "}
          {error.code === "hold_not_found" ? (
            <a href="#/classes">ক্লাস তালিকায় ফিরে যান</a>
          ) : (
            <button type="button" onClick={refresh}>
              আবার চেষ্টা করুন
            </button>
          )}
        </Notice>
      )}

      {error && data && (
        <Notice type="warning">সার্ভারের সাথে যোগাযোগ করা যাচ্ছে না। সর্বশেষ পাওয়া তথ্য দেখানো হচ্ছে।</Notice>
      )}

      {notice && <Notice type={notice.type}>{notice.text}</Notice>}

      {hold && (
        <div className="card">
          <dl className="details">
            <div>
              <dt>ক্লাস</dt>
              <dd>{data.cls.name}</dd>
            </div>
            <div>
              <dt>অবস্থা</dt>
              <dd>
                <StatusBadge status={status} />
              </dd>
            </div>
            {status === "HELD" && (
              <div>
                <dt>বাকি সময়</dt>
                <dd className="countdown" data-testid="countdown">
                  {formatRemaining(remainingMs)}
                </dd>
              </div>
            )}
            <div>
              <dt>মেয়াদ শেষ হবে</dt>
              <dd>{new Date(hold.expiresAt).toLocaleTimeString()}</dd>
            </div>
          </dl>

          {status === "HELD" && !studentId && (
            <Notice type="info">ভর্তি ফি দিতে বা বাতিল করতে উপরের ঘরে আবেদনকারীর জন্ম নিবন্ধন নম্বর দিন।</Notice>
          )}

          {status === "EXPIRED" && (
            <Notice type="warning">
              মেয়াদ শেষ হয়ে গেছে, আসনটি অন্যদের জন্য খুলে দেওয়া হয়েছে। <a href="#/classes">আবার আবেদন করুন</a>
            </Notice>
          )}

          {status === "CONFIRMED" && <Notice type="success">এই ভর্তি নিশ্চিত হয়েছে। আর কিছু করার দরকার নেই।</Notice>}

          {status === "CANCELLED" && (
            <Notice type="info">
              এই আবেদন বাতিল করা হয়েছে। <a href="#/classes">ক্লাস তালিকায় ফিরে যান</a>
            </Notice>
          )}

          <div className="actions">
            <button type="button" className="primary" disabled={!canAct} onClick={confirm}>
              {busy ? "অপেক্ষা করুন…" : "ভর্তি ফি দিন"}
            </button>
            <button type="button" className="danger" disabled={!canAct} onClick={cancel}>
              বাতিল
            </button>
          </div>
        </div>
      )}
    </section>
  );
}
