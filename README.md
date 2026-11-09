# School Admission System

Seat-hold based online admission: backend (.NET 8 + PostgreSQL) and frontend (React 18 + Vite), run with Docker.

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

## Tests

_TODO: filled in after the backend tests are written._

## Docs

- `ASSUMPTIONS.md`: decisions where the brief was not explicit
- `AI_USAGE.md`: how AI tools were used
