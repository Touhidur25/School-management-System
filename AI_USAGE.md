# AI Usage

I used Claude for this assignment. I didn't use any other AI tool.

## How I used it

I gave Claude the error where I stuck. I build this project in diiferent small steps. After each step I ran it on my pc with Docker before moving on.

1. Project setup: docker compose and the database tables
2. Backend APIs for classes and holds (seat limit, hold expiry)
3. Backend tests, including the concurrency test
4. The React UI
5. Changes I asked for after I saw it working. Students can only apply, and a separate admin page creates classes and shows all applications. Later the admin got the power to cancel an admission, and the student gets a notification.


## Where the AI got things wrong

- A few smaller things: an id generator written in a fragile way, and a messy first version of one test. Both were rewritten.

- When it added notifications, it noticed my existing database would not get the new table, so the app would have crashed on start. It added a small startup step that creates the table if it is missing.

- The hold time was rounded up to the next second, so a hold could last almost one second longer than the setting, and that could break a timing test. Now the exact time is stored and only the value shown in the API is rounded.


## How I checked the work

- Ran the backend tests against a real PostgreSQL with docker compose. They include 40 students applying for 5 seats at the same time.
- Ran the frontend tests.
- Ran the whole app with docker compose and tried the student and admin pages in the browser.

