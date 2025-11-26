# Code Review Automation Tool

## 🎯 Project Overview

An automated code review platform that analyzes pull requests for code quality, security vulnerabilities, and best practices. When developers create PRs on GitHub/GitLab, the system automatically reviews the code and posts detailed feedback.

**Status:** Phase 2 - Database & Authentication Setup

---

## 🏗️ Architecture

### System Flow
```
GitHub PR Created
    ↓
Webhook → .NET API
    ↓
Fetch PR Code (GitHub API)
    ↓
Send to PHP Analysis Service
    ↓
Receive Analysis Results
    ↓
Store in PostgreSQL
    ↓
Post Comment on GitHub PR
    ↓
Display in React Dashboard
```

### Tech Stack
- **Frontend:** React (Port 3000)
- **Backend API:** .NET 8.0 Web API (Port 5116)
- **Analysis Engine:** PHP (Port 8000)
- **Database:** MySQL 8.0
- **Cache/Queue:** Redis (planned)
- **Containerization:** Docker + Docker Compose
- **Orchestration:** Kubernetes (planned)
- **CI/CD:** GitHub Actions (planned)

---

## 📁 Project Structure
```
project-root/
├── dotnet-api/              # .NET Web API Service
│   ├── Controllers/         # API endpoints
│   ├── Models/             # Database entity classes (EF Core)
│   ├── Contracts/          # API request/response objects
│   ├── Data/               # DbContext & migrations
│   ├── Services/           # Business logic
│   ├── Middleware/         # Custom middleware (JWT, etc.)
│   ├── Helpers/            # Utility classes
│   └── Program.cs          # Entry point
│
├── php-service/            # PHP Analysis Engine
│   └── (PHP analysis logic)
│
├── react-frontend/         # React Dashboard
│   └── (React components)
│
├── DB_Schema.sql           # database schema
├── docker-compose.yml      
└── README.md              
```

---

## 🗄️ Database Schema

## 🚀 Current Development Phase

### Phase 2: Database & Authentication

**Goals:**
- ✅ Design database schema
- ⏳ Set up Entity Framework Core in .NET
- ⏳ Create database migrations
- ⏳ Build user registration endpoint
- ⏳ Build login endpoint with JWT
- ⏳ Add authentication middleware
- ⏳ Create login/register UI in React

**Next Steps:**
1. Install EF Core packages in .NET
2. Create Model classes for all 7 tables
3. Create ApplicationDbContext
4. Generate and apply migrations
5. Build AuthController with register/login endpoints

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