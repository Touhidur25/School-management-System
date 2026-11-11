/**
 * Student routes: #/classes (default), #/my, #/notifications, #/holds/<id>
 * Admin routes (no link from the student pages): #/admin, #/admin/applications
 */
export function parseRoute(hash) {
  const parts = hash.replace(/^#/, "").split("/").filter(Boolean);
  if (parts[0] === "my") return { name: "my" };
  if (parts[0] === "notifications") return { name: "notifications" };
  if (parts[0] === "admin") {
    return { name: parts[1] === "applications" ? "admin-applications" : "admin-classes" };
  }
  if (parts[0] === "holds" && parts[1]) {
    try {
      return { name: "hold", holdId: decodeURIComponent(parts[1]) };
    } catch {
      return { name: "classes" };
    }
  }
  return { name: "classes" };
}
