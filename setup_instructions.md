# Setup Instructions

This document tracks the setup and development process for the **Code Review Automation Tool**. It will be updated as the project evolves.

## 📋 Prerequisites
- **.NET 9.0 SDK** (for the API)
- **Node.js 18+** (for the Frontend)
- **MySQL 8.0** (Database)
- **PHP 8.1+** (for the Analysis Service)
- **Docker Desktop** (optional, for later stages)

---

## 🗄️ Database Setup (MySQL)

### Configure Connection String
1. Open `dotnet-api/appsettings.json`.
2. Locate the `ConnectionStrings` section.
3. Update the `User` and `Password` fields to match your local MySQL credentials.
   ```

### Database Migrations
**Apply Migrations to Database:**
(Run this after configuring your credentials to create the tables)
```bash
cd dotnet-api
dotnet ef database update
```

---

## 🚀 Running the Application

### 1. .NET Back-end API
Runs on `http://localhost:5116`.
```bash
cd dotnet-api
dotnet restore
dotnet run
```
- **Swagger UI**: Accessible at `http://localhost:5116/swagger` (if enabled) or via standard endpoints.

### 2. React Front-end
Runs on `http://localhost:3000`.
```bash
cd react-app
npm install  # First time only
npm start
```

### 3. PHP Analysis Service
Runs on `http://localhost:8000`.
```bash
cd php-service
php -S localhost:8000
```

---

## 🛠️ Current Development Status
- **Phase**: Phase 5 (Core Analysis Logic - MVP)
- **Database**: Migrations generated & applied.
- **Auth**: JWT Authentication configured (Register/Login).
- **Core API**: 
  - `POST /api/review/analyze` is active.
  - Fetches from GitHub matches Octokit.
  - Connects to PHP Service.
- **Microservices**:
  - .NET API (Orchestrator) ✅
  - PHP Service (Analysis Engine) ✅

## ⏭️ Tomorrow's Plan (Checklist)
- [ ] **React UI**: Add "Analyze PR" button to the dashboard.
- [ ] **Integration Test**: Run the full flow (Browser -> .NET -> PHP) and see the results on screen.
- [ ] **Docker**: Compose all 3 services together.
