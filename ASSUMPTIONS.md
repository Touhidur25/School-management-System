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

_More entries are added as each part is built._
