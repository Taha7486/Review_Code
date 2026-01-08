# Code Review Automation Tool

## 🎯 Project Overview

An automated code review platform that analyzes pull requests for code quality, security vulnerabilities, and best practices. When developers create PRs on GitHub/GitLab, the system automatically reviews the code and posts detailed feedback.

**Status:** Phase 5 - Backend Core Logic (MVP)

---

## 🏗️ Architecture

### System Flow
```
GitHub PR Created / Selected
    ↓
Request Analysis (.NET API)
    ↓
Fetch PR Code (Octokit)
    ↓
Send to PHP Analysis Service (HTTP)
    ↓
Receive Analysis Report (JSON)
    ↓
Return to User (Dashboard)
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
- .NET 8.0 SDK
- Node.js 18+
- PHP 8.1+
- MySQL 8.0
- Docker Desktop (for later phases)

### Running Locally

**1. .NET API**
```bash
cd dotnet-api
dotnet restore
dotnet run
# Runs on http://localhost:5116
```

**2. React Frontend**
```bash
cd react-frontend
npm install
npm start
# Runs on http://localhost:3000
```

**3. PHP Service**
```bash
cd php-service
php -S localhost:8000
# Runs on http://localhost:8000
```

---

## 📝 Notes

- MySQL connection string configured in `dotnet-api/appsettings.json`
- All timestamps use UTC
- JSON columns used for flexible schema (metrics, configurations)
- Cascade deletes enabled for data integrity
- GitHub IDs stored for API integration

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