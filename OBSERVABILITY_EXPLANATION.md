# Understanding Observability in This Project

## What is Observability?

**Observability** is the ability to understand what's happening inside your application by examining its outputs (logs, metrics, traces) without modifying the code. Think of it like having a dashboard in your car that shows speed, fuel level, and engine temperature - you can see what's happening without opening the hood.

In software, observability helps you:
- **Debug problems** - Find out why something failed
- **Monitor performance** - See if your app is slow or using too many resources
- **Track user requests** - Follow a request as it moves through different services
- **Understand system behavior** - See patterns and trends over time

## The Three Pillars of Observability

1. **Logs** - Text records of events (like a diary)
2. **Metrics** - Numerical measurements (like speedometer readings)
3. **Traces** - Following a request through multiple services (like GPS tracking)

---

## How Observability is Implemented in This Project

This is a **Code Review Application** with three services:
- **React Frontend** (Port 3000)
- **.NET API** (Port 5116) 
- **PHP Service** (Port 8000)

Let's see how observability works across these services:

---

## 1. Correlation IDs - Tracking Requests Across Services

### What is a Correlation ID?
A **Correlation ID** is a unique identifier attached to every request. It's like a tracking number for a package - you can follow it as it moves through different services.

### How It Works in This Project

#### Step 1: Request Comes In
When a user makes a request from the React app to the .NET API:

```csharp
// CorrelationIdMiddleware.cs
var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault() 
    ?? Guid.NewGuid().ToString("N")[..8];  // Generate if missing

context.Items["CorrelationId"] = correlationId;  // Store for later use
context.Response.Headers["X-Correlation-ID"] = correlationId;  // Send back to client
```

**Example**: A request gets correlation ID `a3f5b2c1`

#### Step 2: .NET API Calls PHP Service
When the .NET API needs to analyze code, it forwards the correlation ID:

```csharp
// AnalysisService.cs (line 724)
var client = _httpClientFactory.CreateClient();
client.DefaultRequestHeaders.Add("X-Correlation-Id", correlationId);  // Forward the ID
var response = await client.PostAsync(phpServiceUrl, jsonContent);
```

#### Step 3: PHP Service Receives and Uses It
The PHP service captures the correlation ID:

```php
// public/index.php (line 12-14)
$correlationId = $request->getHeaderLine('X-Correlation-Id');
if (empty($correlationId)) {
    $correlationId = uniqid('php-', true);  // Generate if missing
}
```

#### Step 4: All Logs Include the Correlation ID
Now every log message includes this ID, making it easy to trace:

```
[a3f5b2c1] Starting branch analysis. UserId: 123, RepoUrl: github.com/user/repo, Branch: main
[a3f5b2c1] Sending 15 files to PHP analysis service
[a3f5b2c1] PHP analysis complete. Issues: 42, Score: 85.5
[a3f5b2c1] Analysis complete. RunId: 456, Score: 85.5, Files: 15, Issues: 42, Duration: 3.2s
```

**Benefit**: If something fails, you can search logs for `a3f5b2c1` and see the entire request journey!

---

## 2. Structured Logging - Consistent, Searchable Logs

### What is Structured Logging?
Instead of random log messages, structured logging uses a consistent format with key-value pairs. This makes logs searchable and analyzable.

### Implementation in This Project

#### .NET API Logging Format
```csharp
// AnalysisService.cs (line 78-80)
_logger.LogInformation(
    "[{CorrelationId}] Starting branch analysis. UserId: {UserId}, RepoUrl: {RepoUrl}, Branch: {BranchName}",
    correlationId, userId, RedactSensitiveData(request.RepoUrl), request.BranchName);
```

**Output**:
```
[a3f5b2c1] Starting branch analysis. UserId: 123, RepoUrl: github.com/user/repo, Branch: main
```

#### Request Logging Middleware
Every HTTP request is automatically logged:

```csharp
// RequestLoggingMiddleware.cs (line 26, 43-44)
_logger.LogInformation("[{CorrelationId}] Request Started: {Method} {Path}{Query}", ...);
_logger.LogInformation("[{CorrelationId}] Request Completed: {Method} {Path} - Status: {StatusCode} in {Duration}ms", ...);
```

**Example Output**:
```
[a3f5b2c1] Request Started: POST /api/analysis/analyze
[a3f5b2c1] Request Completed: POST /api/analysis/analyze - Status: 200 in 3200ms
```

#### PHP Service Logging
```php
// public/index.php (line 18)
error_log(sprintf("[%s] Incoming Request: %s %s", $correlationId, $request->getMethod(), $request->getUri()->getPath()));
```

**Benefit**: You can search logs by correlation ID, user ID, or any field to find related events!

---

## 3. Metrics Collection - Measuring System Performance

### What are Metrics?
Metrics are numerical measurements collected over time. Like tracking:
- How many requests per minute?
- Average response time?
- How many errors occurred?

### Implementation: MetricsService

#### Recording Metrics
When an analysis completes, metrics are recorded:

```csharp
// AnalysisService.cs (line 206, 211)
_metricsService?.RecordAnalysisDuration(duration, phpResult.FilesAnalyzed, totalIssues);
_metricsService?.RecordIssueDistribution(issue.Severity, issue.Category);
```

#### Storing Metrics
The MetricsService stores metrics in two places:

**1. In-Memory (Fast, Recent Data)**
```csharp
// MetricsService.cs (line 18-19)
private readonly ConcurrentDictionary<string, long> _issueDistribution = new();
private readonly ConcurrentBag<AnalysisDurationMetric> _durationMetrics = new();
```

**2. Database (Persistent, Historical Data)**
```csharp
// MetricsService.cs (line 60-62)
var runs = await context.AnalysisRuns
    .Where(r => r.CreatedAt >= cutoff && r.Status == "completed")
    .ToListAsync();
```

#### Retrieving Metrics
The `/api/metrics` endpoint provides aggregated statistics:

```csharp
// MetricsController.cs (line 21-26)
[HttpGet]
public async Task<IActionResult> GetMetrics([FromQuery] DateTime? since = null)
{
    var metrics = await _metricsService.GetMetricsAsync(since);
    return Ok(metrics);
}
```

**Example Response**:
```json
{
  "totalRuns": 150,
  "averageDuration": 3.2,
  "totalFilesAnalyzed": 2250,
  "totalIssues": 4500,
  "averageScore": 82.5,
  "issueDistribution": {
    "critical:security": 50,
    "major:complexity": 200,
    "minor:style": 3000
  },
  "periodStart": "2026-01-09T00:00:00Z",
  "periodEnd": "2026-01-10T12:00:00Z"
}
```

**Benefit**: You can see trends, identify bottlenecks, and monitor system health!

---

## 4. Sensitive Data Redaction - Security in Logs

### Why Redact Sensitive Data?
Logs might contain passwords, tokens, or API keys. We need to hide these before logging.

### Implementation

#### Redaction Patterns
```csharp
// AnalysisService.cs (line 1347-1359)
private string RedactSensitiveData(string input)
{
    // Redact tokens (40+ character alphanumeric strings)
    input = Regex.Replace(input, @"\b[a-zA-Z0-9]{40,}\b", "[REDACTED]");
    
    // Redact key-value pairs like "token: abc123"
    input = Regex.Replace(input, @"(token|key|secret|password)\s*[:=]\s*[^\s,}]+", 
        match => $"{match.Groups[1].Value}: [REDACTED]", RegexOptions.IgnoreCase);
    
    return input;
}
```

#### Usage Example
**Before Redaction**:
```
Starting analysis. RepoUrl: https://github.com/user/repo?token=ghp_abc123xyz456...
```

**After Redaction**:
```
Starting analysis. RepoUrl: https://github.com/user/repo?token: [REDACTED]
```

**Benefit**: Logs are safe to share and store without exposing secrets!

---

## 5. Request Logging Middleware - Automatic Request Tracking

### What It Does
Automatically logs every HTTP request with:
- Start time
- Method and path
- Status code
- Duration
- Correlation ID

### Implementation

```csharp
// RequestLoggingMiddleware.cs (line 17-45)
public async Task InvokeAsync(HttpContext context)
{
    var correlationId = context.Items["CorrelationId"]?.ToString() ?? "unknown";
    var startTime = DateTime.UtcNow;
    
    _logger.LogInformation("[{CorrelationId}] Request Started: {Method} {Path}", ...);
    
    try {
        await _next(context);
    }
    finally {
        var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;
        _logger.LogInformation("[{CorrelationId}] Request Completed: {Method} {Path} - Status: {StatusCode} in {Duration}ms", ...);
    }
}
```

**Example Output**:
```
[a3f5b2c1] Request Started: POST /api/analysis/analyze
[a3f5b2c1] Starting branch analysis. UserId: 123, RepoUrl: github.com/user/repo, Branch: main
[a3f5b2c1] Sending 15 files to PHP analysis service
[a3f5b2c1] PHP analysis complete. Issues: 42, Score: 85.5
[a3f5b2c1] Analysis complete. RunId: 456, Score: 85.5, Files: 15, Issues: 42, Duration: 3.2s
[a3f5b2c1] Request Completed: POST /api/analysis/analyze - Status: 200 in 3200ms
```

**Benefit**: You automatically get visibility into all requests without adding logging code everywhere!

---

## 6. Error Handling with Correlation IDs

### How Errors Are Logged
When errors occur, they include the correlation ID:

```csharp
// AnalysisService.cs (line 260)
catch (Exception ex)
{
    _logger.LogError(ex, "[{CorrelationId}] Unexpected error during analysis", correlationId);
    // ... handle error
}
```

**Example Error Log**:
```
[a3f5b2c1] Unexpected error during analysis
System.Exception: GitHub API rate limit exceeded
   at AnalysisService.AnalyzeBranchAsync(...)
```

**Benefit**: You can trace errors back to the original request!

---

## Middleware Pipeline - Order Matters!

The .NET API processes requests through middleware in this order:

```csharp
// Program.cs (typical order)
1. CorrelationIdMiddleware      // Generate/extract correlation ID
2. RequestLoggingMiddleware      // Log request start
3. RateLimitingMiddleware        // Check rate limits
4. AuthenticationMiddleware      // Verify JWT token
5. Your Controllers              // Handle the request
6. RequestLoggingMiddleware      // Log request end (in finally block)
7. GlobalExceptionHandler        // Catch any errors
```

**Why Order Matters**: 
- Correlation ID must be set BEFORE logging
- Logging must happen BEFORE the request is processed
- Error handling must be LAST to catch all errors

---

## Real-World Example: Following a Request

Let's trace a complete request:

### 1. User Clicks "Analyze Branch" in React App
```
React → POST /api/analysis/analyze
Headers: { "Authorization": "Bearer token...", "X-Correlation-Id": "a3f5b2c1" }
```

### 2. .NET API Receives Request
```
[a3f5b2c1] Request Started: POST /api/analysis/analyze
[a3f5b2c1] Starting branch analysis. UserId: 123, RepoUrl: github.com/user/repo, Branch: main
```

### 3. .NET API Calls PHP Service
```
.NET → POST http://localhost:8000/analyze/files
Headers: { "X-Correlation-Id": "a3f5b2c1" }
Body: { "files": [...] }
```

### 4. PHP Service Processes Request
```
[a3f5b2c1] Incoming Request: POST /analyze/files
[PHP processes files...]
```

### 5. PHP Service Returns Results
```
.NET receives response from PHP
[a3f5b2c1] PHP analysis complete. Issues: 42, Score: 85.5
```

### 6. .NET API Records Metrics
```
MetricsService.RecordAnalysisDuration(3.2, 15, 42)
MetricsService.RecordIssueDistribution("critical", "security")
```

### 7. Request Completes
```
[a3f5b2c1] Analysis complete. RunId: 456, Score: 85.5, Files: 15, Issues: 42, Duration: 3.2s
[a3f5b2c1] Request Completed: POST /api/analysis/analyze - Status: 200 in 3200ms
```

**If you search logs for `a3f5b2c1`, you see the ENTIRE journey!**

---

## Benefits Summary

1. **Traceability** - Follow requests across services using correlation IDs
2. **Debugging** - Quickly find what went wrong and where
3. **Performance Monitoring** - See slow requests and bottlenecks
4. **Security** - Sensitive data is automatically redacted
5. **Consistency** - Unified logging format across all services
6. **Insights** - Metrics show trends and system behavior
7. **Reliability** - Input validation prevents resource exhaustion

---

## Testing Observability

### Test Correlation ID Flow
```powershell
# Send request with correlation ID
Invoke-RestMethod -Uri "http://localhost:8000/analyze/files" `
  -Method Post `
  -Headers @{"X-Correlation-Id"="test-abc123"} `
  -Body (@{files=@()} | ConvertTo-Json) `
  -ContentType "application/json"

# Check logs for "test-abc123" to see the full trace
```

### View Metrics
```powershell
# Get metrics (requires authentication)
Invoke-RestMethod -Uri "http://localhost:5116/api/metrics?since=2026-01-09T00:00:00Z" `
  -Headers @{"Authorization"="Bearer YOUR_JWT_TOKEN"}
```

---

## Future Enhancements (From Documentation)

The project documentation suggests these future improvements:

1. **Prometheus Integration** - Export metrics in Prometheus format
2. **Grafana Dashboards** - Visualize metrics and logs
3. **Distributed Tracing** - Use OpenTelemetry for full request traces
4. **Log Aggregation** - Send logs to ELK stack (Elasticsearch, Logstash, Kibana)
5. **Health Checks** - Add `/health` endpoints with dependency status
6. **Alerting** - Configure alerts for high error rates, slow requests

---

## Key Takeaways

1. **Observability = Visibility** - You can see what's happening inside your app
2. **Correlation IDs** - Track requests across multiple services
3. **Structured Logging** - Consistent, searchable log format
4. **Metrics** - Numerical measurements for monitoring
5. **Security** - Always redact sensitive data in logs
6. **Middleware** - Automatic logging without modifying business logic

This implementation gives you powerful tools to understand, debug, and monitor your application!
