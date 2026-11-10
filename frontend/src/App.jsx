import { api } from "./api.js";
import ErrorBoundary from "./components/ErrorBoundary.jsx";
import NotificationBanner from "./components/NotificationBanner.jsx";
import StudentIdBar from "./components/StudentIdBar.jsx";
import { useNotifications, useStudentId } from "./hooks.js";
import { useRoute } from "./router.js";
import AdminApplicationsScreen from "./screens/AdminApplicationsScreen.jsx";
import ClassListScreen from "./screens/ClassListScreen.jsx";
import CreateClassScreen from "./screens/CreateClassScreen.jsx";
import HoldScreen from "./screens/HoldScreen.jsx";
import MyHoldsScreen from "./screens/MyHoldsScreen.jsx";
import NotificationsScreen from "./screens/NotificationsScreen.jsx";

// Students can only apply. Class creation, the all-applications list and cancelling an
// admission live on the admin pages, which the student pages never link to.
const ADMIN_TABS = [
  { name: "admin-classes", href: "#/admin", label: "ক্লাস তৈরি" },
  { name: "admin-applications", href: "#/admin/applications", label: "সব আবেদন" },
];

export default function App() {
  const route = useRoute();
  const [studentId, setStudentId] = useStudentId();
  const isAdmin = route.name.startsWith("admin");
  const notifications = useNotifications(isAdmin ? null : studentId);

  async function markRead(notification) {
    try {
      await api.markNotificationRead(notification.notificationId, studentId);
    } catch {
      // Not critical; it simply stays unread and is shown again.
    }
    notifications.refresh();
  }

  async function markAllRead() {
    try {
      await api.markAllNotificationsRead(studentId);
    } catch {
      // Same as above.
    }
    notifications.refresh();
  }

  const unreadCount = notifications.unread.length;
  const tabs = isAdmin
    ? ADMIN_TABS
    : [
        { name: "classes", href: "#/classes", label: "ক্লাস তালিকা" },
        { name: "my", href: "#/my", label: "আমার আবেদন" },
        {
          name: "notifications",
          href: "#/notifications",
          label: unreadCount > 0 ? `বিজ্ঞপ্তি (${unreadCount})` : "বিজ্ঞপ্তি",
        },
      ];

  let screen;
  switch (route.name) {
    case "my":
      screen = <MyHoldsScreen studentId={studentId} />;
      break;
    case "notifications":
      screen = (
        <NotificationsScreen
          studentId={studentId}
          notifications={notifications.list}
          onRead={markRead}
          onReadAll={markAllRead}
        />
      );
      break;
    case "hold":
      screen = <HoldScreen key={route.holdId} holdId={route.holdId} studentId={studentId} />;
      break;
    case "admin-classes":
      screen = <CreateClassScreen />;
      break;
    case "admin-applications":
      screen = <AdminApplicationsScreen />;
      break;
    default:
      screen = <ClassListScreen studentId={studentId} />;
  }

  return (
    <div className="app">
      <header className="app-header">
        <h1>{isAdmin ? "স্কুল ভর্তি ব্যবস্থা - অ্যাডমিন" : "স্কুল ভর্তি ব্যবস্থা"}</h1>
      </header>

      {!isAdmin && <StudentIdBar studentId={studentId} onChange={setStudentId} />}
      {!isAdmin && <NotificationBanner unread={notifications.unread} onDismiss={markRead} />}

      <nav className="tabs" aria-label="প্রধান মেনু">
        {tabs.map((tab) => (
          <a
            key={tab.name}
            href={tab.href}
            className={route.name === tab.name ? "tab active" : "tab"}
            aria-current={route.name === tab.name ? "page" : undefined}
          >
            {tab.label}
          </a>
        ))}
      </nav>

      <main>
        <ErrorBoundary key={route.name + (route.holdId ?? "")}>{screen}</ErrorBoundary>
      </main>
    </div>
  );
}
