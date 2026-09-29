# HappyHeadlines ArticleService

Implements the assignment with:

- REST CRUD for articles
- X-axis split: 3 ArticleService instances
- Nginx round-robin load balancer
- Z-axis split: 8 SQLite database shards

## Run

```bash
docker compose up --build
```

Open Swagger UI at:

```text
http://localhost:8080/docs
```

## CRUD endpoints

- `POST /articles`
- `GET /articles/{article_id}`
- `PUT /articles/{article_id}`
- `DELETE /articles/{article_id}`

## Create body

```json
{
  "title": "A positive European story",
  "content": "Article content...",
  "scope": "europe"
}
```

Valid scopes:

- africa
- antarctica
- asia
- australia
- europe
- north_america
- south_america
- global

## Z-axis database split

The create request chooses a shard from `scope`.

For example:

```text
scope=europe -> article_europe.db
scope=global -> article_global.db
```

The generated article id includes the scope, for example:

```text
europe_d74694d0-...
```

GET, PUT and DELETE can therefore derive the correct database from the id without searching all eight databases.

## X-axis service split

```text
                +--> ArticleService 1
Client -> Nginx +--> ArticleService 2
                +--> ArticleService 3
```

Nginx uses round-robin balancing by default.

Each response contains the header:

```text
X-Article-Service-Instance
```

Repeated requests will show which instance handled each call.
