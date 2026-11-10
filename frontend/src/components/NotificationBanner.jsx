import { notificationText } from "../messages.js";

/** Shown on top of every student page while there is an unread notification. */
export default function NotificationBanner({ unread, onDismiss }) {
  if (unread.length === 0) return null;
  const latest = unread[0];

  return (
    <div className="notice notice-error notification-banner" role="alert" data-testid="notification-banner">
      <strong>নতুন বিজ্ঞপ্তি:</strong> {notificationText(latest)}
      {unread.length > 1 && <span> (আরও {unread.length - 1}টি নতুন বিজ্ঞপ্তি আছে)</span>}
      <div className="banner-actions">
        <button type="button" onClick={() => onDismiss(latest)}>
          ঠিক আছে
        </button>{" "}
        <a href="#/notifications">সব বিজ্ঞপ্তি দেখুন</a>
      </div>
    </div>
  );
}
