# Assumptions

Where the brief was not explicit, this is what I assumed and why.

## Data and storage

- **IDs** are short strings with a prefix (`c_...` for classes, `h_...` for holds), matching the examples in the brief.
- **Schema creation** uses EF Core `EnsureCreated` on startup (with retry) instead of migrations. For a self-contained take-home this keeps `docker compose up` to a single step.
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

## Seats, expiry and concurrency

- A seat is taken by a CONFIRMED application, or by a HELD one that has not passed `expiresAt`.
- **Expiry needs no job or API call.** Whether a hold is expired is decided from the clock whenever it is read or counted, so seats free themselves and `GET` calls show `EXPIRED` right away. Stale rows are written back as `EXPIRED` the next time that class is touched by a write.
- **Never more than `totalSeats`**: apply, confirm and cancel each run in a database transaction that first locks the class row (`SELECT ... FOR UPDATE`). Operations on one class therefore run one at a time, and the seat count is checked after taking the lock. Because the lock lives in PostgreSQL, not in the app, it also holds when two backend processes share the same database. Different classes do not block each other.
- A partial unique index on `(class_id, student_id)` for HELD/CONFIRMED rows is a second line of defence against duplicate active applications.
