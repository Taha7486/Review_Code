# Test Scenarios for Code Review Tool

## Pre-Deployment Testing Checklist

### 1. Basic Functionality Tests

#### Test 1.1: User Registration & Authentication
```bash
# Register new user
curl -X POST http://localhost:5116/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{"username":"testuser","email":"test@example.com","password":"Test123!"}'

# Login
curl -X POST http://localhost:5116/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"test@example.com","password":"Test123!"}'
```

#### Test 1.2: Public Repository Analysis
```bash
# Analyze a small public PHP repo
curl -X POST http://localhost:5116/api/analysis/start \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "repoUrl": "https://github.com/slimphp/Slim",
    "branchName": "4.x"
  }'
```

#### Test 1.3: Branch Listing
```bash
# Get branches for a repo
curl -X GET "http://localhost:5116/api/analysis/branches?repoUrl=https://github.com/slimphp/Slim" \
  -H "Authorization: Bearer YOUR_JWT_TOKEN"
```

### 2. Load & Performance Tests

#### Test 2.1: Small Repository (5-10 files)
- **Repo**: https://github.com/vlucas/phpdotenv (small utility library)
- **Expected**: < 10 seconds
- **Metrics**: Response time, memory usage, CPU usage

#### Test 2.2: Medium Repository (20-30 files)
- **Repo**: https://github.com/guzzle/guzzle (HTTP client)
- **Expected**: < 30 seconds
- **Metrics**: Database connections, PHP memory

#### Test 2.3: Large Repository (50+ files)
- **Repo**: https://github.com/laravel/framework (will hit 50 file limit)
- **Expected**: < 60 seconds
- **Metrics**: File filtering effectiveness, timeout handling

#### Test 2.4: Concurrent Users
```bash
# Run 5 simultaneous analyses
for i in {1..5}; do
  curl -X POST http://localhost:5116/api/analysis/start \
    -H "Authorization: Bearer YOUR_JWT_TOKEN" \
    -H "Content-Type: application/json" \
    -d '{"repoUrl":"https://github.com/slimphp/Slim","branchName":"4.x"}' &
done
wait
```

### 3. Error Handling Tests

#### Test 3.1: Invalid Repository
```bash
curl -X POST http://localhost:5116/api/analysis/start \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"repoUrl":"https://github.com/nonexistent/repo","branchName":"main"}'
# Expected: 404 or appropriate error message
```

#### Test 3.2: Invalid Branch
```bash
curl -X POST http://localhost:5116/api/analysis/start \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"repoUrl":"https://github.com/slimphp/Slim","branchName":"nonexistent-branch"}'
# Expected: Branch not found error
```

#### Test 3.3: Rate Limit Test (without token)
```bash
# Make 60+ requests in quick succession
for i in {1..65}; do
  curl -X GET "http://localhost:5116/api/analysis/branches?repoUrl=https://github.com/slimphp/Slim"
done
# Expected: Rate limit error after ~60 requests
```

#### Test 3.4: PHP Service Down
```bash
# Stop PHP service
taskkill /F /IM php.exe

# Try analysis
curl -X POST http://localhost:5116/api/analysis/start \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"repoUrl":"https://github.com/slimphp/Slim","branchName":"4.x"}'
# Expected: Service unavailable error

# Restart PHP service
cd php-service
php -S localhost:8000 -t public
```

### 4. Security Tests

#### Test 4.1: SQL Injection Attempts
```bash
# Try SQL injection in repo URL
curl -X POST http://localhost:5116/api/analysis/start \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"repoUrl":"https://github.com/test/repo\"; DROP TABLE users; --","branchName":"main"}'
```

#### Test 4.2: XSS Attempts
```bash
curl -X POST http://localhost:5116/api/analysis/start \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"repoUrl":"<script>alert(\"XSS\")</script>","branchName":"main"}'
```

#### Test 4.3: JWT Token Manipulation
```bash
# Try with expired token
# Try with modified token
# Try with no token
curl -X POST http://localhost:5116/api/analysis/start \
  -H "Content-Type: application/json" \
  -d '{"repoUrl":"https://github.com/slimphp/Slim","branchName":"4.x"}'
# Expected: 401 Unauthorized
```

#### Test 4.4: File Size Bomb
```bash
# Create a test endpoint that sends a huge file
# Expected: 400 Bad Request (file size exceeded)
```

### 5. Data Integrity Tests

#### Test 5.1: Line Number Accuracy
- Analyze a repo with known issues
- Verify reported line numbers match actual code
- Check code inspector shows correct context

#### Test 5.2: Score Consistency
- Analyze same branch multiple times
- Verify scores are identical
- Check idempotency (no duplicate runs)

#### Test 5.3: Special Characters
```bash
# Test with repo containing unicode, emojis, special chars
curl -X POST http://localhost:5116/api/analysis/start \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"repoUrl":"https://github.com/username/repo-with-émojis","branchName":"main"}'
```

### 6. Edge Cases

#### Test 6.1: Empty Branch
- Create a branch with no changes from main
- Expected: "No files found for analysis" message

#### Test 6.2: Binary Files Only
- Branch with only images/PDFs
- Expected: Filtered out, no analysis

#### Test 6.3: Very Long File Names
- Test with files having 200+ character paths
- Expected: Handled gracefully

#### Test 6.4: Deeply Nested Directories
- Test with files 10+ levels deep
- Expected: Analyzed correctly

### 7. Frontend Tests

#### Test 7.1: UI Responsiveness
- Test on mobile, tablet, desktop
- Check score dial animation
- Verify issue grouping

#### Test 7.2: Real-time Updates
- Start analysis
- Poll for status updates
- Verify UI updates correctly

#### Test 7.3: Error Display
- Trigger various errors
- Verify user-friendly messages
- Check error recovery

### 8. Database Tests

#### Test 8.1: Connection Pool
- Run 20+ concurrent analyses
- Monitor database connections
- Check for connection leaks

#### Test 8.2: Large Result Sets
- Analyze repo with 1000+ issues
- Verify pagination works
- Check query performance

#### Test 8.3: Data Cleanup
- Delete user
- Verify cascade deletes work
- Check orphaned records

## Performance Benchmarks

### Expected Metrics:
- **Small repo (10 files)**: < 10s
- **Medium repo (30 files)**: < 30s
- **Large repo (50 files)**: < 60s
- **API response time**: < 200ms (non-analysis endpoints)
- **Database queries**: < 100ms
- **Memory usage**: < 512MB per analysis
- **Concurrent users**: Support 10+ simultaneous analyses

## Monitoring Checklist

### Health Checks:
- [ ] API health endpoint: http://localhost:5116/health
- [ ] PHP health endpoint: http://localhost:8000/health
- [ ] Database connectivity
- [ ] GitHub API connectivity

### Logs to Monitor:
- [ ] Application logs (correlation IDs)
- [ ] Error logs (exceptions, failures)
- [ ] Performance logs (slow queries)
- [ ] Security logs (failed auth, rate limits)

### Metrics to Track:
- [ ] Request rate (requests/second)
- [ ] Error rate (errors/total requests)
- [ ] Response time (p50, p95, p99)
- [ ] Analysis duration
- [ ] Database query time
- [ ] GitHub API rate limit remaining

## Post-Deployment Validation

### Smoke Tests:
1. Register new user
2. Login
3. Analyze public repo
4. View results
5. Check historical runs

### Stress Tests:
1. 100 concurrent users
2. 1000 analyses in 1 hour
3. Database with 10,000+ records
4. Network latency simulation

## Known Limitations to Document

1. **Language Support**: PHP only (JavaScript/CSS accepted but limited analysis)
2. **File Limits**: Max 50 files, 500KB per file
3. **Analysis Type**: Pattern-based (not AST), may have false positives
4. **GitHub**: No OAuth yet, requires PAT for private repos
5. **Scalability**: Single-instance PHP service (no horizontal scaling yet)
6. **Real-time**: No webhooks, manual trigger only
