# Week 45 — Sustainability: three GAF tactics

Assignment: pick three tactics from the Green Architecture Framework (GAF) that we have not actively worked on yet, improve HappyHeadlines with them, and document the process with before/after code snippets.

## Tactics we had already used, and therefore did not choose

| GAF tactic | Where it already exists |
| --- | --- |
| Cache Data Closer to User | `ArticleCache` and `CommentCache` (week 39/40) |
| Utilise Geographical Sharding | Eight `articles_<scope>` databases (Z-axis) |
| Independent scaling of modular services | Three ArticleService replicas behind Nginx (X-axis) |
| Fine-grained service model | The microservice split itself |

## Chosen tactics

| # | GAF tactic | Category | Change |
| --- | --- | --- | --- |
| 1 | **Reduce Logging** | Accept Lower Fidelity | Framework logs for SQL commands and outgoing HTTP calls are raised to `Warning` in all seven services. |
| 2 | **Defer Fetching / Lazy fetch** | Reduce Network Package Size | The daily newsletter fetches subscribers only when there are articles to send. |
| 3 | **Pool Resources (Threads / Connections)** | Utilise your Resources Efficiently | Four services reuse `DbContext` instances from a pool instead of creating one per request. |

---

## 1. Reduce Logging

**Problem.** Every request produced several log entries that nobody reads. EF Core wrote every SQL statement at `Information`, and `HttpClient` wrote four entries per outgoing call. They were sent to the console and over the network to Seq, where they were stored. The OpenTelemetry exporter even logged its own HTTP calls to Seq, so exporting logs created more logs.

**Before** — `src/<Service>/appsettings.json` (all seven services):

```json
"LogLevel": {
  "Default": "Information",
  "Microsoft.AspNetCore": "Warning"
}
```

**After:**

```json
"LogLevel": {
  "Default": "Information",
  "Microsoft.AspNetCore": "Warning",
  "Microsoft.EntityFrameworkCore.Database.Command": "Warning",
  "System.Net.Http.HttpClient": "Warning"
}
```

**Why only these two categories.** Our own log messages (e.g. *Simulated welcome mail*, *Created draft*) stay at `Information`, so the demonstration in Seq still works. Errors and warnings from EF Core and HttpClient are still logged. Traces are not affected, so SQL and HTTP calls are still visible as spans in Seq.

**Measured** — 5 × `POST /comments` + 5 × `GET /comments/article/{id}`, counting log entries:

| Service | Before | After |
| --- | --- | --- |
| CommentService | 34 | 0 |
| ProfanityService | 5 | 0 |

The breakdown before was 20 HttpClient entries (ProfanityService calls), 10 EF Core SQL entries, and 4 entries from the log/trace exporter itself.

---

## 2. Defer Fetching / Lazy fetch

**Problem.** The daily newsletter always fetched the full subscriber list from SubscriberService, even on a day with no new articles, when nothing is sent. That is a network call and a database query done for nothing.

**Before** — `src/NewsletterService/Services/DailyNewsletterSender.cs`:

```csharp
var articles = await client.GetFromJsonAsync<List<ArticlePublished>>(
    "/articles/recent", _jsonOptions, cancellationToken)
    ?? throw new JsonException("Article list was null.");

var subscribers =
    await _subscriberClient.GetSubscribersAsync(cancellationToken);
```

**After:**

```csharp
var articles = await client.GetFromJsonAsync<List<ArticlePublished>>(
    "/articles/recent", _jsonOptions, cancellationToken)
    ?? throw new JsonException("Article list was null.");

// Defer fetching: subscribers are only needed when there is
// something to send, so an empty day costs no call to SubscriberService.
if (articles.Count == 0)
{
    _logger.LogInformation(
        "Daily newsletter skipped: no new articles.");

    return (0, null);
}

var subscribers =
    await _subscriberClient.GetSubscribersAsync(cancellationToken);
```

**Measured** — `POST /newsletters/daily` with no recent articles, and SubscriberService not running:

| | Before | After |
| --- | --- | --- |
| Calls to SubscriberService | 1 (timed out after 3 s) | 0 |
| Response time | 3.20 s | 0.13 s |
| Response | `sent: false` | `sent: false` (unchanged) |

There is also a side effect for fault isolation: an empty day no longer counts as a failed call in the circuit breaker.

---

## 3. Pool Resources (Connections)

**Problem.** `AddDbContext` creates a new `DbContext` for each HTTP request and throws it away afterwards. Each one allocates change tracking and internal services that must later be garbage collected. Npgsql already pools the database connections themselves; this change also pools the EF Core objects around them.

**Before** — `src/CommentService/Program.cs`, and the same line in DraftService, ProfanityService and SubscriberService:

```csharp
builder.Services.AddDbContext<CommentDbContext>(options =>
    options.UseNpgsql(connectionString));
```

**After:**

```csharp
builder.Services.AddDbContextPool<CommentDbContext>(options =>
    options.UseNpgsql(connectionString));
```

**How it works.** When a request ends, the context is reset and returned to a pool (default size 1024) rather than being disposed. The next request receives the same object, so less memory is allocated and less CPU goes to garbage collection. Our contexts hold no per-request state, which is the condition for pooling. Controllers still receive the context through the constructor, so no other code changes.

ArticleService is not changed: it chooses a shard database per request through `ArticleDbContextFactory`. A single pool would only cover one connection string.

**Verified** — after the change, all four services were run against PostgreSQL. Comments: 5 × 201, 5 × 200; subscribers: 3 × 201, 3 × 204; a draft was created and read back (200). ProfanityService was exercised through the comment checks. No errors were logged.
