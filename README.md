# School Admission System

Seat-hold based online admission: backend (.NET 8 + PostgreSQL) and frontend (React 18 + Vite), run with Docker.

## Project layout

| Path | What |
|---|---|
| `backend/SchoolAdmission.Api` | API: .NET 8, EF Core, PostgreSQL |
| `backend/SchoolAdmission.Tests` | Backend integration and concurrency tests |
| `frontend/src` | React UI (Vite). `frontend/tests` holds the frontend tests |
| `docker-compose.yml` | Runs the whole app (database, backend, frontend) |
| `docker-compose.test.yml` | Runs the tests |

## Run

Only Docker is required.

```bash
docker compose up --build
```

| Service  | URL                    |
|----------|------------------------|
| Frontend | http://localhost:3000  |
| Backend  | http://localhost:8080  |

Optional settings (copy `.env.example` to `.env`):

- `HOLD_DURATION_SECONDS` (default `600`): how long a seat is held
- `API_URL` (default `http://localhost:8080`): backend URL used by the frontend
- `POSTGRES_*`: database credentials

Data is stored in a Docker volume, so `docker compose down` then `up` keeps it. Use `docker compose down -v` to wipe it.

## Using the app

There are two separate sets of pages. The student pages never link to the admin pages.

**Student** (http://localhost:3000)

1. Enter a 17-digit birth registration number in the box at the top; it identifies the student (no login).
2. **ক্লাস তালিকা**: see total / held / confirmed / available seats (updates every few seconds) and press **আবেদন করুন** to hold a seat. Students cannot create classes.
3. On the application page you see the status and a countdown. **ভর্তি ফি দিন** confirms the admission (no real payment); **বাতিল** cancels. If the time runs out the application becomes **মেয়াদ শেষ** and the seat is released.
4. **আমার আবেদন**: all applications of the entered student.
5. **বিজ্ঞপ্তি**: messages for the student. If the school cancels an application or an admission, a red notice appears on top of every page within a few seconds (or the next time the student opens the app), and it stays in this list.

**School admin** (http://localhost:3000/#/admin)

1. **ক্লাস তৈরি**: create a class with a name and seat count. Each shift is its own class.
2. **সব আবেদন** (http://localhost:3000/#/admin/applications): every application with the student's birth registration number, class, status, and the time it was made. Expired (fee not paid in time) and cancelled applications are listed too. Filter by class; the list refreshes by itself.
3. In the same list the admin can cancel an admission (**ভর্তি বাতিল**) or a running application (**আবেদন বাতিল**). The seat is freed at once and the student is notified.

Tip: to see expiry quickly, set `HOLD_DURATION_SECONDS=30` in `.env` and run `docker compose up -d` again.

## Tests

Backend tests run against a real, throw-away PostgreSQL (the seat locking depends on PostgreSQL row locks), so Docker is again all you need:

```bash
docker compose -f docker-compose.test.yml up --build --abort-on-container-exit --exit-code-from tests
docker compose -f docker-compose.test.yml down -v
```

The test run ends with a pass/fail summary, and the command exits with a non-zero code if any test fails.

What is covered (`backend/SchoolAdmission.Tests`):

- **Classes**: create, input validation, seat counts in the list.
- **Admin list**: all applications with student number, status and time, including expired and cancelled ones; filter by class.
- **Admin cancel and notifications**: cancelling a confirmed admission or a running application frees the seat and notifies the student once; repeat cancel, expired applications, other students' notifications, and a cancel racing with the student's own confirm.
- **Holds**: apply, no seats left, duplicate application, student id validation, confirm (including repeated confirm), cancel, ownership (403), `/me/holds`.
- **Expiry**: hold expires and frees its seat with no API call, expired hold cannot be confirmed, student can re-apply, confirmed admissions never expire. A controllable clock is used, so the tests do not sleep.
- **Concurrency**: 40 simultaneous applications for 5 seats (exactly 5 succeed), the same with two separate app instances on one database, one student applying 10 times at once, 10 simultaneous confirms of one hold, and applications racing with cancels and confirms.

To run them without Docker for the tests (needs the .NET 8 SDK and a PostgreSQL you point `TEST_DB_CONNECTION` at): `dotnet test backend/SchoolAdmission.Tests`.

Frontend tests (Node's built-in test runner, nothing to install):

```bash
docker compose -f docker-compose.test.yml --profile frontend run --build --rm frontend-tests
```

They cover the logic the UI depends on: Bengali digit conversion and 17-digit student id check, the countdown format, an application past its time being shown as expired, every backend error code having a Bengali message, notification wording, and the address routing (including that students have no route to the admin pages). Without Docker: `cd frontend && npm test` (Node 18 or newer).

## Docs

- `ASSUMPTIONS.md`: decisions where the brief was not explicit
- `AI_USAGE.md`: how AI tools were used
