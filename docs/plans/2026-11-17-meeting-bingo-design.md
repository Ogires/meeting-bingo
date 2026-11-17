# Design: Meeting Bingo

**Date:** 2026-11-17 · **Author:** Sergio (Ogires) with Claude
**App #8 of the miniapp factory, the first one on .NET and Azure** · Domain: `bingo.webninja.fyi` · Target cost: €0 a month within the Azure free allowances, with a €5 budget alert

---

## 1. Vision

**Meeting Bingo** is a live bingo for the meetings that could have been an email. Someone creates a room, pastes the link in the meeting chat, and everyone plays on their own card while the meeting goes on.

The earlier factory apps run on Cloudflare Workers. This one exercises the cloud-native .NET stack end to end:
- real time with ASP.NET Core SignalR;
- a container image, run on Azure Container Apps;
- Azure SQL with no passwords anywhere (Managed Identity);
- infrastructure as code with Bicep;
- integration tests against a real SQL Server (Testcontainers);
- OpenTelemetry, locally and in Application Insights;
- a CI/CD pipeline that stores no credentials (OIDC).

**Languages:** code and documentation in English. The app and its phrases are in Spanish, because the phrases are Spanish meeting clichés.

## 2. Scope of the first version

### 2.1 Flow

1. Someone creates a room and shares its link, for example `bingo.webninja.fyi/s/K7QMP`.
2. Players join without signing up. Each one gets a random geeky alias and a 5×5 card with a free centre square.
3. When someone in the meeting says a phrase on your card, you mark it. If two or more players mark the same phrase within 60 seconds, the room sees "Confirmado por 3 personas".
4. The server holds every card, checks lines and bingos, and announces them to the room. The client never declares a bingo.

### 2.2 Phrases

Between 60 and 80 Spanish meeting phrases, in a versioned JSON file where each phrase has a stable id:
- **Technical:** «¿Se me oye?», «Estás en mute», «Se te ha congelado la imagen», «¿Veis mi pantalla?»
- **Jargon:** «Lo vemos offline», «Hay que alinearnos», «Ponerlo en el radar»
- **Situations:** «Alguien llega tarde», «Ladra un perro», «Nos pasamos de la hora»

No phrase presents a legitimate engineering term (CI/CD, deploy, refactoring...) as hot air.

### 2.3 Statistics

When a room closes, only aggregates are kept: duration, number of players, bingos, and how many times each phrase was marked and confirmed. The home page shows the most-marked phrases of the week.

### 2.4 Privacy

There are no accounts, names or free text, so there is nothing to moderate. The client IP is used only in memory, for rate limits, and is never written to logs or to the database.

### 2.5 Limits

- 50 players per room and 10 connections per IP.
- 10 rooms created per IP per hour. Join attempts are rate-limited too.
- Global caps on open rooms (500 to start with) and on connections. The connection cap is set from the load test (§8.3).
- A room is removed 5 minutes after it empties, or after 3 hours without activity.

### 2.6 Out of scope

Custom phrase lists, teams and sounds. More than one replica, which would need a Redis backplane or Azure SignalR Service. Private endpoints for the database.

## 3. Architecture

```
Browser (Angular)
   │  HTTPS: create room, stats              WebSocket: SignalR
   ▼
┌──────────────── one container, one process (.NET 10) ───────────────┐
│  Minimal API ── /api/rooms, /api/stats, /healthz                     │
│  BingoHub (SignalR) ── join, mark, room events                       │
│  RoomRegistry (memory) ── rooms, cards, marks                        │
│  Sweeper (BackgroundService) ── closes empty or idle rooms           │
│  Recorder (BackgroundService) ── Channel<GameSummary> → Azure SQL    │
│  Static files ── the compiled Angular app                            │
└──────────────────────────────────────────────────────────────────────┘
                        │ only the Recorder, a few times a day
                        ▼
                   Azure SQL (free offer)
```

### 3.1 Solution layout

| Project | Contents |
|---|---|
| `MeetingBingo.Core` | The domain, with no dependencies: room, card, phrases, line and bingo rules, the confirmation window and the ports (`IGameRecorder`) |
| `MeetingBingo.Infrastructure` | EF Core, migrations and the Recorder |
| `MeetingBingo.Api` | Startup: endpoints, hub, dependency injection, rate limiting, security headers |
| `MeetingBingo.AppHost` and `MeetingBingo.ServiceDefaults` | Aspire. The AppHost runs only on a developer machine; ServiceDefaults ships inside the API (§3.7) |
| `web/` | The Angular app |

Package versions live in `Directory.Packages.props` (Central Package Management), with NuGet lock files.

### 3.2 Rooms in memory

- A `ConcurrentDictionary` of rooms. Each room is changed inside its own `lock`; the operations are short and synchronous, and messages to the room are sent outside the lock.
- Each player gets a random 128-bit token, kept in `sessionStorage`. Reloading the page rejoins the room with the same card and marks.
- Room codes have 5 characters from a 31-character alphabet without look-alikes (`23456789ABCDEFGHJKMNPQRSTUVWXYZ`: no 0/O or 1/I/L), which gives about 28.6 million codes.
- Time comes from `TimeProvider`, so the confirmation window and room expiry are testable without waiting.
- The Sweeper closes empty and idle rooms. Closing a room hands its summary to the Recorder.

### 3.3 SignalR hub

A typed hub (`Hub<IBingoClient>`) with one group per room.
- **Methods:** `JoinRoom(code, token?)` returns the card and the room state. `ToggleCell(index)` marks or unmarks a cell.
- **Events:** `PlayerJoined`, `PlayerLeft`, `PhraseMarked` (with the count and whether it is confirmed) and `Bingo` (alias and line).
- Every mark is checked against the card the server dealt.

### 3.4 HTTP API

| Method | Route | Purpose |
|---|---|---|
| `POST` | `/api/rooms` | Creates a room and returns its code (rate-limited per IP) |
| `GET` | `/api/stats` | Top phrases of the week, served from an in-memory snapshot, with the time they were computed |
| `GET` | `/healthz` | Liveness probe. It never touches SQL, so probes don't wake the database |

Errors are returned as ProblemDetails (RFC 9457) by a global `IExceptionHandler`, with no internal details.

### 3.5 The database stays off the user's path

The Azure SQL free offer gives 100,000 vCore-seconds a month per database. A paused database takes about a minute to wake up and then stays online at least 15 minutes after its last query. If every mark were written to SQL, the first player would wait that minute and the monthly allowance would run out within a few busy days. So no web request ever waits for SQL:

- When a room closes, its summary goes into a bounded `Channel<GameSummary>`. If the channel is full, the oldest summary is dropped and counted in a metric.
- The Recorder writes the summaries in batches during a window every 6 hours (configurable) and when the container shuts down. On shutdown, the Sweeper first closes every open room, so their summaries reach that last flush.
- **Statistics snapshot.** After each write, the Recorder recomputes the weekly statistics over the same connection and stores them in an in-memory snapshot, which `/api/stats` serves. A freshly started container loads the snapshot once, in the background, so it shows statistics without making anyone wait. A read-through cache would be the wrong tool here: an expired entry would wake the database on the next visit.
- **Idempotent writes.** Each game gets a `GameId` when it ends, and the Recorder skips games that already exist. When a connection drops while a transaction commits, EF Core retries without knowing whether the commit succeeded; with the `GameId`, that retry cannot duplicate a game.
- **Retries.** EF Core retries transient errors (`EnableRetryOnFailure`) within the window. If the batch still fails, it waits for the next window.
- **Running out.** The database wakes up once per window, once when a container starts, and once per scale-to-zero shutdown that has pending summaries. If the monthly allowance runs out, the database pauses until the next month. Games keep working; the statistics freeze and the home page says how old they are.

**Tables:**
- `Games`: id, start, end, players, bingos.
- `PhraseMarks`: game id, phrase id, marks and confirmations.

Phrases live in the versioned JSON file, not in the database.

### 3.6 Frontend

Angular with standalone components and signals. The `@microsoft/signalr` client updates a signal with the room state. ASP.NET Core serves the compiled app as static files, so there is one container and one deployment. The printable fallback card is generated in the browser from the phrase list bundled with the app.

### 3.7 Local development with Aspire

The Aspire AppHost starts SQL Server in a Docker container, the API as a normal process (debugger and hot reload work) and the Angular dev server, and passes the connection string to the API. Its dashboard shows logs, traces and metrics, including the app's own metrics.

`ServiceDefaults` is the only Aspire part that reaches production. It is regular code in the API that configures OpenTelemetry, health checks and HTTP resilience; in Azure the telemetry goes to Application Insights instead of the dashboard.

Aspire can also deploy to Container Apps, but this project does not use that: the infrastructure is written explicitly in Bicep (§4).

### 3.8 Accepted trade-offs

- A restart loses the open rooms and any summaries not yet recorded.
- One replica only, so no horizontal scale-out.
- The printable card covers failures of the hub or the API. It does not cover a full outage, because the same container serves the page.
- With scale to zero, the first visitor after a quiet period waits for the container to start (§4.4).

## 4. Azure infrastructure

### 4.1 Resources

Everything is described in Bicep, in one resource group, `rg-meetingbingo`, in West Europe or in Spain Central if it offers every service needed (checked when creating it).

| Resource | Configuration |
|---|---|
| Container Apps environment | Consumption plan |
| Container App | 0.25 vCPU and 0.5 GiB, 0 to 1 replicas, public HTTP ingress (supports WebSockets). Termination grace period of about 90 seconds, so the Recorder can flush while a paused database wakes up; the .NET host shutdown timeout is raised to match |
| Image | GitHub Container Registry (free for public images) instead of Azure Container Registry |
| App identity (user-assigned) | Lets the app sign in to Azure SQL without a password |
| CI identity (user-assigned) | Lets GitHub Actions deploy through a federated credential (OIDC) |
| Azure SQL | Server with Microsoft Entra-only authentication; one database on the free offer, set to pause when the monthly allowance runs out |
| Log Analytics and Application Insights | Receive the OpenTelemetry data. The workspace has a daily ingestion cap |
| Cost Management budget | €5 a month, with alerts at 50, 80 and 100 % (it only alerts; it does not stop spending) |

**No Key Vault:** with no passwords anywhere, there are no secrets to keep.

### 4.2 Domain and certificate

`bingo.webninja.fyi` uses a free managed certificate from Container Apps:
- A CNAME and a TXT `asuid` record in Cloudflare.
- The CNAME must be **DNS only** (grey cloud): Microsoft warns that an intermediate CNAME such as Cloudflare's proxy prevents issuing and renewing the certificate.
- `webninja.fyi` has no CAA records, so DigiCert, the issuer of these certificates, is not blocked.

### 4.3 Cost

It stays at €0 a month while usage stays within the free allowances.

| Item | Cost |
|---|---|
| Container | 180,000 vCPU-seconds and 360,000 GiB-seconds free per month per subscription. At 0.25 vCPU and 0.5 GiB, that is **about 200 hours running**. Scaled to zero, it costs nothing |
| Requests | 2 million a month free |
| Azure SQL | €0. Free offer: 100,000 vCore-seconds, 32 GB of data and 32 GB of backups per database per month, for up to 10 databases per subscription. It pauses when the allowance runs out |
| Observability | The first 5 GB a month of ingestion are free; the daily cap keeps us below that |
| Certificate, DNS and image registry | €0 |
| Beyond the free container hours | About $0.03 per running hour. **Not confirmed:** the figure comes from a third-party article quoting the Azure pricing API; the official page does not show it and the pricing API returns 0 |

### 4.4 Cold start

With scale to zero, the first visitor waits for the container to start. It is measured during verification. If it takes more than about 10 seconds, the app keeps one minimum replica during launch week, which would cost a few euros at the rate above. The chiseled base image (§5) helps it start fast.

## 5. CI/CD with GitHub Actions

1. **Test:** builds .NET 10, runs the unit and integration tests (Testcontainers starts SQL Server on the GitHub runner), and builds and tests the Angular app.
2. **Image:** a multi-stage Dockerfile (Node builds Angular, the .NET SDK publishes, a chiseled ASP.NET runtime image runs it with no shell and as a non-root user). It is pushed to `ghcr.io/ogires/meeting-bingo:<sha>`.
3. **Deploy,** only from `main`:
   - `azure/login` with **OIDC**. GitHub stores no secrets, only the tenant, subscription and client ids, which are not secret.
   - EF Core migrations run as a pipeline step, from a migrations bundle, never at app startup. The CI identity is the database's Entra administrator. The GitHub runner is not an Azure service, so its IP is allowed in the SQL firewall only during that step, and the rule is removed even if the job fails.
   - A T-SQL step creates the app identity as a database user with `db_datareader` and `db_datawriter` only. Bicep manages Azure resources but cannot reach inside the database.
   - `az containerapp update` switches to the new image, then a smoke test checks `/healthz`, the security headers and room creation.
4. **Infrastructure:** applied from the Azure CLI, always after a Bicep `what-if`. Applying it from a workflow would need an identity with far more rights than the deploy identity (it creates role assignments), so that is left for later, behind a GitHub environment with a required reviewer.

## 6. Failure handling and observability

| Failure | Behaviour |
|---|---|
| Connection drops | The SignalR client reconnects with increasing delays (`withAutomaticReconnect`) and rejoins with its token, recovering its card and marks |
| The room no longer exists (expired, or the server restarted) | A clear message, «Esta sala se ha cerrado», with a button to create another and the printable card |
| WebSockets blocked (corporate proxies) | SignalR falls back to Server-Sent Events or long polling |
| Room creation or the hub fails | The page offers the printable card, generated in the browser |
| Azure SQL paused, out of allowance or failing | Retries inside the Recorder window; otherwise the batch waits for the next window. The web never waits for SQL |
| Container shuts down (scale to zero) | Container Apps sends SIGTERM and the Recorder makes a last flush within the grace period. If time runs out, those summaries are lost and the loss is logged |
| Unhandled exception in the API | `IExceptionHandler` returns ProblemDetails, with no stack traces |
| Platform probes | `/healthz` does not touch SQL |

**Custom metrics** with `System.Diagnostics.Metrics`: open rooms, connected players, bingos, Recorder flushes (succeeded and failed) and summaries dropped from the channel. They show in the Aspire dashboard locally and in Application Insights in production, where a saved KQL query reports bingos per hour and failed flushes.

## 7. Security

With no accounts and no free text, the main risks are abuse, memory exhaustion and cheating.

### 7.1 Controls

- **Exact limits.** With a single replica, in-memory counters are exact.
  - **Room creation:** 10 per IP per hour, with the ASP.NET Core rate limiter.
  - **Hub calls:** an `IHubFilter` limits each connection to a few marks per second.
  - **Connections:** 10 per IP, 50 players per room, and the global caps from §2.5.
  - **Messages:** SignalR accepts at most 4 KB per message, with keep-alive and client timeouts.
- **The real client IP.** Behind the Container Apps ingress, the client IP arrives in `X-Forwarded-For`. `ForwardedHeaders` trusts only the platform's proxy; otherwise anyone could bypass the per-IP limits with a forged header.
- **Origin check.** The hub only accepts connections whose `Origin` is `bingo.webninja.fyi`, so another site cannot open connections from its visitors' browsers (cross-site WebSocket hijacking).
- **Input validation.** Room code format, cell index (0 to 24) and token are validated. Aliases are generated by the server, and Angular escapes all output by default.
- **Cheating.** Lines and bingos are always computed by the server from the card it dealt. Marking phrases nobody said cannot be prevented, but "Confirmado por N" makes it visible.
- **Room codes and tokens.** Rooms are short-lived and join attempts are rate-limited, which makes guessing one of the ~28.6 million codes impractical. The 128-bit player token only gives access to that player's card.
- **Headers.** A strict CSP (Angular's `autoCsp` computes the hashes of its scripts), HSTS, `X-Content-Type-Options`, `Referrer-Policy`, `frame-ancestors 'none'`, and `noindex` on rooms (`/s/*`).
- **No secrets.** Managed Identity for SQL, OIDC for GitHub Actions; no password exists anywhere.
- **Least privilege in SQL.** The app identity can only read and write data. Migrations run with the CI identity. EF Core always sends parameterised queries.
- **Network.** The SQL firewall allows Azure services, because the consumption plan has no fixed outbound IP. This is a documented concession: a private endpoint costs money.
- **Supply chain.** Dependabot for NuGet, npm and GitHub Actions; actions pinned by SHA; `npm ci` and `dotnet restore --locked-mode`.

### 7.2 OWASP Top 10 2025

| Category | Controls |
|---|---|
| **A01 Broken Access Control** | The 128-bit token is the only key to a card. The server validates every mark against the card it dealt. Hub connections only from the app's origin |
| **A02 Security Misconfiguration** | Security headers and strict CSP on every response; `noindex` on rooms; ProblemDetails with no internals; `ForwardedHeaders` limited to the platform proxy |
| **A03 Software Supply Chain Failures** | Lock files and locked-mode restores, Dependabot, actions pinned by SHA, a minimal chiseled base image |
| **A04 Cryptographic Failures** | HTTPS only, with a managed certificate and HSTS. Codes and tokens from a cryptographic random generator. No passwords to store |
| **A05 Injection** | Parameterised queries through EF Core. Inputs validated against fixed formats. No free text |
| **A06 Insecure Design** | Server-authoritative game; per-IP, per-room and global caps sized by a load test; this section is the threat model |
| **A07 Authentication Failures** | No login by design: anonymous identity per token, scoped to one card in one short-lived room |
| **A08 Software or Data Integrity Failures** | Idempotent, insert-only game summaries. Images are built only by CI from `main` and tagged with the commit SHA |
| **A09 Logging & Alerting Failures** | Structured logs and custom metrics in Application Insights; failed flushes and dropped summaries are counted. No IPs or tokens in logs |
| **A10 Mishandling of Exceptional Conditions** | Global exception handler; SQL failures never reach the player; graceful shutdown flush; bounded channel with a drop policy |

## 8. Testing

### 8.1 Layers

| Layer | Tools | What it covers |
|---|---|---|
| **Domain unit tests** (`MeetingBingo.Core`) | xUnit v3 and `FakeTimeProvider` | Card dealing (25 cells, free centre, no repeats, reproducible with a seed), the 12 lines and bingo, "confirmed" inside and outside the 60-second window, room expiry (5 minutes empty, 3 hours idle), the caps and the code format. No network or database: milliseconds |
| **API and hub integration** | `WebApplicationFactory` and the SignalR client against the TestServer | Two clients in one room: one marks and the other receives `PhraseMarked`. Server-validated bingo; rejoining with the token recovers the card; an out-of-range index is rejected; the 11th room from one IP gets a 429; a forged `X-Forwarded-For` from outside the proxy does not change the IP (`TestServer.SendAsync` sets the remote address); a foreign origin is rejected; ProblemDetails; security headers; `/healthz` does not touch SQL |
| **Real SQL integration** | Testcontainers (`Testcontainers.MsSql`) | Migrations apply from scratch. The Recorder writes a batch, and replaying it duplicates nothing. Stopping the host flushes what is pending. With SQL down, a game still finishes. The weekly statistics query |
| **Angular** | Vitest, Angular's default runner since version 21 | The card, the signal-based state with a mocked SignalR service, and the printable card |
| **Smoke test after deploy** | A workflow step | `/healthz` returns 200, the home page carries the headers, a room can be created through the API |

### 8.2 Decisions

- **No EF Core InMemory provider or SQLite** for database tests: they do not behave like SQL Server in transactions, types or retries, and Microsoft advises against InMemory for this. Testcontainers starts one container per test collection, not per test, because the image is large and slow to start.
- **WebApplicationFactory rather than `Aspire.Hosting.Testing`**, because Aspire runs only on developer machines and the tests must not depend on it.
- **Time is injected.** Everything uses `TimeProvider`; integration tests replace it through `WithWebHostBuilder`.
- **Minimal test doubles.** `FakeTimeProvider` instead of clock mocks and a real SQL Server instead of repository mocks. A mocking library comes in only if a dependency appears that cannot be started in a test. Plain xUnit assertions.

### 8.3 Load test

500 rooms of 50 players would be 25,000 connections in theory, far more than 0.25 vCPU and 0.5 GiB can be expected to hold. A small .NET console app using the SignalR client opens connections in rooms of 20 and marks cells against the container running locally with the same limits (`docker run --cpus 0.25 --memory 512m`). The global connection cap is set from the measured result with a safety margin, and the open-room cap is reviewed. It runs once, outside CI.

## 9. Verification in production

- Cold start time from zero replicas (§4.4).
- WebSockets through the Container Apps ingress, and the SSE/long-polling fallback.
- Security headers and CSP with the real build; `noindex` on rooms.
- Rate limits with the real client IP behind the ingress: the 11th room gets a 429, and a forged `X-Forwarded-For` does not reset the count.
- The app identity has only `db_datareader` and `db_datawriter`.
- After a week: SQL wake-ups and vCore-seconds used, container hours used and the actual cost against the budget.

## 10. Open points

- Region: West Europe or Spain Central, depending on service availability.
- The global connection cap and the open-room cap, from the load test.
- The Recorder window (6 hours to start), adjusted after a week of measured SQL consumption.
- Whether launch week needs a minimum replica, from the measured cold start.

## Sources

- [Azure Container Apps pricing](https://azure.microsoft.com/en-us/pricing/details/container-apps/)
- [Container Apps per-second price, third-party article](https://www.devzero.io/blog/azure-container-apps-pricing)
- [Custom domains and managed certificates in Container Apps](https://learn.microsoft.com/en-us/azure/container-apps/custom-domains-managed-certificates)
- [Azure SQL Database free offer FAQ](https://learn.microsoft.com/en-us/azure/azure-sql/database/free-tier-faq)
- [Azure Retail Prices API](https://prices.azure.com/api/retail/prices)
- [What's new in Aspire 13](https://aspire.dev/whats-new/aspire-13/)
