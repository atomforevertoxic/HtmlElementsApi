# HtmlElementsApi — тестовое задание (REST API, .NET 10)

REST API одной функции: принимает JSON (CSS-селектор, HTML-страница, URL, шифротекст, ключ),
парсит страницу через AngleSharp, находит элементы и email, расшифровывает текст AES-256,
пишет результат в PostgreSQL и возвращает JSON-ответ с отступами.

Стек: **.NET 10 / ASP.NET Core** (контроллер + сервис), FluentValidation, AngleSharp,
Dapper + **PostgreSQL 18**, AES-256 (ECB, `PaddingMode.None`), System.Text.Json, Swagger.

---

## Быстрый старт

Требуется запущенный **Docker** (Docker Desktop).

```bash
docker compose up -d
```

| Сервис | Адрес | Примечание |
|---|---|---|
| API | http://localhost:8090 | приложение |
| Swagger UI | http://localhost:8090/api/swagger | удобное тестирование |
| pgAdmin | http://localhost:8080 | **открывается сразу, без логина и пароля** |

- Контейнер `api` монтирует папку с исходниками и **каждый раз заново собирает и запускает** приложение
  (`rm -rf bin obj && dotnet restore && dotnet build && dotnet run`). Первый запуск: скачивание
  образов (~1–2 ГБ) + restore/build, занимает несколько минут; повторные — ~20–60 секунд.
- БД переживает перезапуск: `docker compose down` — данные в volume сохраняются;
  `docker compose down -v` — полный сброс (включая данные elements и настройки pgAdmin).
- pgAdmin: клик по серверу `elements-db` подключает **без ввода пароля** (см. «Ключевые решения»).

## Структура проекта

Минимальный набор (контроллер, сервис, модели) + инфраструктура:

```
Controllers/ElementsController.cs   # POST /api/elements
Services/ElementsService.cs         # вся бизнес-логика
Models/                             # запрос, ответ, error-коды
Validators/                         # FluentValidation
Program.cs                          # DI, JSON, Swagger, DDL при старте
compose.yml                         # api + postgres:18-alpine + pgadmin
pgadmin/servers.json                # преднастроенный сервер БД для pgAdmin
json_payload_1.txt, json_payload_2.txt   # тестовые входные данные
web-page.txt                            # исходный HTML (для сверки)
json_result_1.txt, json_result_2.txt     # ответы API на payload_1/2 (в корне)
```

---

## API: `POST /api/elements`

Content-Type: `application/json`. Имена полей — `snake_case`, JSON сериализуется с отступами.

### Запрос

| Поле | Тип | Описание |
|---|---|---|
| `selector` | string | CSS-селектор (не пустой) |
| `attribute` | string | имя HTML-атрибута, например `href` (не пустой) |
| `url_b64` | string | URL, закодированный в Base64 |
| `encrypted_text_bytes_b64` | string | AES-256 шифротекст в Base64 (длина кратна 16) |
| `key_bytes_b64` | string | ключ в Base64 (16, 24 или 32 байта) |
| `page_b64` | string | HTML-код страницы в Base64 |

### Ответ

**Всегда HTTP 200** — результат или ошибка; ошибки кодируются в `is_error` / `error_code`.

| Поле | Тип | Описание |
|---|---|---|
| `is_error` | int | 0 — успех, 1 — ошибка |
| `error_code` | string | текстовый код ошибки (см. таблицу), пустой при успехе |
| `error_message` | string | сообщение ошибки (для «иных ошибок» — текст исключения) |
| `elements_count` | int | сколько элементов нашёл селектор |
| `emails_count` | int | сколько email найдено регулярным выражением |
| `url` | string | URL в открытом виде |
| `decrypted_plain_text` | string | расшифрованный текст |
| `elements_attr_list` | string[] | значения атрибутов найденных элементов |
| `emails_list` | string[] | найденные email |

### Коды ошибок

| `error_code` | Когда |
|---|---|
| `MISSING_PARAMETER` | отсутствует обязательное поле / пустое тело / битый JSON |
| `EMPTY_SELECTOR` | `selector` пустой |
| `EMPTY_ATTRIBUTE` | `attribute` пустой |
| `INVALID_URL_BASE64` | `url_b64` не является корректным Base64 |
| `INVALID_PAGE_BASE64` | `page_b64` не является корректным Base64 |
| `INVALID_KEY_BASE64` | ключ не Base64 или длина не 16/24/32 байта |
| `INVALID_CIPHERTEXT_BASE64` | шифротекст не Base64 или не кратен 16 байтам |
| `CSS_SELECTOR_ERROR` | AngleSharp не смог применить селектор |
| `AES_DECRYPT_ERROR` | ошибка расшифровки (неверный ключ/данные/нешифротекст) |
| `DB_ERROR` | БД недоступна (поля ответа при этом уже посчитаны) |
| `INTERNAL_ERROR` | прочие непредвиденные ошибки |

### Пример запроса

```bash
curl -X POST http://localhost:8090/api/elements \
  -H "Content-Type: application/json" \
  --data @json_payload_1.txt
```

---

## Тестовые данные и ожидаемый результат

| Вход | `elements_count` | `emails_count` | `url` |
|---|---|---|---|
| `json_payload_1.txt` | **238** (`a[href]`) | **5** | `https://test.com/page1` |
| `json_payload_2.txt` | **9** (`script[src]`) | **5** | `https://test.com/page123` |

Оба запроса: `is_error = 0`, `decrypted_plain_text` =
`AES Error: Object reference not set to an instance of an object.`,
в `emails_list` — 5 адресов, включая дубль `letters@rbc.ru`
(`webmaster@rbc.ru`, `privet@test.com`, `hh_test_task@gmail.com`, `letters@rbc.ru` ×2).

Готовые ответы лежат в корне: **`json_result_1.txt`**, **`json_result_2.txt`**.
После обоих запросов в таблице `elements` — **247 строк** (238 + 9).

## Как проверить

1. **Swagger:** открыть http://localhost:8090/api/swagger → `POST /api/elements` → *Try it out* →
   вставить содержимое `json_payload_1.txt` → Execute.
2. **Негатив:**
   ```bash
   # пустой selector -> 200, is_error=1, error_code=EMPTY_SELECTOR
   curl -s -X POST http://localhost:8090/api/elements \
     -H "Content-Type: application/json" \
     -d '{"selector":"","attribute":"href","url_b64":"aHR0cHM6Ly90ZXN0LmNvbS9wYWdlMQ==","encrypted_text_bytes_b64":"YmJiYmJiYmJiYmJiYmJiYg==","key_bytes_b64":"YWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWE=","page_b64":"PHA+eDwvcD4="}'
   ```
3. **pgAdmin:** http://localhost:8080 (без логина) → в дереве `elements-db` →
   `Databases` → `elements_db` → `Schemas` → `public` → `Tables` → `elements`;
   правый клик → *View/Edit Data → All Columns* или Query Tool (F5):
   ```sql
   SELECT count(*) FROM elements;   -- 247
   ```

---

## Async и потоки (п. 11 ТЗ)

Общая идея: **async нужен там, где есть ожидание** — сети, диска, другого потока.
В REST API это прямая выгода: во время ожидания I/O поток из пула Kestrel **не блокируется**,
а освобождается на обслуживание других запросов. Если делать синхронный I/O, то под N
конкурентными запросами потребуется N заблокированных потоков (память, переключение контекста,
деградация throughput); с async — размер пула ограничен реальной CPU-работой. Плюс сквозная
отмена через `CancellationToken` (`HttpContext.RequestAborted`): клиент отключился — запрос к БД
и вся обработка отменяются. Синхронный код поверх async (`.Result` / `.Wait()`) не используется:
риск дедлока и непредвиденные блокировки.

**Где в проекте async:**

| Место | Файл |
|---|---|
| `async Task<ActionResult<...>> Post(...)`, `await ValidateAsync(...)`, `await ProcessAsync(...)` | `Controllers/ElementsController.cs:22,33,41` |
| парсинг HTML — `await parser.ParseDocumentAsync(page, ct)` | `Services/ElementsService.cs:57` |
| расшифровка — `CryptoStream` + `await reader.ReadToEndAsync(ct)` (async-чтение, не `Read`) | `Services/ElementsService.cs:96-98` |
| БД — `OpenConnectionAsync`, `BeginTransactionAsync`, Dapper `ExecuteAsync`, `CommitAsync` | `Services/ElementsService.cs:109-113` |
| инициализация DDL при старте | `Program.cs:45-88` |

**Где сознательно синхронно и почему:**

| Операция | Обоснование отказа от async |
|---|---|
| `Convert.FromBase64String` (декодинг url/page/key/шифротекста) | чистая CPU-операция над `byte[]` в памяти, микросекунды; async-аналога в BCL нет и не нужен — ожидания внешнего ресурса нет |
| регулярное выражение по email (`Regex.Matches`) | в .NET нет async для regex — выражение **скомпилировано** (`RegexOptions.Compiled`, `Services/ElementsService.cs:24-27`) и защищено таймаутом 5 сек; алгоритмически это CPU-работа по строке в памяти, async здесь не сократил бы время ответа |
| `document.QuerySelectorAll`, `GetAttribute`, `OuterHtml` | CPU-операции AngleSharp по уже загруженному DOM; async-парсинга хватает, выборка синхронна по API библиотеки |
| `UTF8Encoding.GetString` (строгий UTF-8) | синхронное преобразование байтов в строку, без I/O |
| сериализация ответа `System.Text.Json` | выполняется фреймворком ASP.NET Core на этапе вывода ответа; в прикладном коде для неё нет точки async-включения |
| правила FluentValidation | проверки строк в памяти (`NotNull/NotEmpty/Must`); `ValidateAsync` используется для единообразия пайплайна и отмены, сами правила синхронны |
| `Task.Run` не применяется | CPU-работу искусственно на ThreadPool не выбрасываем — это добавляет переключения контекста, не ускоряя ответ |

Подробнее: [Асинхронные программы в C#](https://learn.microsoft.com/ru-ru/dotnet/csharp/programming-guide/concepts/async/),
[Async overview](https://learn.microsoft.com/ru-ru/dotnet/csharp/async).

---

## Ключевые решения

- **pgAdmin без пароля:** БД в режиме `trust` (`POSTGRES_HOST_AUTH_METHOD=trust`) — пароль не нужен
  в принципе. Сам pgAdmin тем не менее показывает диалог пароля, если у сервера нет ни сохранённого
  пароля, ни `passfile` (это поведение UI, код возвращает 428 **до** подключения). Поэтому в
  `pgadmin/servers.json` задан маркер `PassFile` на несуществующий файл: pgAdmin перестаёт спрашивать
  пароль, libpq отсутствие файла игнорирует, соединение открывается сразу и без пароля.
- **JSON:** `JsonNamingPolicy.SnakeCaseLower` + `WriteIndented = true` (`Program.cs:14-15`);
  имена полей также закреплены `[JsonPropertyName]`.
- **Порядок шагов в сервисе:** запись в БД — **последним** шагом: при недоступной БД ответ
  (`DB_ERROR`) уже содержит посчитанные `elements_count`, `emails_count`, `url`,
  `decrypted_plain_text` и списки.
- **Ответы на ошибки валидации/биндинга JSON** тоже структурированные (HTTP 200, `is_error=1`):
  авто-400 из модели отключён (`SuppressModelStateInvalidFilter`, `Program.cs:19`).
- **DDL при старте** (`CREATE TABLE IF NOT EXISTS elements ...`) с retry 15 × 2 сек:
  приложение дожидается БД, но и без неё запускается (лог-ворнинг) — запросы вернут `DB_ERROR`.
- Валидация — FluentValidation с `CascadeMode.Stop`, каждое правило с `WithErrorCode`.

## Заметки по окружению

- **Рекомендуемый запуск — Docker.** На Windows с включённым **Smart App Control** локальный запуск
  (`dotnet run`) может падать с `FileLoadException` на `AngleSharp.dll` (системная блокировка файла,
  а не угроза в коде). В Linux-контейнере этого нет вовсе.
- **Локальный запуск без Docker:** строка БД в `appsettings.json` указывает на хост `db` (из
  compose-сети). Локально переопределите её, например:
  `set ConnectionStrings__ElementsDb=Host=localhost;Port=5432;Database=elements_db;Username=...`
  и запустите `dotnet run`. Без доступной БД приложение всё равно поднимется, а запросы будут
  отвечать `DB_ERROR` (остальные поля ответа считаются как обычно).
