# DevOps Showcase Suggestions

## Overview
This document outlines improvements to showcase your DevOps skills for deployment and containerization.

---

## 1. Docker & Containerization

### Priority: HIGH

#### Create Multi-Stage Dockerfiles

**dotnet-api/Dockerfile**
```dockerfile
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["dotnet-api.csproj", "./"]
RUN dotnet restore
COPY . .
RUN dotnet build -c Release -o /app/build
RUN dotnet publish -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 5116
ENTRYPOINT ["dotnet", "dotnet-api.dll"]
```

**php-service/Dockerfile** (already exists, verify optimization)
```dockerfile
FROM php:8.2-fpm-alpine
WORKDIR /var/www
RUN apk add --no-cache composer
COPY composer.json composer.lock ./
RUN composer install --no-dev --optimize-autoloader
COPY . .
EXPOSE 8000
CMD ["php", "-S", "0.0.0.0:8000", "-t", "public"]
```

**react-app/Dockerfile**
```dockerfile
# Build stage
FROM node:18-alpine AS build
WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY . .
RUN npm run build

# Production stage
FROM nginx:alpine
COPY --from=build /app/build /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
```

#### Docker Compose (Production-Ready)
```yaml
version: '3.8'

services:
  mysql:
    image: mysql:8.0
    container_name: code-review-db
    environment:
      MYSQL_ROOT_PASSWORD: ${DB_PASSWORD}
      MYSQL_DATABASE: code_review_tool
    volumes:
      - mysql_data:/var/lib/mysql
      - ./DB_Schema.sql:/docker-entrypoint-initdb.d/schema.sql
    ports:
      - "3306:3306"
    healthcheck:
      test: ["CMD", "mysqladmin", "ping", "-h", "localhost"]
      interval: 10s
      timeout: 5s
      retries: 5
    networks:
      - code-review-network

  php-service:
    build:
      context: ./php-service
      dockerfile: Dockerfile
    container_name: code-review-php
    depends_on:
      mysql:
        condition: service_healthy
    environment:
      - PHP_MEMORY_LIMIT=512M
    ports:
      - "8000:8000"
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8000/health"]
      interval: 30s
      timeout: 10s
      retries: 3
    networks:
      - code-review-network
    restart: unless-stopped

  dotnet-api:
    build:
      context: ./dotnet-api
      dockerfile: Dockerfile
    container_name: code-review-api
    depends_on:
      mysql:
        condition: service_healthy
      php-service:
        condition: service_healthy
    environment:
      - DB_HOST=mysql
      - DB_PORT=3306
      - DB_NAME=code_review_tool
      - DB_USER=root
      - DB_PASSWORD=${DB_PASSWORD}
      - JWT_SECRET_KEY=${JWT_SECRET_KEY}
      - PHP_ANALYSIS_API_URL=http://php-service:8000/api/analyze/files
      - GITHUB_PAT=${GITHUB_PAT}
    ports:
      - "5116:5116"
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:5116/health"]
      interval: 30s
      timeout: 10s
      retries: 3
    networks:
      - code-review-network
    restart: unless-stopped

  react-app:
    build:
      context: ./react-app
      dockerfile: Dockerfile
    container_name: code-review-frontend
    depends_on:
      - dotnet-api
    environment:
      - REACT_APP_API_URL=http://localhost:5116
    ports:
      - "3000:80"
    networks:
      - code-review-network
    restart: unless-stopped

volumes:
  mysql_data:
    driver: local

networks:
  code-review-network:
    driver: bridge
```

---

## 2. Kubernetes Deployment

### Priority: MEDIUM-HIGH

#### Create K8s Manifests

**k8s/namespace.yaml**
```yaml
apiVersion: v1
kind: Namespace
metadata:
  name: code-review
```

**k8s/mysql-deployment.yaml**
```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: mysql
  namespace: code-review
spec:
  replicas: 1
  selector:
    matchLabels:
      app: mysql
  template:
    metadata:
      labels:
        app: mysql
    spec:
      containers:
      - name: mysql
        image: mysql:8.0
        env:
        - name: MYSQL_ROOT_PASSWORD
          valueFrom:
            secretKeyRef:
              name: mysql-secret
              key: password
        - name: MYSQL_DATABASE
          value: code_review_tool
        ports:
        - containerPort: 3306
        volumeMounts:
        - name: mysql-storage
          mountPath: /var/lib/mysql
      volumes:
      - name: mysql-storage
        persistentVolumeClaim:
          claimName: mysql-pvc
---
apiVersion: v1
kind: Service
metadata:
  name: mysql
  namespace: code-review
spec:
  selector:
    app: mysql
  ports:
  - port: 3306
    targetPort: 3306
```

**k8s/api-deployment.yaml**
```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: dotnet-api
  namespace: code-review
spec:
  replicas: 3
  selector:
    matchLabels:
      app: dotnet-api
  template:
    metadata:
      labels:
        app: dotnet-api
    spec:
      containers:
      - name: api
        image: your-registry/code-review-api:latest
        env:
        - name: DB_HOST
          value: mysql
        - name: JWT_SECRET_KEY
          valueFrom:
            secretKeyRef:
              name: api-secrets
              key: jwt-key
        ports:
        - containerPort: 5116
        livenessProbe:
          httpGet:
            path: /health
            port: 5116
          initialDelaySeconds: 30
          periodSeconds: 10
        readinessProbe:
          httpGet:
            path: /health
            port: 5116
          initialDelaySeconds: 10
          periodSeconds: 5
        resources:
          requests:
            memory: "256Mi"
            cpu: "250m"
          limits:
            memory: "512Mi"
            cpu: "500m"
---
apiVersion: v1
kind: Service
metadata:
  name: dotnet-api
  namespace: code-review
spec:
  selector:
    app: dotnet-api
  ports:
  - port: 5116
    targetPort: 5116
  type: LoadBalancer
```

**k8s/ingress.yaml**
```yaml
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: code-review-ingress
  namespace: code-review
  annotations:
    cert-manager.io/cluster-issuer: letsencrypt-prod
    nginx.ingress.kubernetes.io/ssl-redirect: "true"
spec:
  ingressClassName: nginx
  tls:
  - hosts:
    - api.code-review.example.com
    - app.code-review.example.com
    secretName: code-review-tls
  rules:
  - host: api.code-review.example.com
    http:
      paths:
      - path: /
        pathType: Prefix
        backend:
          service:
            name: dotnet-api
            port:
              number: 5116
  - host: app.code-review.example.com
    http:
      paths:
      - path: /
        pathType: Prefix
        backend:
          service:
            name: react-app
            port:
              number: 80
```

---

## 3. CI/CD Pipeline

### Priority: HIGH

#### GitHub Actions Workflow

**.github/workflows/ci-cd.yml**
```yaml
name: CI/CD Pipeline

on:
  push:
    branches: [ main, develop ]
  pull_request:
    branches: [ main ]

env:
  REGISTRY: ghcr.io
  IMAGE_NAME: ${{ github.repository }}

jobs:
  test-dotnet:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '9.0.x'
      
      - name: Restore dependencies
        run: dotnet restore
        working-directory: ./dotnet-api
      
      - name: Build
        run: dotnet build --no-restore
        working-directory: ./dotnet-api
      
      - name: Test
        run: dotnet test --no-build --verbosity normal
        working-directory: ./dotnet-api/tests

  test-php:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      
      - name: Setup PHP
        uses: shivammathur/setup-php@v2
        with:
          php-version: '8.2'
      
      - name: Install dependencies
        run: composer install
        working-directory: ./php-service
      
      - name: Run tests
        run: php tests/run_tests.php
        working-directory: ./php-service

  test-react:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      
      - name: Setup Node
        uses: actions/setup-node@v3
        with:
          node-version: '18'
      
      - name: Install dependencies
        run: npm ci
        working-directory: ./react-app
      
      - name: Run tests
        run: npm test -- --watchAll=false
        working-directory: ./react-app
      
      - name: Build
        run: npm run build
        working-directory: ./react-app

  build-and-push:
    needs: [test-dotnet, test-php, test-react]
    runs-on: ubuntu-latest
    if: github.event_name == 'push' && github.ref == 'refs/heads/main'
    
    strategy:
      matrix:
        service: [dotnet-api, php-service, react-app]
    
    steps:
      - uses: actions/checkout@v3
      
      - name: Log in to Container Registry
        uses: docker/login-action@v2
        with:
          registry: ${{ env.REGISTRY }}
          username: ${{ github.actor }}
          password: ${{ secrets.GITHUB_TOKEN }}
      
      - name: Extract metadata
        id: meta
        uses: docker/metadata-action@v4
        with:
          images: ${{ env.REGISTRY }}/${{ env.IMAGE_NAME }}-${{ matrix.service }}
      
      - name: Build and push Docker image
        uses: docker/build-push-action@v4
        with:
          context: ./${{ matrix.service }}
          push: true
          tags: ${{ steps.meta.outputs.tags }}
          labels: ${{ steps.meta.outputs.labels }}

  deploy:
    needs: build-and-push
    runs-on: ubuntu-latest
    if: github.event_name == 'push' && github.ref == 'refs/heads/main'
    
    steps:
      - uses: actions/checkout@v3
      
      - name: Deploy to Kubernetes
        uses: azure/k8s-deploy@v4
        with:
          manifests: |
            k8s/namespace.yaml
            k8s/mysql-deployment.yaml
            k8s/api-deployment.yaml
            k8s/php-deployment.yaml
            k8s/react-deployment.yaml
            k8s/ingress.yaml
          images: |
            ${{ env.REGISTRY }}/${{ env.IMAGE_NAME }}-dotnet-api:latest
            ${{ env.REGISTRY }}/${{ env.IMAGE_NAME }}-php-service:latest
            ${{ env.REGISTRY }}/${{ env.IMAGE_NAME }}-react-app:latest
```

---

## 4. Infrastructure as Code

### Priority: MEDIUM

#### Terraform (AWS Example)

**terraform/main.tf**
```hcl
provider "aws" {
  region = var.aws_region
}

# ECS Cluster
resource "aws_ecs_cluster" "code_review" {
  name = "code-review-cluster"
}

# RDS MySQL
resource "aws_db_instance" "mysql" {
  identifier           = "code-review-db"
  engine              = "mysql"
  engine_version      = "8.0"
  instance_class      = "db.t3.micro"
  allocated_storage   = 20
  storage_encrypted   = true
  
  db_name  = "code_review_tool"
  username = var.db_username
  password = var.db_password
  
  vpc_security_group_ids = [aws_security_group.rds.id]
  db_subnet_group_name   = aws_db_subnet_group.main.name
  
  backup_retention_period = 7
  skip_final_snapshot    = false
  final_snapshot_identifier = "code-review-final-snapshot"
}

# Application Load Balancer
resource "aws_lb" "main" {
  name               = "code-review-alb"
  internal           = false
  load_balancer_type = "application"
  security_groups    = [aws_security_group.alb.id]
  subnets           = aws_subnet.public[*].id
}

# ECS Task Definitions
resource "aws_ecs_task_definition" "api" {
  family                   = "code-review-api"
  network_mode             = "awsvpc"
  requires_compatibilities = ["FARGATE"]
  cpu                      = "512"
  memory                   = "1024"
  
  container_definitions = jsonencode([{
    name  = "dotnet-api"
    image = "${var.ecr_repository}/code-review-api:latest"
    portMappings = [{
      containerPort = 5116
      protocol      = "tcp"
    }]
    environment = [
      { name = "DB_HOST", value = aws_db_instance.mysql.address },
      { name = "DB_NAME", value = "code_review_tool" }
    ]
    secrets = [
      { name = "JWT_SECRET_KEY", valueFrom = aws_secretsmanager_secret.jwt.arn }
    ]
    logConfiguration = {
      logDriver = "awslogs"
      options = {
        "awslogs-group"         = "/ecs/code-review-api"
        "awslogs-region"        = var.aws_region
        "awslogs-stream-prefix" = "ecs"
      }
    }
  }])
}
```

---

## 5. Monitoring & Observability

### Priority: HIGH

#### Prometheus Metrics

**dotnet-api: Add Prometheus Exporter**
```bash
dotnet add package prometheus-net.AspNetCore
```

**Program.cs additions:**
```csharp
using Prometheus;

// Add before app.Run()
app.UseMetricServer(); // Exposes /metrics endpoint
app.UseHttpMetrics();  // Tracks HTTP metrics
```

#### Grafana Dashboard JSON
Create `monitoring/grafana-dashboard.json` with panels for:
- Request rate
- Error rate
- Response time (p50, p95, p99)
- Active analyses
- Database connection pool
- GitHub API rate limit

#### ELK Stack Configuration

**docker-compose.monitoring.yml**
```yaml
version: '3.8'

services:
  elasticsearch:
    image: docker.elastic.co/elasticsearch/elasticsearch:8.11.0
    environment:
      - discovery.type=single-node
      - "ES_JAVA_OPTS=-Xms512m -Xmx512m"
    ports:
      - "9200:9200"
    volumes:
      - elasticsearch_data:/usr/share/elasticsearch/data

  logstash:
    image: docker.elastic.co/logstash/logstash:8.11.0
    volumes:
      - ./logstash/pipeline:/usr/share/logstash/pipeline
    ports:
      - "5000:5000"
    depends_on:
      - elasticsearch

  kibana:
    image: docker.elastic.co/kibana/kibana:8.11.0
    ports:
      - "5601:5601"
    depends_on:
      - elasticsearch

volumes:
  elasticsearch_data:
```

---

## 6. Security Enhancements

### Priority: HIGH

#### Secrets Management

**Use AWS Secrets Manager / Azure Key Vault**
```csharp
// Program.cs
builder.Configuration.AddAzureKeyVault(
    new Uri($"https://{keyVaultName}.vault.azure.net/"),
    new DefaultAzureCredential());
```

#### Security Scanning

**.github/workflows/security.yml**
```yaml
name: Security Scan

on:
  push:
    branches: [ main ]
  schedule:
    - cron: '0 0 * * 0'  # Weekly

jobs:
  trivy-scan:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      
      - name: Run Trivy vulnerability scanner
        uses: aquasecurity/trivy-action@master
        with:
          scan-type: 'fs'
          scan-ref: '.'
          format: 'sarif'
          output: 'trivy-results.sarif'
      
      - name: Upload to GitHub Security
        uses: github/codeql-action/upload-sarif@v2
        with:
          sarif_file: 'trivy-results.sarif'

  dependency-check:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      
      - name: OWASP Dependency Check
        uses: dependency-check/Dependency-Check_Action@main
        with:
          project: 'code-review-tool'
          path: '.'
          format: 'HTML'
```

---

## 7. Performance Optimization

### Priority: MEDIUM

#### Redis Caching Layer

**docker-compose.yml addition:**
```yaml
  redis:
    image: redis:7-alpine
    ports:
      - "6379:6379"
    volumes:
      - redis_data:/data
    healthcheck:
      test: ["CMD", "redis-cli", "ping"]
      interval: 10s
      timeout: 3s
      retries: 3
```

**dotnet-api: Add Redis**
```bash
dotnet add package StackExchange.Redis
```

#### Database Indexing

**DB_Schema.sql additions:**
```sql
-- Add composite indexes for common queries
CREATE INDEX idx_runs_user_created ON analysis_runs(repository_id, created_at DESC);
CREATE INDEX idx_issues_run_severity ON analysis_issues(run_id, severity, category);
CREATE INDEX idx_repos_user_name ON repositories(user_id, name);
```

---

## 8. Documentation

### Priority: HIGH

#### API Documentation

**Add Swagger UI:**
```csharp
// Program.cs
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Code Review API",
        Version = "v1",
        Description = "Automated code review and analysis API"
    });
    
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
});

app.UseSwagger();
app.UseSwaggerUI();
```

#### Architecture Diagram

Create `docs/architecture.md` with:
- System architecture diagram
- Data flow diagram
- Deployment architecture
- Security architecture

---

## 9. Deployment Strategies

### Blue-Green Deployment

**k8s/blue-green-deployment.yaml**
```yaml
apiVersion: v1
kind: Service
metadata:
  name: dotnet-api
spec:
  selector:
    app: dotnet-api
    version: blue  # Switch to 'green' for deployment
  ports:
  - port: 5116
---
apiVersion: apps/v1
kind: Deployment
metadata:
  name: dotnet-api-blue
spec:
  replicas: 3
  selector:
    matchLabels:
      app: dotnet-api
      version: blue
  template:
    metadata:
      labels:
        app: dotnet-api
        version: blue
    spec:
      containers:
      - name: api
        image: your-registry/code-review-api:v1.0.0
---
apiVersion: apps/v1
kind: Deployment
metadata:
  name: dotnet-api-green
spec:
  replicas: 3
  selector:
    matchLabels:
      app: dotnet-api
      version: green
  template:
    metadata:
      labels:
        app: dotnet-api
        version: green
    spec:
      containers:
      - name: api
        image: your-registry/code-review-api:v1.1.0
```

---

## 10. Quick Wins Checklist

### Immediate Actions (1-2 days):
- [ ] Create docker-compose.yml
- [ ] Add Dockerfiles for all services
- [ ] Set up GitHub Actions CI
- [ ] Add Swagger documentation
- [ ] Create comprehensive README

### Short-term (3-5 days):
- [ ] Kubernetes manifests
- [ ] Terraform/IaC scripts
- [ ] Prometheus metrics
- [ ] Security scanning
- [ ] Load testing scripts

### Medium-term (1-2 weeks):
- [ ] ELK stack integration
- [ ] Grafana dashboards
- [ ] Blue-green deployment
- [ ] Redis caching
- [ ] Database optimization

---

## Showcase Portfolio Items

### What to Highlight:
1. **Multi-service orchestration** (Docker Compose)
2. **Container optimization** (multi-stage builds)
3. **Kubernetes deployment** (manifests, ingress, secrets)
4. **CI/CD automation** (GitHub Actions)
5. **Infrastructure as Code** (Terraform)
6. **Monitoring & observability** (Prometheus, Grafana, ELK)
7. **Security practices** (secrets management, scanning)
8. **High availability** (load balancing, health checks)
9. **Scalability** (horizontal pod autoscaling)
10. **Documentation** (architecture diagrams, API docs)

### Portfolio Presentation:
- Create a detailed README with architecture diagrams
- Include screenshots of Grafana dashboards
- Show CI/CD pipeline execution
- Document deployment process step-by-step
- Include performance benchmarks
- Demonstrate monitoring and alerting
