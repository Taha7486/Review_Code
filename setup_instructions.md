# Setup Instructions

This document provides step-by-step instructions for setting up and running the **Code Review Automation Tool**.

## 📋 Prerequisites
- **.NET 9.0 SDK** - [Download](https://dotnet.microsoft.com/download)
- **Node.js 18+** - [Download](https://nodejs.org/)
- **MySQL 8.0** - [Download](https://dev.mysql.com/downloads/)
- **PHP 8.2+** - [Download](https://www.php.net/downloads)
- **Composer** - [Download](https://getcomposer.org/)
- **Git** - For cloning the repository
- **Docker Desktop** (optional, for containerization)

---

## � Environment Variables Setup

### Step 1: Create `.env` file

Navigate to `dotnet-api/` and create your environment file:

```bash
cd dotnet-api
cp .env.example .env
```

### Step 2: Edit `.env` with your credentials


## 🗄️ Database Setup

### Apply Migrations

```bash
cd dotnet-api
dotnet ef database drop
dotnet ef database update
```

## 🚀 Running the Application

### Order of Startup (Important!)

1. **MySQL** (must be running first)
2. **PHP Analysis Service**
3. **.NET API**
4. **React Frontend**

### PHP Analysis Service

```bash
cd php-service
composer install 
php -S localhost:8000 -t public
```

✅ Service ready on: `http://localhost:8000`

### .NET API

```bash
cd dotnet-api
dotnet run
```

✅ API ready on: `http://localhost:5116`

### React Frontend

```bash
cd react-app
npm install
npm start
```

✅ Frontend ready on: `http://localhost:3000`
---
kill all servers : 
---
taskkill /F /IM php.exe /IM node.exe /IM dotnet.exe /IM dotnet-api.exe
---
## 🔧 Development Status

### ✅ Phase 5 Complete - MVP Ready

**Working Features:**
- ✅ User authentication (Register/Login) with JWT
- ✅ Branch-based analysis (compare against main/master)
- ✅ GitHub integration via Octokit with **Correlation Tracking**
- ✅ Branch dropdown (auto-fetch from GitHub)
- ✅ Full file content analysis (not just diffs)
- ✅ PHP analysis engine (Complexity, Security, Style)
- ✅ Visual Score Dial (animated, color-coded)
- ✅ Code Inspector (shows ±2 lines of context)
- ✅ File grouping
- ✅ Environment variable security (Root `.env` support)
- ✅ Global Exception Handling
- ✅ Unit Tests Integration (`dotnet-api/tests`)

**Architecture:**
- `.NET API` → Orchestrates workflow
- `PHP Service` → Analyzes code
- `React App` → User interface
- `MySQL` → Stores users & reviews

---

## 🎯 Next Steps (ToDo)

### Immediate Tasks (Next Session)
- [ ] **Run & Verify Tests**:
  - Run `dotnet test` in `dotnet-api/tests`
  - Run `php tests/run_tests.php` in `php-service`
- [ ] **Verify End-to-End Flow**:
  - Check if moving `.env` affected the running services (restart required)
  - Test a full analysis cycle from the UI
- [ ] **Refine Docker Setup**:
  - Create `docker-compose.yml` to orchestrate all 3 services + DB

### Short-term Goals
- [ ] Implement GitHub OAuth (for private repos)
- [ ] Add rate limiting
- [ ] Improve error handling & logging further

### Long-term Goals
- [ ] Webhook integration (auto-analyze on push)
- [ ] Email notifications
- [ ] Review history dashboard
- [ ] Cloud Deployment (Azure/AWS)

---

## 🆘 Troubleshooting

### Port Conflicts

**Check what's using a port:**
```bash
# Windows
netstat -ano | findstr :5116

# macOS/Linux
lsof -i :5116
```

**Kill process:**
```bash
# Windows
taskkill /F /PID <process_id>

# macOS/Linux
kill -9 <process_id>
```

### Database Issues

**Reset database:**
```bash
cd dotnet-api
dotnet ef database drop
dotnet ef database update
```

### Clear Node/Composer Caches

```bash
# React
cd react-app
rm -rf node_modules package-lock.json
npm install

# PHP
cd php-service
rm -rf vendor composer.lock
composer install
```

---

## 📚 Additional Resources

- [.NET Documentation](https://docs.microsoft.com/dotnet/)
- [React Documentation](https://react.dev/)
- [Slim Framework](https://www.slimframework.com/)
- [Octokit.NET](https://octokitnet.readthedocs.io/)

---

**Last Updated:** January 8, 2026
**Project Phase:** 5 - MVP Complete
**Next Milestone:** Testing & Dockerization
