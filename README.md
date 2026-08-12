# GameZone API

ASP.NET Core 8 Web API with user registration, JWT login, and user management, backed by
SQL Server LocalDB via EF Core.

## Layout

```
GameZone.slnx
global.json                 pins the .NET 8 SDK
src/GameZoneApi/
  Program.cs                DI, JWT bearer auth, Swagger
  Models/User.cs            User entity + role constants
  Models/Machine.cs         Machine entity + fixed seed rows
  Models/GameRecord.cs      play session (user x machine x duration) + cost formula
  Models/Dtos.cs            request/response records with validation
  Data/AppDbContext.cs      EF Core context, indexes, machine seed
  Data/DbInitializer.cs     dev-only migrate + admin seed
  Services/TokenService.cs  JWT issuing
  Controllers/              AuthController, UsersController, MachinesController, RecordsController
  GameZoneApi.http           ready-to-run requests
```

## Play sessions

Four tables: `Users`, `Machines`, `Records`, `Payments`. A record says *user X played
machine Y for N minutes starting at T*, and stores the cost charged for it. Payments
preserve the financial events associated with that record.

**Machines are fixed reference data** - they are seeded by the migration and have no
write API, only `GET /api/machines`:

| Id | Name | Rate/h |
| --- | --- | --- |
| `1111...1111` | Rig-01 | 8.00 |
| `2222...2222` | Rig-02 | 6.00 |
| `3333...3333` | Rig-03 | 4.50 |
| `4444...4444` | Console-01 | 5.00 |
| `5555...5555` | Rig-04 (maintenance) | 5.50, inactive |

`Records` is the CRUD surface. Cost is computed server-side as
`hourlyRate * durationMinutes / 60` and snapshotted on the row, so changing a machine's
rate later never rewrites past bills. A user only sees and edits their own records;
admins see and edit everyone's. Two sessions cannot overlap on the same machine.

## Database

Connection string points at `(localdb)\MSSQLLocalDB`, database `GameZoneDb`. The same
instance is reachable from SSMS — connect to server name `(localdb)\MSSQLLocalDB` with
Windows Authentication.

Migrations live in `Data/Migrations`. In Development the app applies them on startup and
seeds an admin account:

- **admin@gamezone.local** / **Admin123!**

Apply migrations manually with:

```powershell
cd src\GameZoneApi
dotnet ef database update
```

## Configuration

The JWT signing key is stored in user-secrets, not in `appsettings.json`. It is already set
for this machine. To regenerate:

```powershell
cd src\GameZoneApi
dotnet user-secrets set "Jwt:Key" "<at least 32 characters of random text>"
```

## Running

```powershell
cd src\GameZoneApi
dotnet run
```

Swagger UI is at `/swagger`, health check at `/health`. Use the **Authorize** button in
Swagger and paste the `accessToken` from `/api/auth/login`.

## Endpoints

| Method | Route | Auth |
| --- | --- | --- |
| GET | `/health` | anonymous |
| POST | `/api/auth/register` | anonymous |
| POST | `/api/auth/login` | anonymous |
| GET | `/api/users/me` | any user |
| POST | `/api/users/me/change-password` | any user |
| GET | `/api/users/{id}` | self or admin |
| PUT | `/api/users/{id}` | self or admin |
| GET | `/api/users` | admin |
| DELETE | `/api/users/{id}` | admin |
| GET | `/api/machines` | any user |
| GET | `/api/machines/{id}` | any user |
| GET | `/api/records` | own rows; admin sees all |
| POST | `/api/records` | any user |
| GET | `/api/records/{id}` | owner or admin |
| PUT | `/api/records/{id}` | owner or admin |
| DELETE | `/api/records/{id}` | owner or admin |
| POST | `/api/payments` | admin |
| GET | `/api/payments` | own rows; admin sees all |
| GET | `/api/payments/{id}` | record owner or admin |
