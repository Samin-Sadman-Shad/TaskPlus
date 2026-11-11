---
description: Produce the submission deliverables — README.md, final PROMPTS.md, SQL script from migrations, OpenAPI and Postman collection exports
argument-hint: "[readme | prompts | sql | postman]  (optional — omit to produce everything)"
allowed-tools: Read, Write, Edit, Glob, Grep, Bash(dotnet build:*), Bash(dotnet test:*), Bash(dotnet ef:*), Bash(dotnet run:*), Bash(dotnet tool:*), Bash(npx:*), Bash(curl:*), Bash(ls:*), Bash(mkdir:*), Bash(tree:*)
---

# /write-docs

Produce the documentation and artifacts the assignment requires. Everything you write must describe
**the code as it is**. Read the code before you write about it, and don't document features that
don't exist.

**Scope:** `$ARGUMENTS` — one of `readme`, `prompts`, `sql`, `postman`, or empty for all.

## 0. Read context first

1. `docs/REQUIREMENTS.md` — the Documentation and Deliverables sections
2. `CLAUDE.md`, `docs/ARCHITECTURE.md`, `docs/API-CONVENTIONS.md`, `docs/PROGRESS.md`
3. The actual code: `src/`, `client/smart-task-web/`, `appsettings*.json`, controllers (routes and policies)
4. Existing `README.md` / `PROMPTS.md`

Before writing, check the build: `dotnet build SmartTask.sln && dotnet test SmartTask.sln`. If it
fails, stop and report — documenting a broken build wastes the reviewer's time.

## 1. `sql` — database script

```bash
mkdir -p database
dotnet ef migrations script --idempotent -p src/SmartTask.Persistence -s src/SmartTask.Api -o database/SmartTask.sql
```
Add a header comment to `database/README.md` explaining that the script is generated from the EF Core
migrations and how to apply either one.

## 2. `postman` — API collection

1. Start the API in Development (`dotnet run --project src/SmartTask.Api --launch-profile https`) in the
   background, wait until `/health/live` responds, then save
   `curl -k https://localhost:7180/swagger/v1/swagger.json -o docs/api/openapi.json`, then stop the API.
   (If you can't run it, ask the user to run that command and continue once the file exists.)
2. Convert it: `npx openapi-to-postmanv2 -s docs/api/openapi.json -o docs/api/SmartTask.postman_collection.json -p -O folderStrategy=Tags`.
3. Post-process the collection JSON:
   - collection variables `baseUrl` = `https://localhost:7180`, and `accessToken` = empty
   - collection-level auth: Bearer `{{accessToken}}`; the auth folder's requests use **no auth**
   - a **test script on Login and Refresh** that saves `pm.response.json().data.accessToken` to
     `accessToken` (Postman's cookie jar keeps the refresh cookie automatically)
   - sample bodies that pass validation (use the seed users' emails, never real passwords — use
     `{{password}}` variables)
4. Create `docs/api/postman_environment.json` with `baseUrl`, `email` and `password` (empty values).

## 3. `readme` — `README.md` (repo root)

Required sections (from the assignment), in this order:

1. **Project Overview** — what it does, the three roles, a features list with status (✅ done / ⏳ partial),
   and screenshots if `docs/images/` has any
2. **Technology Stack** — table of backend, frontend, data, security, AI, testing, with versions
   **read from the `.csproj` files and `package.json`**
3. **Architecture** — a Mermaid diagram of the layers and dependency direction, a short explanation
   of each layer, the patterns used and why (Repository, Unit of Work, Service layer, Strategy for AI,
   Options, base-controller response mapping), and a link to `docs/ARCHITECTURE.md`
4. **Setup Instructions** — prerequisites (.NET SDK, Node LTS, SQL Server/LocalDB, EF tool), then
   numbered steps: clone → user-secrets (`Jwt:SigningKey`, `Ai:ApiKey`, seed passwords) → connection
   string → `dotnet ef database update` (or run `database/SmartTask.sql`) → run the API → `npm ci && npm start` →
   log in with the seeded accounts. Include a troubleshooting section (dev certificate trust, CORS
   origin, SQL connectivity)
5. **Configuration** — table of every setting (`Jwt`, `Ai`, `Cors`, `RateLimiting`, `Serilog`, `Seed`,
   `Database`, `Bootstrap:Admin`), which ones are secrets, and where to set them. Explain the first-admin
   flow: the bootstrap admin from configuration, then `POST users` / `PATCH users/{id}/role` for everyone else
6. **API Overview** — the endpoint table (verb, route, roles, description) **generated from the
   controllers**, the response envelope with a success example and an error example, the auth flow
   (access token + rotating refresh cookie) as a Mermaid sequence diagram, and paging/sorting/filter
   query parameters with allowed sort fields. Link to Swagger and the Postman collection
7. **Security** — how each required item is covered: JWT, refresh tokens, password hashing, RBAC +
   resource checks, HTTPS/HSTS, CORS, validation, rate limiting, secure configuration, and security headers
8. **Folder Structure** — a trimmed tree (two to three levels) for backend and frontend, with a one-line
   purpose per folder (use `tree -L 3 -I 'bin|obj|node_modules|dist|.angular'` and then trim it)
9. **Testing** — how to run the tests, what is covered, and the current test count
10. **Agentic Development Workflow** — a short note on how the project was built with Claude Code
    custom commands (`.claude/commands/`), linking `CLAUDE.md` and `docs/WORKFLOW.md`
11. **Known Limitations & Future Improvements** — be honest (e.g. access tokens can't be revoked
    before they expire, no email verification, and anything left ⏳ in PROGRESS.md)

Style: plain, professional and concise. Commands in fenced code blocks. No marketing language.

## 4. `prompts` — `PROMPTS.md`

If `/implement-ai` already created it, **update it from the code**: copy the exact current system
prompt and user template from `TaskDescriptionPrompt`, with the prompt version. If the API is
reachable and an AI key is configured, call the endpoint with the example inputs and replace the
illustrative outputs with real ones (label them with the model name and date). Check that all five
required sections exist: AI Prompt Design, Prompt Structure, Example Inputs and Outputs, Validation
Approach, and Safety Considerations.

## 5. Verify

- Every command in README's setup section has been run or checked against the code.
- Every endpoint in the README table exists in a controller (grep the routes), and vice versa.
- Every relative link in README.md and PROMPTS.md resolves to a file.
- No secrets anywhere: `grep -rnE "ghp_|github_pat_|SigningKey\"\s*:\s*\"[^\"]+|Password=" --include=*.json --include=*.md .` prints nothing.

Report ✅/❌ for each deliverable in `docs/REQUIREMENTS.md` → Deliverables:
source code, SQL script/migrations, README.md, PROMPTS.md, and the API collection.

## 6. Finish

1. Update `docs/PROGRESS.md` (mark the documentation step done and list the deliverables).
2. Print the deliverables checklist and anything the user must do manually (e.g. add screenshots,
   push to GitHub, share the repo link).
3. Suggested commit: `docs: README, PROMPTS, SQL script and API collection`.
