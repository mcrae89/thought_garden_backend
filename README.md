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

### 3. Run the API
```powershell
cd src/ThoughtGarden.Api
dotnet restore
dotnet build
dotnet run
