import { notificationText } from "../messages.js";
import Notice from "../components/Notice.jsx";

const formatDateTime = (iso) =>
  new Date(iso).toLocaleString([], { dateStyle: "medium", timeStyle: "medium" });

export default function NotificationsScreen({ studentId, notifications, onRead, onReadAll }) {
  const unreadCount = notifications.filter((n) => !n.readAt).length;

  return (
    <section>
      <h2>বিজ্ঞপ্তি</h2>

      {!studentId && (
        <Notice type="info">বিজ্ঞপ্তি দেখতে উপরের ঘরে শিক্ষার্থীর 17 অঙ্কের জন্ম নিবন্ধন নম্বর দিন।</Notice>
      )}

      {studentId && notifications.length === 0 && <p>কোনো বিজ্ঞপ্তি নেই।</p>}

      {studentId && unreadCount > 1 && (
        <p>
          <button type="button" onClick={onReadAll}>
            সবগুলো পড়া হয়েছে
          </button>
        </p>
      )}

      {studentId && notifications.length > 0 && (
        <ul className="cards">
          {notifications.map((n) => (
            <li key={n.notificationId} className={n.readAt ? "card" : "card unread"} data-read={n.readAt ? "yes" : "no"}>
              <p className="notification-text">{notificationText(n)}</p>
              <p className="muted">{formatDateTime(n.createdAt)}</p>
              <p>
                <a href={`#/holds/${encodeURIComponent(n.holdId)}`}>আবেদনের বিবরণ</a>{" "}
                {!n.readAt && (
                  <button type="button" onClick={() => onRead(n)}>
                    পড়া হয়েছে
                  </button>
                )}
              </p>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
