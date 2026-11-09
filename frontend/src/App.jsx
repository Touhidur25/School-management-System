import { useEffect, useState } from "react";
import { API_URL } from "./config.js";

// Placeholder: confirms the frontend can reach the backend.
// Real screens are built in the UI step.
export default function App() {
  const [status, setStatus] = useState("checking...");

  useEffect(() => {
    fetch(`${API_URL}/health`)
      .then((r) => (r.ok ? "backend connected" : "backend returned an error"))
      .catch(() => "backend unreachable")
      .then(setStatus);
  }, []);

  return (
    <main style={{ fontFamily: "sans-serif", padding: 24 }}>
      <h1>School Admission System</h1>
      <p>Backend: {status}</p>
    </main>
  );
}
