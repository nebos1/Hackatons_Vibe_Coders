# Hackatons Vibe Coders

Event and social platform built during the AI Ruse Hackathon. The current production-ready branch uses PostgreSQL and is suitable for Render/Railway-style deployment.

## Current Stack

- .NET 8
- ASP.NET Core MVC + Razor Views
- Entity Framework Core
- ASP.NET Core Identity
- PostgreSQL
- Optional OpenAI integration
- Optional Google Maps integration
- Optional SMTP email delivery
- Local or S3-compatible media storage

## What Works Right Now

- registration, login, roles, and seeded admin account
- event browsing and event details
- organizer profiles and organizer management screens
- posts, stories, comments, likes, and social feed UI
- direct messages and shared cards inside conversations
- ticket creation, ticket management, attendee data, and user tickets
- venue layout editing, including AI-assisted layout generation
- AI-assisted search when an API key is configured
- Bulgarian and English localization

## Local Run

### Prerequisites

- .NET 8 SDK
- Docker Desktop or another Docker runtime

### Local database

The default development setup uses PostgreSQL in Docker:

```powershell
docker compose up -d postgres
dotnet restore
dotnet ef database update
dotnet run
```

Default local URLs:

- `https://localhost:7180`
- `http://localhost:5180`

The development connection string already points to:

```text
Host=localhost;Port=5432;Database=evento_dev;Username=evento;Password=evento_dev_password
```

### Optional `.env`

The app loads a local `.env` automatically. Start from `.env.example`.

Common keys:

```env
OPENAI_API_KEY=
GOOGLE_MAPS_KEY=
ADMIN_EMAIL=
ADMIN_PASSWORD=
```

Aliases also supported:

- `AI_API_KEY` for `OPENAI_API_KEY`
- `GOOGLE_MAPS_API_KEY` for `GOOGLE_MAPS_KEY`

### Startup behavior

On startup the app automatically:

- applies pending EF Core migrations
- seeds roles
- seeds the admin account
- seeds demo data only in `Development` when `SeedDemoData=true`

## Demo Accounts

Development defaults:

- admin email: `admin@grooveon.com`
- admin password: `admin`

Demo users and organizers use:

- password: `Demo123`
- example emails: `ivan.dimitrov@demo.bg`, `sofia.sound@demo.bg`, `beat.rooms@demo.bg`

## Production Env

Required in production:

- `DATABASE_URL`
- `ADMIN_EMAIL`
- `ADMIN_PASSWORD`
- `APP_PUBLIC_URL`

Useful optional keys:

- `OPENAI_API_KEY`
- `GOOGLE_MAPS_KEY`
- `GOOGLE_SITE_VERIFICATION`
- `EMAIL_ENABLED`
- `BREVO_API_KEY`
- `SMTP_HOST`
- `SMTP_PORT`
- `SMTP_USERNAME` or `SMTP_USER`
- `SMTP_PASSWORD` or `SMTP_PASS`
- `SMTP_FROM_EMAIL`
- `SMTP_FROM_NAME`
- `SMTP_ENABLE_SSL`

For persistent media uploads in production, use S3-compatible storage:

- `MEDIA_STORAGE=S3`
- `S3_ENDPOINT`
- `S3_BUCKET`
- `S3_ACCESS_KEY_ID`
- `S3_SECRET_ACCESS_KEY`
- `S3_REGION`
- `S3_FORCE_PATH_STYLE`
- `S3_KEY_PREFIX`
- `S3_SIGNED_URL_MINUTES`

Railway-style aliases are also supported for bucket credentials:

- `BUCKET`
- `ACCESS_KEY_ID`
- `SECRET_ACCESS_KEY`
- `REGION`
- `ENDPOINT`

`ADMIN_PASSWORD` outside Development must satisfy the stronger production policy: at least 10 characters, uppercase, lowercase, number, and symbol.

## Render / Railway Notes

- The app reads `DATABASE_URL` first and falls back to `ConnectionStrings:DefaultConnection`.
- The app binds to the `PORT` environment variable automatically in hosted environments.
- Local filesystem uploads are fine for development, but hosted environments should use S3-compatible storage unless you intentionally attach persistent disk storage.

## Build Status

Verified locally on May 7, 2026:

- `dotnet build` succeeds

## Project Structure

```text
Hackatons_Vibe_Coders/
|-- Areas/
|-- Common/
|-- Configuration/
|-- Controllers/
|-- Data/
|-- Infrastructure/
|-- Migrations/
|-- Models/
|-- Services/
|-- ViewModels/
|-- Views/
|-- wwwroot/
|-- Program.cs
|-- EventsApp.csproj
|-- EventsApp.sln
|-- README.MD
```
