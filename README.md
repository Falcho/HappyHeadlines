# HappyHeadlines

HappyHeadlines is a C#/.NET 10 school project for **Development of Large Systems**. It simulates a positive-news platform and demonstrates REST APIs, microservices, scaling, messaging, caching, and monitoring. APIs are demonstrated through Postman; newsletter delivery is simulated through logs.

## How it works

| Service | Responsibility |
| --- | --- |
| DraftService | Saves and retrieves unfinished articles. |
| PublisherService | Checks articles through ProfanityService and publishes approved articles to RabbitMQ. |
| ArticleService | Consumes published articles, stores them, and provides REST CRUD and recent-article endpoints. |
| CommentService | Checks comments for profanity, stores them, and retrieves comments by article ID. |
| ProfanityService | Checks submitted text against prohibited words stored in its database. |
| NewsletterService | Consumes published articles for immediate newsletters and periodically retrieves recent articles for daily newsletters. |

PublisherService sends an `ArticlePublished` message to a RabbitMQ fanout exchange. Separate queues deliver a copy to ArticleService and NewsletterService, allowing them to process it independently. The three ArticleService replicas share the article-storage queue, so they compete to process each message rather than each storing a separate copy.

## Why it is built this way

- **Separate services:** Each has a focused responsibility and owns its data access. This makes service boundaries and distributed communication visible for learning.
- **REST and RabbitMQ:** REST handles direct requests, such as retrieving an article or checking profanity. RabbitMQ separates publishing from the consumers that store articles and simulate newsletters.
- **Scaling:** Nginx distributes HTTP requests across three ArticleService replicas (X-axis scaling). Article data is split into eight SQLite databases by continent/global scope (Z-axis scaling). The replicas share those files through a Docker volume; this is a local simulation, not geographically distributed storage.
- **Dependency injection:** Controllers and workers receive their dependencies through constructors. A singleton cache is shared within one service process; each replica still has its own cache.
- **`async`/`await` and `Task`:** Database, HTTP, and messaging operations involve waiting for I/O. Awaiting asynchronous operations allows the request thread to be released while waiting. It does not automatically create a new thread or make the operation itself faster. `Task` represents completion; `Task<T>` also carries a result. Callers can await these methods and observe errors. We avoid `async void` for these operations because callers cannot await it.
- **`void` and `static` are separate choices:** `void` means a method returns no result, and is appropriate for short synchronous operations such as updating a cache. `static` means a member belongs to the class rather than an instance; a method can be both `static` and `async`. Instance methods let controllers and workers use their injected dependencies.
- **Enums:** `ArticleScope` gives the supported scopes explicit names instead of scattering string values through the code.

## Caching and monitoring

**ArticleCache** is refreshed in the background at startup and every minute. It contains global articles published within the latest 14 days. Individual global-article reads check it first and fall back to the database on a miss. The daily-newsletter endpoint retains its separate one-day database query. Cached changes become visible after the next successful refresh.

**CommentCache** loads all comments for an article on a cache miss. It holds entries for at most **30 articles**, not 30 comments. Accessing an entry makes it most recently used; adding a 31st evicts the least recently used entry (LRU). Saving a comment invalidates that article's cached entry. This supports the sequential demonstration; concurrent database reads and writes can still race with cache filling.

Locks protect the in-memory collections. Cache contents and counters reset when a service restarts.

- **Seq + OpenTelemetry:** Central logs and connected traces across HTTP calls and RabbitMQ messages.
- **Prometheus:** Collects `/metrics` every five seconds from all three ArticleService replicas and CommentService.
- **Grafana:** Displays each cache's hit ratio: `hits / (hits + misses) × 100`. Article counters are summed across replicas before calculating the ratio. Panels use current process totals, not a rolling time window. Before any lookups, they show **No requests yet**.

## Run locally

Requirements: .NET 10 SDK, Docker Desktop with Linux containers, and Postman. Run commands from this `HappyHeadlines` directory.

Start infrastructure and build the article image:

```powershell
docker compose up -d rabbitmq seq prometheus grafana
docker compose exec rabbitmq rabbitmq-diagnostics -q ping
docker compose build article-service-1
```

Wait for the RabbitMQ check to succeed. Start the following services in **separate terminals**, leaving each running. Start PublisherService before the article replicas and NewsletterService: it declares the exchange and queues on a fresh installation.

```powershell
dotnet run --project src/ProfanityService --no-launch-profile --urls http://localhost:5055
dotnet run --project src/PublisherService --no-launch-profile --urls http://localhost:5058
```

After PublisherService starts:

```powershell
docker compose up -d article-service-1 article-service-2 article-service-3 load-balancer
```

Then, in separate terminals:

```powershell
dotnet run --project src/CommentService --no-launch-profile --urls http://localhost:5056
dotnet run --project src/DraftService --no-launch-profile --urls http://localhost:5057
dotnet run --project src/NewsletterService --no-launch-profile --urls http://localhost:5059
```

ArticleService is available through Nginx at `http://localhost:8080`. Other services use the ports above. After shutting down the PC, restart both the containers and the services running in terminals. If article containers exited before RabbitMQ was ready, restart them once RabbitMQ is healthy.

## Import the Grafana dashboard

Prometheus loads the repository's [prometheus.yml](prometheus.yml) through Docker Compose; it does not need a dashboard import.

1. Check [Prometheus targets](http://localhost:9090/targets): three article targets and one comment target should be **UP**. CommentService is reached through Docker Desktop's `host.docker.internal` address.
2. Open [Grafana](http://localhost:3000). On a fresh installation, sign in with `admin` / `admin` and follow the password prompt.
3. Under **Connections → Data sources**, add **Prometheus**. Set its URL to `http://prometheus:9090`, then select **Save & test**. This is the Docker service address, not the browser's `localhost`.
4. Go to **Dashboards → New → Import**, upload [happyheadlines-cache-dashboard.json](monitoring/happyheadlines-cache-dashboard.json), select your Prometheus data source when prompted, and import it.
5. Open **HappyHeadlines — Cache monitoring**. It refreshes every five seconds. The export uses Grafana's v2 dashboard format, exported from Grafana 13.2.3; use a compatible version.

Grafana dashboards and Prometheus history persist in Docker volumes. The JSON export lets other group members recreate the dashboard on their own machines. Keep all four scrape targets healthy when demonstrating the combined ratios.

## Short demonstration

1. Publish a clean article through `POST http://localhost:5058/publications`; inspect its connected trace in [Seq](http://localhost:8081).
2. Request a recent global article using `GET http://localhost:8080/articles/global/{id}`. Inspect `X-Article-Cache` and `X-Article-Service-Instance` in Postman.
3. Request `GET http://localhost:5056/comments/article/{articleId}` twice: expect **MISS**, then **HIT**. Add a comment and repeat to demonstrate invalidation.
4. Run the prepared LRU test: fill 30 entries, revisit the first, add a 31st, and show that the second was evicted.
5. Show both Grafana percentages changing as requests are made. Scraping and dashboard refresh introduce a short delay.

The daily newsletter timer runs every 24 hours from service startup. `POST http://localhost:5059/newsletters/daily` triggers the same sender immediately for demonstration. Neither path sends real email.
