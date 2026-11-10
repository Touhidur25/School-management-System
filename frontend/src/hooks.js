import { useCallback, useEffect, useRef, useState } from "react";
import { api, serverNow } from "./api.js";
import { isValidStudentId } from "./format.js";

/**
 * Loads data now and then every `intervalMs`, and again when the tab becomes visible.
 * Keeps showing the last good data if a later refresh fails. Re-starts when `deps` change.
 */
export function usePolling(fetcher, deps, intervalMs = 3000) {
  const [state, setState] = useState({ data: null, error: null, loading: true });
  const fetcherRef = useRef(fetcher);
  fetcherRef.current = fetcher;
  const generation = useRef(0);

  const refresh = useCallback(async () => {
    const mine = generation.current;
    try {
      const data = await fetcherRef.current();
      if (mine === generation.current) setState({ data, error: null, loading: false });
    } catch (error) {
      if (mine === generation.current) {
        setState((previous) => ({ data: previous.data, error, loading: false }));
      }
    }
  }, []);

  useEffect(() => {
    generation.current += 1;
    setState({ data: null, error: null, loading: true });
    refresh();
    const timer = setInterval(refresh, intervalMs);
    const onVisible = () => {
      if (document.visibilityState === "visible") refresh();
    };
    document.addEventListener("visibilitychange", onVisible);
    return () => {
      generation.current += 1; // ignore answers that arrive after this point
      clearInterval(timer);
      document.removeEventListener("visibilitychange", onVisible);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, deps);

  return { ...state, refresh };
}

/** Server-aligned current time in ms, updated every second (drives the countdowns). */
export function useNow(intervalMs = 1000) {
  const [now, setNow] = useState(serverNow());
  useEffect(() => {
    const timer = setInterval(() => setNow(serverNow()), intervalMs);
    return () => clearInterval(timer);
  }, [intervalMs]);
  return now;
}

const STORAGE_KEY = "studentId";

function loadStudentId() {
  try {
    const value = window.localStorage.getItem(STORAGE_KEY);
    return isValidStudentId(value) ? value : null;
  } catch {
    return null;
  }
}

/** The student's id (birth registration number), remembered across refreshes. */
export function useStudentId() {
  const [studentId, setStudentId] = useState(loadStudentId);
  const update = useCallback((value) => {
    setStudentId(value);
    try {
      if (value) window.localStorage.setItem(STORAGE_KEY, value);
      else window.localStorage.removeItem(STORAGE_KEY);
    } catch {
      // Storage can be blocked (private mode); the id then lasts until the page closes.
    }
  }, []);
  return [studentId, update];
}

/**
 * The student's notifications, refreshed every few seconds, so a cancellation by the
 * school shows up on whatever page the student has open. Pass null to switch it off.
 */
export function useNotifications(studentId) {
  const { data, refresh } = usePolling(
    () => (studentId ? api.notifications(studentId) : Promise.resolve([])),
    [studentId]
  );
  const list = data ?? [];
  const unread = list.filter((n) => !n.readAt);
  return { list, unread, refresh };
}
