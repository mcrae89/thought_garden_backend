# Thought Garden Backend

ASP.NET Core 8 Web API + GraphQL backend for the **Thought Garden** journaling app.  
Provides encrypted journal entry storage, sentiment tagging (future), and garden state visualization.

---

## ⚡️ Tech Stack
- **.NET 8** (ASP.NET Core Web API)
- **GraphQL (Hot Chocolate)**
- **Entity Framework Core 8** (PostgreSQL provider)
- **PostgreSQL** for persistence
- **Docker** for containerization
- **GitHub Actions** for CI/CD

---

## 🚀 Getting Started

### 1. Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- [PostgreSQL](https://www.postgresql.org/download/) (or Docker)
- [Docker Desktop](https://www.docker.com/products/docker-desktop) (optional, for containerization)

### 2. Local Database Setup
```powershell
# Create database in local Postgres
psql -U postgres -c "CREATE DATABASE thought_garden;"
```

### 3. Run the API
```powershell
cd src/ThoughtGarden.Api
dotnet restore
dotnet build
dotnet run
```

### 4. Auth & Session Config
- Access token TTL: 15 minutes
- Refresh token TTL: 7 days
- Refresh rotation: enabled (issue new refresh token on each refresh; invalidate the previous one)
- reuse leeway: 2 minutes overlap to avoid failing confurrent requests
appsettings.json
```
{
  "Jwt": {
    "Issuer": "ThoughtGarden",
    "Audience": "ThoughtGarden.Mobile",
    "SigningKey": "REPLACE_WITH_Base64_256bit_SECRET",
    "AccessTokenMinutes": 15,
    "RefreshTokenDays": 7,
    "RotateRefreshTokens": true,
    "ReuseLeewayMinutes": 2
  }
}
```
Environment variables
```
JWT__ISSUER=ThoughtGarden
JWT__AUDIENCE=ThoughtGarden.Mobile
JWT__SIGNINGKEY=REPLACE_WITH_Base64_256bit_SECRET
JWT__ACCESSTOKENMINUTES=15
JWT__REFRESHTOKENDAYS=7
JWT__ROTATEREFRESHTOKENS=true
JWT__REUSELEEWAYMINUTES=2
```
Mobile expectations
- Use the access toekn for GraphQL calls; on 401, call the refresh flow.
- Replace the stored refresh token with the new one returned by the server.
- If refresh fails (expired/reused), prompt the user to sign in again.
