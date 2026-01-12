# Production Deployment Cleanup & Readiness Plan

This document outlines all the necessary changes, cleanups, and architectural adjustments required to transition the **CodeReviewAI** project from a local development environment to a production-ready cloud environment.

---

## ✅ Phase 1: Completed Tasks (Recently Addressed)

### 🧹 Application Cleanup & Standardization
- [x] **Unified .env Management**: Created `.env.production` at the root to orchestrate all services.
- [x] **JWT Security**: Enforced mandatory `JWT_SECRET_KEY` in production; server will fail to start if missing or insecure.
- [x] **Internal Service Security**: Implemented a Bearer token shared secret between the .NET API and PHP service.
- [x] **Production Error Handling**: Disabled `display_errors` in the PHP service and hidden stack traces in the .NET API for production mode.
- [x] **CORS Readiness**: Configured `AllowedOrigins` to be environment-driven for secure production domains.
- [x] **Sanitize Production Logging**: Downgraded intermediate logs to `LogDebug`. High-level milestones remain `LogInformation`.
- [x] **Frontend Console Cleanup**: All `console.log` calls are now development-only.
- [x] **Caching & Data Sync Fixes**: Resolved score-syncing and stale data issues.

---

## 🏗️ 2. Infrastructure & Environment Strategy

### 🟢 Cross-Platform Initialization
Current code often uses `localhost` as a fallback. In production, we must ensure **no hardcoded fallbacks** leak into the execution.

- **Checklist:**
  - [x] **Unified .env Management**: Create a master `.env.production` file at the root to orchestrate all three services.
  - [ ] **Dockerization**: 
    - Create a multi-stage `Dockerfile` (Build & Runtime) for the `.NET API`.
    - Create a production `Dockerfile` for the `PHP Service` (using PHP8-FPM + Nginx or Apache).
    - Create a production `Dockerfile` for the `React App` (Build step + Nginx for serving static files).
  - [ ] **Docker Compose**: Define a `docker-compose.prod.yml` to link the API, PHP Service, and a MySQL/MariaDB container.

---

## 🖥️ 3. .NET API (.NET 9) Readiness

### ⚙️ Configuration Readiness (`appsettings.json`)
- [ ] **JWT Key**: Remove the default fallback `super_secret_key...` in `Program.cs`. Production must **fail** if `JWT_SECRET_KEY` is not provided.
- [ ] **CORS**: Set `AllowedOrigins` strictly to the production domain (e.g., `https://app.codereviewai.com`).
- [ ] **Connection Strings**: Ensure standard MySQL port (3306) and credentials are provided via DB secrets, not hardcoded.

### 🛡️ Security
- [x] **Secret Redaction**: Request logging now automatically redacts tokens and passwords from logged strings.
- [x] **JWT Enforcement**: Production mode now requires a unique, secure JWT key.
- [ ] **GitHub Token Privacy**: Ensure `GITHUB_PAT` is treated as a sensitive secret (e.g., AWS Secrets Manager or GitHub Actions Secrets).

---

## ⚛️ 4. React Frontend Final Polish

### ⚙️ Environment Variables
- [x] **API Endpoint**: `REACT_APP_API_URL` is environment-aware and ready for production.
- [ ] **Build Optimization**: Confirm `npm run build` is used for deployment with optimized assets.

---

## 🐘 5. PHP Analysis Service

### 🛡️ Security & Scalability
- [x] **Display Errors**: Production mode disables error output.
- [x] **Request Validation**: Implemented internal Bearer token validation.

---

## 📋 6. Immediate Manual Steps

1. **Verify EF Core Migrations**: Ensure you have a strategy to run `dotnet ef database update` in the CI/CD pipeline.
2. **GitHub OAuth**: If switching to production, register a production GitHub App to handle OAuth correctly instead of relying on a Personal Access Token (`PAT`) for everything.
3. **SSL/TLS**: Secure all inter-service communication (React -> API and API -> PHP).

---
*Last Updated: 2026-01-11*

