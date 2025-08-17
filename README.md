    # Thought Garden — Backend (ASP.NET Core 8 + GraphQL)

    Backend API for Thought Garden. Built with **ASP.NET Core 8**, **Hot Chocolate (GraphQL)**, **EF Core**, and **PostgreSQL**.

    ## Features (MVP)
    - GraphQL API (Hot Chocolate)
    - JWT authentication
    - Journal entries with AES-256-GCM encryption at rest
    - Soft delete for entries
    - PostgreSQL via EF Core

    ## Getting Started (Local Dev)
    ### Prereqs
    - .NET 8 SDK
    - Docker (for local Postgres) or a running PostgreSQL instance
    - Node is **not** required here

    ### 1) Configure environment
    Create a `.env.local` file (never commit this) at repo root:
    ```env
    # Example values; customize as needed
    ASPNETCORE_ENVIRONMENT=Development
    DATABASE_URL=Host=localhost;Port=5432;Database=thought_garden;Username=postgres;Password=postgres
    JWT_PRIVATE_KEY_PEM=-----BEGIN PRIVATE KEY-----
...your-key...
-----END PRIVATE KEY-----
    # Optional: Key Vault/KMS settings (future)
    ```

    ### 2) Start Postgres (Docker)
    ```bash
    docker compose up -d
    # or: docker run --name tg-pg -e POSTGRES_PASSWORD=postgres -p 5432:5432 -d postgres:16
    ```

    ### 3) Run migrations
    ```bash
    dotnet restore
    dotnet tool restore
    dotnet ef database update
    ```

    ### 4) Run the API
    ```bash
    dotnet run --project src/ThoughtGarden.Api
    # GraphQL endpoint (example): https://localhost:5001/graphql
    ```

    ## Project Structure (suggested)
    ```
    src/
      ThoughtGarden.Api/         # ASP.NET Core host + GraphQL schema registration
      ThoughtGarden.Core/        # domain models, encryption utilities
      ThoughtGarden.Data/        # DbContext, entities, EF migrations
    tests/
      ThoughtGarden.Tests/       # unit/integration tests
    docker/
      docker-compose.yml         # local Postgres, optional admin tools
    ```

    ## Useful Commands
    ```bash
    # add migration
    dotnet ef migrations add Init --project src/ThoughtGarden.Data --startup-project src/ThoughtGarden.Api

    # update database
    dotnet ef database update --project src/ThoughtGarden.Data --startup-project src/ThoughtGarden.Api
    ```

    ## CI
    See `.github/workflows/backend-ci.yml` for minimal build/test pipeline.

    ## Security Notes
    - Never log plaintext journal content.
    - Keep JWT signing keys and DB credentials in secure secrets (Key Vault / GitHub Actions secrets).
    - All traffic must be over HTTPS.
