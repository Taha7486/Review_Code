# 🔍 Code Review Automation Platform

![License](https://img.shields.io/badge/license-MIT-blue.svg) 
![Build Status](https://img.shields.io/badge/build-passing-brightgreen.svg)
![Docker](https://img.shields.io/badge/docker-%230db7ed.svg?style=flat&logo=docker&logoColor=white)
![.NET](https://img.shields.io/badge/.NET-512BD4?style=flat&logo=dotnet&logoColor=white)
![React](https://img.shields.io/badge/-React-61DAFB?style=flat&logo=react&logoColor=black)

A production-ready **Microservices DevOps Platform** that automates code quality analysis. It leverages a modern microservices architecture to analyze GitHub branches for complexity, security, and style issues before they merge.

---

## 📸 Dashboard & Metrics

<img width="1366" height="720" alt="image" src="https://github.com/user-attachments/assets/4a09b8fc-b0fd-4ba3-bf79-29c5f17a1440" />


> **Live Observability:** Real-time visibility into system performance using Prometheus & Grafana.

---


### 🛠️ Tech Stack

| Service | Technology | Port | Description |
| :--- | :--- | :--- | :--- |
| **Frontend** | React 18, Tailwind | `3000` | Interactive Dashboard, Real-time status |
| **Backend** | .NET 9.0 Web API | `5116` | Orchestration, Auth, GitHub Integration |
| **Analyzer** | PHP 8.2 (Slim) | `8000` | Static Code Analysis Engine |
| **Database** | MySQL 8.0 | `3306` | Relational Data Store (Users, Reports) |
| **Monitoring** | Prometheus | `9090` | Metric scraping and storage |
| **Visualization** | Grafana | `3001` | Operational Dashboards |

---
## 🏗️ System Architecture
```mermaid
graph TB
    subgraph "Client Layer"
        A[👤 User Browser]
    end
    
    subgraph "Frontend Layer"
        B[⚛️ React Dashboard<br/>Port 3000]
    end
    
    subgraph "Backend Services"
        C[🔷 .NET API<br/>Orchestrator<br/>Port 5116]
        D[🐘 PHP Analyzer<br/>Code Analysis<br/>Port 8000]
    end
    
    subgraph "Data Layer"
        E[(🗄️ MySQL<br/>Port 3306)]
    end
    
    subgraph "Observability Stack"
        F[📊 Prometheus<br/>Metrics<br/>Port 9090]
        G[📈 Grafana<br/>Dashboards<br/>Port 3001]
    end
    
    subgraph "External"
        H[🔗 GitHub API]
    end

    A -->|HTTP Requests| B
    B -->|REST API| C
    C -->|Fetch Code| H
    C -->|Send Analysis Job| D
    C -->|Store Results| E
    D -->|Read/Write| E
    C -.->|Expose Metrics| F
    D -.->|Expose Metrics| F
    F -->|Scrape| G
    
    style A fill:#e1f5ff
    style B fill:#61dafb,color:#000
    style C fill:#512bd4,color:#fff
    style D fill:#777bb4,color:#fff
    style E fill:#00758f,color:#fff
    style F fill:#e6522c,color:#fff
    style G fill:#f46800,color:#fff
    style H fill:#333,color:#fff
```

## ✨ Key Features

*   **⚡ Automated Branch Analysis:** Fetches and analyzes any GitHub branch in seconds.
*   **📊 Deep Metrics:** Calculates **Cyclomatic Complexity**, **Maintainability Index**, and **Lines of Code (LOC)**.
*   **🛡️ Security Scanning:** Detects common vulnerabilities (e.g., `eval()`, hardcoded secrets).
*   **📈 Full Observability:** Professional Grafana dashboards tracking `requests_per_second`, `analysis_duration`, and `system_health`.
*   **🐳 Fully Containerized:** One command (`docker-compose up`) prevents "it works on my machine" issues.
*   **🔄 CI/CD Pipeline:** Automated testing and build pipeline using GitHub Actions.

---

## 🚀 Quick Start (Docker)

The easiest way to run the platform is using Docker Compose.

### Prerequisites
*   Docker & Docker Compose installed.
*   A GitHub Personal Access Token (for fetching private repos).

### Steps

1.  **Clone the repository:**
    ```bash
    git clone https://github.com/Taha7486/Review_Code.git
    cd Review_Code
    ```

2.  **Configure Environment:**
    Copy the example env file and add your credentials.
    ```bash
    cp .env.example .env
    # Edit .env and add your GITHUB_PAT and JWT_SECRET
    ```

3.  **Run the Stack:**
    ```bash
    docker-compose up -d --build
    ```

4.  **Access the App:**
    *   **Frontend Check:** [http://localhost:3000](http://localhost:3000)
    *   **Grafana Dashboard:** [http://localhost:3001](http://localhost:3001) (Login: `admin`/`admin`)
    *   **Prometheus:** [http://localhost:9090](http://localhost:9090)

---

## 🧪 Testing

We employ a test-driven approach with a comprehensive suite of automated tests.

```bash
# Run Backend Tests
cd dotnet-api
dotnet test

# Run Frontend Tests
cd react-app
npm test
```

---

## 📂 Project Structure

```bash
├── dotnet-api/         # Main Orchestrator (C#)
├── php-service/        # Analysis Logic (PHP)
├── react-app/          # User Interface (JS)
├── infrastructure/     # Docker & Prometheus Configs
└── .github/workflows/  # CI/CD Pipelines
```
