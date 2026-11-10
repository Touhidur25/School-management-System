# Assumptions

Where the brief was not explicit, this is what I assumed and why.

## Data and storage

- **IDs** are short strings with a prefix (`c_...` for classes, `h_...` for holds), matching the examples in the brief.
- **Schema creation** uses EF Core `EnsureCreated` on startup (with retry) instead of migrations. For a self-contained take-home this keeps `docker compose up` to a single step. `EnsureCreated` skips a database that already has tables, so tables added after the first version are created by a small `CREATE TABLE IF NOT EXISTS` step at startup; an existing database keeps its data and gets the new table.
- **Timestamps** are stored and returned in UTC.
- **Student ID** is validated as exactly 17 digits (the birth registration number format in the brief).
- A database-level partial unique index guarantees a student cannot have two active (HELD/CONFIRMED) holds in one class, even if application logic had a bug.

## API and networking

- **CORS** allows any origin, because the frontend runs on a different port from the backend and there is no login.


## API behaviour

- **Error format** is always `{ "error": { "code": "...", "message": "..." } }`. The UI maps `code` to a friendly message.
- **Status codes**: `400` bad input or missing/invalid `X-Student-Id`; `403` acting on another student's application; `404` class or application not found; `409` conflicts (no seats, already applied, expired, cancelled, already confirmed).
- **Applying twice to the same class** while an application is active (HELD) or confirmed returns `409 already_applied` instead of returning the existing hold. A student whose previous application expired or was cancelled can apply again.
- **Confirm** is idempotent: confirming an already confirmed application returns `200` and changes nothing, so `confirmedSeats` counts it once. Confirming an expired or cancelled application returns `409`.
- **Cancel** (`DELETE`) works only on a HELD application. Cancelling an already cancelled one returns `204` (idempotent). Cancelling an expired one returns `409 hold_expired`. Cancelling a confirmed admission returns `409 already_confirmed`, because the brief has no refund or withdrawal flow.
- `GET /classes`, `GET /classes/{id}` and `GET /holds/{id}` need no `X-Student-Id`; the brief lists the header only for the other endpoints.
- **Class input**: name is required (max 200 chars), `totalSeats` is a whole number from 1 to 100000. Duplicate class names are allowed.
- `expiresAt` is rounded up to a whole second, so a hold lasts at least `HOLD_DURATION_SECONDS`.
- **Admin cancels an admission**: `POST /admin/holds/{holdId}/cancel` works on a HELD application and on a CONFIRMED admission (students themselves can only cancel a HELD one). The status becomes `CANCELLED`, the seat is freed, and the student gets a notification. Cancelling an already cancelled one returns `200` and does not notify again; an application that already expired returns `409 hold_expired`. No reason text is collected. The student may apply to that class again afterwards, because the brief has no rule against it.
- **Notifications** (`GET /me/notifications`, `POST /me/notifications/{id}/read`, `POST /me/notifications/read-all`, all with `X-Student-Id`) are in-app only. The brief has no login and no contact details (phone, email), so the only address is the birth registration number: the message is stored and shown whenever that student next has the app open, and while the app is open it appears within the polling interval. Only an admin cancellation creates one; a student cancelling their own application does not. They are saved in the database, so they survive restarts.
- **Extra endpoint for the admin screen**: `GET /admin/holds` (optional `?classId=`) returns every application, newest first, with `studentId`, `className`, `status`, `appliedAt`, `expiresAt` and `confirmedAt`. Expired and cancelled applications are included on purpose: the admin should see who applied but did not pay in time, or withdrew. The list is not paginated, which is fine for the size of a school's intake.

## Seats, expiry and concurrency

- A seat is taken by a CONFIRMED application, or by a HELD one that has not passed `expiresAt`.
- **Expiry needs no job or API call.** Whether a hold is expired is decided from the clock whenever it is read or counted, so seats free themselves and `GET` calls show `EXPIRED` right away. Stale rows are written back as `EXPIRED` the next time that class is touched by a write.
- **Never more than `totalSeats`**: apply, confirm and cancel each run in a database transaction that first locks the class row (`SELECT ... FOR UPDATE`). Operations on one class therefore run one at a time, and the seat count is checked after taking the lock. Because the lock lives in PostgreSQL, not in the app, it also holds when two backend processes share the same database. Different classes do not block each other.
- A partial unique index on `(class_id, student_id)` for HELD/CONFIRMED rows is a second line of defence against duplicate active applications.

## UI

- **Language**: the UI text is in Bengali, like the brief. Error messages are chosen from the backend's error `code`, so users never see raw technical text.
- **Student identity**: one box at the top of every page holds the 17-digit birth registration number (Bengali digits are accepted and converted). It is remembered in the browser (`localStorage`), so a refresh or reopening the browser keeps the student's applications visible. A different browser or device needs the number typed again.
- **Students and admin are separate in the UI**: students can only see the class list, apply and manage their own applications. Class creation and the all-applications list are on admin pages (`#/admin`, `#/admin/applications`) that the student pages do not link to. This is a separation of screens, not a security boundary: the brief says there is no login, so anyone who knows the admin address can open it.
- **Why the API stays open**: `POST /classes` is part of the API contract that is called directly, without credentials, so it cannot require an admin key. The same applies to the extra admin endpoint below.
- **Live updates**: seat counts and application status refresh by polling every 3 seconds (and immediately when the tab becomes visible again). Polling was chosen over WebSockets as the simplest approach that meets "updates within a few seconds".
- **Expiry in the UI**: the countdown uses the server's clock (taken from the `Date` response header, so a wrong computer clock does not matter). When it reaches zero the application shows as expired at once and the fee button is disabled, without a reload; the page then asks the server to confirm.
- **Cancel** asks for confirmation in the browser before sending the request.
- **Admin cancel** also asks for confirmation, showing the student number and class, because it cannot be undone and it sends a notification.
- **Student notification**: a red notice on every student page until the student presses "ঠিক আছে", plus a **বিজ্ঞপ্তি** tab with an unread count and the full list.
- Seat limits and expiry are always enforced by the backend. The UI only mirrors them, so a stale screen can never oversell a class.
