# Sanitize Service

### Prerequisites

- .NET 8 SDK
- Docker (for containerized run)

.NET 8 microservice that accepts uploads, detects file format from **content** (not the filename), runs format-specific sanitization, and returns the sanitized bytes.

Main endpoint: `POST /sanitize`

## Run locally

From the solution directory (folder containing `SanitizeService.sln`):

```bash
dotnet build
dotnet run --project src/SanitizeService.Api/SanitizeService.Api.csproj
```

Alternatively:

```bash
cd src/SanitizeService.Api
dotnet run
```

The API listens on the URLs shown in the console. The `http` profile in `Properties/launchSettings.json` defaults to **http://localhost:5149**.

### ABC format note

The ABC format is treated as a **continuous binary byte sequence**, not as a line-based text format. Line breaks (e.g. `\n`) are considered part of the data and may invalidate the structure.

### Sample files

In the repository root:

| File | Description |
|------|-------------|
| `sample-valid.abc` | Valid ABC: header `123`, one `A1C` block, footer `789`. |
| `sample.abc` | Malicious example (`AFC` in the body); sanitization replaces that block with `A255C` → output `123A1CA3CA255C789`. |
| `sample-invalid.abc` | Invalid structure (body between header/footer not a multiple of 3 bytes); API returns **400**. |

### Example: sanitize (local)

With the API listening on port **5149**:

```bash
curl -X POST http://localhost:5149/sanitize -F "file=@sample-valid.abc" -o output-valid.abc
curl -X POST http://localhost:5149/sanitize -F "file=@sample.abc" -o output.abc
```

Run these from the solution directory so paths to `sample.abc` resolve. For `sample.abc`, the sanitized output is `123A1CA3CA255C789` (the invalid `AFC` block becomes `A255C`).

### Error example

Errors are returned as **Problem Details** (`application/problem+json`). Example when the ABC body between header and footer is not a multiple of 3 bytes (message from the sanitizer):

```json
{
  "status": 400,
  "title": "Bad Request",
  "detail": "ABC body (between header and footer) must be a whole number of 3-byte blocks. Body length=1."
}
```

## Running with Docker

The container listens on **port 8080** inside (`ASPNETCORE_URLS=http://+:8080`). Map a host port to **8080** (example below uses host **5000**, like the earlier README).

1. **Build the Docker image**

   ```bash
   docker build -t sanitize-service .
   ```

2. **Run the container**

   ```bash
   docker run --rm -p 5000:8080 -e ASPNETCORE_ENVIRONMENT=Development sanitize-service
   ```

   If **5000** (or **8080** on the host) is already in use, pick another host port, e.g. `-p 8081:8080`.

3. **Swagger UI**

   Open [http://localhost:5000/swagger](http://localhost:5000/swagger) (use the same host port you mapped).

   - Swagger is only registered when `ASPNETCORE_ENVIRONMENT=Development`. Without it, **`POST /sanitize` still works**; Swagger UI is not served.
   - The API always listens on **8080** inside the container; `-p host:8080` chooses the URL on your machine.

**Quick test (curl)**

```bash
curl -X POST "http://localhost:5000/sanitize" -F "file=@sample.abc" -o output.abc
```

Run `curl` from the directory where `sample.abc` lives, or pass an absolute path to `-F`.

## Configuration

| Setting | Description |
|--------|-------------|
| `Sanitization:MaxFileSizeBytes` | Maximum uploaded **file** size for the sanitize endpoint (and `SeekableStreamEnsurer`); default 100 MB. Future endpoints can use different limits in code while this stays the sanitize policy. |
| `Sanitization:MaxRequestBodyBytes` | Host-wide ceiling for the **raw HTTP request body** (Kestrel + multipart). Independent of `MaxFileSizeBytes`; set it ≥ the largest per-route file limit plus multipart overhead (default 100 MB + 256 KB). |

The sanitize action compares `IFormFile.Length` to `MaxFileSizeBytes` only. Kestrel uses `MaxRequestBodyBytes`.

### Logging

[Serilog](https://serilog.net/) writes to the console and to **rolling files** under `src/SanitizeService.Api/logs/` (e.g. `sanitize-YYYYMMDD.log`). Levels and sinks are configured in `appsettings.json` under the `Serilog` section. The `logs/` folder is gitignored.

When the app runs **in Docker**, those files are under **`/app/logs`** inside the container. To inspect them, use **`docker exec`** on the running container, or mount a host directory over **`/app/logs`** so logs persist on the host.

Optional:

```cmd
docker run -p 5000:8080 -v %cd%/logs:/app/logs sanitize-service
```

(From a shell where `%cd%` is not expanded, use e.g. `-v ./logs:/app/logs` instead.)

## Tests

```bash
dotnet test
```

## Architecture

- **SanitizeService.Api** — HTTP, DI bootstrap, exception mapping.
- **SanitizeService.Application** — `ISanitizationService`, `IFileSanitizerFactory`, `SanitizationService`, `SeekableStreamEnsurer`, `CompositeFileFormatDetector`, `FileSanitizerFactory`, application exceptions (`UnsupportedFormatException`, `FileSizeExceededException`).
- **SanitizeService.Domain** — `FileFormat`, `IFileFormatDetector` (facade), `IFileFormatProbe` (per-format probes), `AbcFormatProbe`, `AbcFileSanitizer`, domain exceptions.

### Format detection and streams

`IFileFormatProbe` implementations should rewind a seekable stream to position `0` after probing. `CompositeFileFormatDetector` resets the stream before **each** probe so a partial read in one probe cannot break the next.

Adding a new format: implement `IFileFormatProbe` + `IFileSanitizer`, register `AddSingleton<IFileFormatProbe, …>` and `AddSingleton<IFileSanitizer, …>` in `AddSanitization`. No changes to the composite class.
