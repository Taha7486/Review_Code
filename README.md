# Code Review Automation Tool

## 🎯 Project Overview

An automated code review platform that analyzes Git branches for code quality, security vulnerabilities, and best practices. Developers can analyze their feature branches **before** creating pull requests, getting instant feedback on issues.

**Status:** Phase 5 Complete - Full MVP with Branch Analysis

---

## 🏗️ Architecture

### System Flow
```
Developer selects GitHub Repo + Branch
    ↓
Frontend fetches available branches (GitHub API)
    ↓
User selects branch → Analyze
    ↓
.NET API compares branch vs main (Octokit)
    ↓
Fetches full file content from branch
    ↓
Sends to PHP Analysis Service (HTTP)
    ↓
PHP analyzes: Complexity, Security, Style
    ↓
Returns structured JSON report
    ↓
Frontend displays: Score Dial + Code Inspector + Grouped Issues
```

### Tech Stack
- **Frontend:** React (Port 3000)
- **Backend API:** .NET 9.0 Web API (Port 5116)
- **Analysis Engine:** PHP 8+ (Slim Framework) (Port 8000)
- **Database:** MySQL 8.0
- **Containerization:** Docker + Docker Compose

---

## 📁 Project Structure
```
project-root/
├── dotnet-api/              # .NET Web API Orhcestrator
│   ├── Controllers/         # Auth, Review
│   ├── Models/             # EF Core Models & DTOs
│   ├── Services/           # ReviewService (The Brain)
│   ├── Data/               # DbContext & migrations
│   └── Program.cs          # DI & Configuration
│
├── php-service/            # PHP Analysis Engine
│   ├── app/Controllers     # AnalysisController
│   ├── app/Services        # Complexity, Security, Style logic
│   └── public/index.php    # Entry point
│
├── react-frontend/         # React Dashboard
│
├── DB_Schema.sql           # Reference schema
├── setup_instructions.md   # Setup Guide
└── README.md              
```

---

## 🚀 Current Development Phase

### Phase 5: Core Analysis Logic (MVP)

**Goals:**
- ✅ Database & Auth Setup (Phase 2)
- ✅ Fetch Code from GitHub (Octokit)
- ✅ Configure Service-to-Service communication (HTTP)
- ✅ Implement PHP Analysis logic (Slim)
- ✅ End-to-End flow (.NET -> PHP -> .NET)

**Next Steps (Phase 6):**
1. Integrate "Analyze" button in React.
2. Link GitHub OAuth for private repos.
3. Dockerize and Compose all services.

---

## 🔧 Development Setup

### Prerequisites
- .NET 9.0 SDK
- Node.js 18+
- PHP 8.2+
- MySQL 8.0
- Composer (for PHP dependencies)
- Docker Desktop (optional, for containerization)

### 🔐 Environment Variables Setup

**IMPORTANT:** Never commit secrets to version control!

1. **Create `.env` file** in `dotnet-api/`:
   ```bash
   cd dotnet-api
   cp .env.example .env
   ```

2. **Edit `.env`** with your credentials:
   ```env
   # Database
   DB_HOST=localhost
   DB_PORT=3306
   DB_NAME=code_review_tool
   DB_USER=root
   DB_PASSWORD=your_mysql_password
   
   # JWT (Must be at least 32 characters!)
   JWT_SECRET_KEY=your_super_secret_jwt_key_here_minimum_32_chars
   
   # Services
   PHP_ANALYSIS_API_URL=http://localhost:8000/api/analyze/files
   
   # GitHub (Optional - for private repos)
   GITHUB_PAT=your_github_personal_access_token
   ```

3. **Security Notes:**
   - `.env` is automatically gitignored
   - In production, use environment variables or secret managers (Azure Key Vault, AWS Secrets Manager)
   - The `.env.example` file is committed as a template

### Running Locally

**1. Database Setup**
```bash
# Create database
mysql -u root -p
CREATE DATABASE code_review_tool;

# Run migrations
cd dotnet-api
dotnet ef database update
```

**2. .NET API**
```bash
cd dotnet-api
dotnet restore
dotnet run
# Runs on http://localhost:5116
```

**3. PHP Analysis Service**
```bash
cd php-service
composer install  # First time only
php -S localhost:8000 -t public
# CRITICAL: Must use -t public for routing!
# Runs on http://localhost:8000
```

**4. React Frontend**
```bash
cd react-app
npm install  # First time only
npm start
# Runs on http://localhost:3000
```

### 🎯 Quick Test

1. Navigate to `http://localhost:3000`
2. Login with your account
3. Enter a GitHub repo URL: `https://github.com/owner/repo`
4. Click the **Refresh icon** 🔄 to load branches
5. Select a branch from the dropdown
6. Click **Analyze Branch**
7. View results with:
   - **Visual Score Dial**
   - **Issues grouped by file**
   - **Code Inspector** (shows exact problematic lines)

---

## 📝 Notes

### Security
- **Environment variables** used for all secrets (DB, JWT, API keys)
- Secrets never committed to Git (`.env` is gitignored)
- Use `.env.example` as a template
- Production: Use cloud secret managers (Azure Key Vault, AWS Secrets Manager)

### Architecture Decisions
- **Branch-based analysis** (not PR-based) - analyze before creating PRs
- **Full file content** sent to PHP analyzer (not just diffs/patches)
- Line numbers accurate to actual file content
- All timestamps use UTC
- JSON columns for flexible schema (metrics, configurations)
- Cascade deletes enabled for data integrity

### Current Features ✨
- ✅ **Branch Dropdown**: Auto-fetch branches from GitHub
- ✅ **Visual Score Dial**: Animated circular gauge (color-coded)
- ✅ **Code Inspector**: Shows exact problematic lines with context (±2 lines)
- ✅ **File Grouping**: Issues organized by file
- ✅ **Smart Analysis**: Complexity, Security, Style checking
- ✅ **Trailing Whitespace**: Disabled per user request

---

## 🚀 Current Development Status

### ✅ Completed (Phase 5 - MVP)
- Database & Authentication (Phase 2)
- GitHub Integration via Octokit
- Service-to-service communication (.NET ↔ PHP)
- PHP Analysis Engine (Complexity, Security, Style)
- Branch comparison (vs default branch)
- React Dashboard with real-time analysis
- Environment variable security
- Visual code inspector with syntax highlighting

### 🎯 Next Steps
- Add basic tests (.NET + PHP)
- Implement GitHub OAuth for private repos
- Dockerize all services (docker-compose)
- CI/CD pipeline
- Deploy to cloud (Azure/AWS)

---

## 🎯 Future Phases

- Phase 3: GitHub OAuth & Webhooks
- Phase 4: Code Fetching via GitHub API
- Phase 5-6: PHP Analysis Engine
- Phase 7: End-to-End Integration
- Phase 8: React Dashboard UI
- Phase 9: CI/CD Pipeline
- Phase 10: Kubernetes Deployment
- Phase 11: Monitoring & Observability
- Phase 12: Documentation & Polish