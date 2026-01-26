# 🚀 Oracle Cloud Free Tier Deployment Guide

## 📋 Overview

This guide will walk you through deploying your **Code Review Automation Tool** to Oracle Cloud's **Always Free Tier** - giving you real cloud infrastructure experience at **$0 cost**.

**What you'll build:**
```
Internet (HTTPS) → Oracle Cloud VM
                    ├── Nginx (Reverse Proxy + SSL)
                    ├── Docker Compose
                    │   ├── React Frontend
                    │   ├── .NET API
                    │   ├── PHP Service
                    │   ├── MySQL Database
                    │   ├── Prometheus
                    │   └── Grafana
                    └── Firewall (Security)
```

**Time Required:** 4-6 hours (first time)

**DevOps Skills You'll Demonstrate:**
- ✅ Cloud infrastructure provisioning (Oracle Cloud)
- ✅ Linux server administration (Ubuntu)
- ✅ Docker Compose in production
- ✅ Nginx reverse proxy configuration
- ✅ SSL/TLS setup (Let's Encrypt)
- ✅ Firewall & security hardening
- ✅ Monitoring in production (Prometheus + Grafana)
- ✅ Real-world DevOps practices

---

## Phase 1: Oracle Cloud Account Setup (15 minutes)

### Step 1.1: Create Oracle Cloud Account

1. **Go to:** https://www.oracle.com/cloud/free/
2. **Click:** "Start for free"
3. **Fill in:**
   - Email address (use your student email if possible)
   - Country/Region
   - Cloud Account Name (e.g., `yourname-cloud`)
4. **Verify email** and complete registration
5. **Important:** You'll need to provide a credit card for verification, but **you will NOT be charged** unless you manually upgrade

**What you get (ALWAYS FREE):**
- 2 AMD Compute VMs (1GB RAM each)
- 4 ARM-based Ampere A1 cores (24GB RAM total)
- 200GB Block Volume Storage
- 10TB outbound data transfer/month
- Load Balancer (1 instance)

### Step 1.2: Sign In to Console

1. Go to: https://cloud.oracle.com/
2. Enter your **Cloud Account Name**
3. Click **Continue**
4. Sign in with your credentials
5. You'll land on the **Oracle Cloud Console Dashboard**

---

## Phase 2: Create a Virtual Machine (30 minutes)

### Step 2.1: Launch Compute Instance

1. **From Dashboard**, click **"Create a VM instance"** (or navigate to: Compute → Instances)
2. **Name your instance:** `code-review-server`
3. **Placement:**
   - Keep default compartment
   - Choose availability domain (any)

### Step 2.2: Choose Image and Shape

**Image:**
1. Click **"Change Image"**
2. Select **"Canonical Ubuntu"** (22.04 or latest LTS)
3. Click **"Select Image"**

**Shape:**
1. Click **"Change Shape"**
2. Select **"Ampere"** (ARM-based - Always Free)
3. Choose **"VM.Standard.A1.Flex"**
4. Set:
   - **OCPUs:** 2
   - **Memory (GB):** 12
5. Click **"Select Shape"**

**Why ARM?** The Always Free ARM instances are more powerful (24GB RAM total vs 2GB for AMD)

### Step 2.3: Configure Networking

**Virtual Cloud Network:**
- Keep **"Create new virtual cloud network"** selected
- VCN Name: `code-review-vcn`
- Subnet Name: `code-review-subnet`

**Public IP:**
- ✅ **Check:** "Assign a public IPv4 address"

### Step 2.4: Add SSH Keys

**Important:** You need SSH keys to access your server.

**On Windows (PowerShell):**
```powershell
# Generate SSH key pair
ssh-keygen -t rsa -b 4096 -f C:\Users\YourUsername\.ssh\oracle_cloud_key

# This creates:
# - oracle_cloud_key (private key - keep secret!)
# - oracle_cloud_key.pub (public key - upload to Oracle)
```

**In Oracle Console:**
1. Under **"Add SSH keys"**, select **"Paste public keys"**
2. Open `oracle_cloud_key.pub` in Notepad
3. Copy the entire content (starts with `ssh-rsa`)
4. Paste into the text box

### Step 2.5: Configure Boot Volume

- **Boot volume size:** 50 GB (default is fine)
- Keep other settings as default

### Step 2.6: Create the Instance

1. Click **"Create"** at the bottom
2. Wait 2-3 minutes for provisioning
3. **Status will change:** Provisioning → Running (orange → green)

### Step 2.7: Note Your Public IP

Once running:
1. Click on your instance name
2. Find **"Public IP address"** (e.g., `123.45.67.89`)
3. **Copy this IP** - you'll need it!

---

## Phase 3: Configure Firewall Rules (15 minutes)

Oracle Cloud has **two layers of firewall**. You need to open ports in both.

### Step 3.1: Security List (Cloud Firewall)

1. From your instance page, click on the **Subnet name** (under "Primary VNIC")
2. Click on the **Security List** (e.g., "Default Security List for code-review-vcn")
3. Click **"Add Ingress Rules"**

**Add these rules one by one:**

**Rule 1: HTTP (Port 80)**
- Source CIDR: `0.0.0.0/0`
- IP Protocol: `TCP`
- Destination Port Range: `80`
- Description: `HTTP`
- Click **"Add Ingress Rules"**

**Rule 2: HTTPS (Port 443)**
- Source CIDR: `0.0.0.0/0`
- IP Protocol: `TCP`
- Destination Port Range: `443`
- Description: `HTTPS`
- Click **"Add Ingress Rules"**

**Rule 3: Grafana (Port 3001)**
- Source CIDR: `0.0.0.0/0`
- IP Protocol: `TCP`
- Destination Port Range: `3001`
- Description: `Grafana`
- Click **"Add Ingress Rules"**

**Rule 4: Prometheus (Port 9090)**
- Source CIDR: `0.0.0.0/0`
- IP Protocol: `TCP`
- Destination Port Range: `9090`
- Description: `Prometheus`
- Click **"Add Ingress Rules"**

**Note:** SSH (port 22) is already open by default.

### Step 3.2: Ubuntu Firewall (iptables)

We'll configure this after connecting to the server.

---

## Phase 4: Connect to Your Server (10 minutes)

### Step 4.1: SSH into Your Server

**On Windows (PowerShell):**
```powershell
# Replace with YOUR public IP
ssh -i C:\Users\YourUsername\.ssh\oracle_cloud_key ubuntu@123.45.67.89
```

**First time connecting:**
- You'll see: "The authenticity of host... can't be established"
- Type: `yes` and press Enter

**You should see:**
```
Welcome to Ubuntu 22.04.x LTS
...
ubuntu@code-review-server:~$
```

**You're in!** 🎉

### Step 4.2: Update System

```bash
# Update package lists
sudo apt update

# Upgrade installed packages
sudo apt upgrade -y

# This may take 5-10 minutes
```

---

## Phase 5: Install Docker & Docker Compose (20 minutes)

### Step 5.1: Install Docker

```bash
# Install prerequisites
sudo apt install -y ca-certificates curl gnupg lsb-release

# Add Docker's official GPG key
sudo mkdir -p /etc/apt/keyrings
curl -fsSL https://download.docker.com/linux/ubuntu/gpg | sudo gpg --dearmor -o /etc/apt/keyrings/docker.gpg

# Set up Docker repository
echo \
  "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] https://download.docker.com/linux/ubuntu \
  $(lsb_release -cs) stable" | sudo tee /etc/apt/sources.list.d/docker.list > /dev/null

# Install Docker
sudo apt update
sudo apt install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin

# Verify installation
sudo docker --version
# Should show: Docker version 24.x.x or higher
```

### Step 5.2: Add User to Docker Group

```bash
# Add ubuntu user to docker group (so you don't need sudo)
sudo usermod -aG docker ubuntu

# Apply group changes (logout and login)
exit
```

**Reconnect to server:**
```powershell
ssh -i C:\Users\YourUsername\.ssh\oracle_cloud_key ubuntu@123.45.67.89
```

**Test Docker without sudo:**
```bash
docker ps
# Should show empty list (no containers yet)
```

### Step 5.3: Verify Docker Compose

```bash
docker compose version
# Should show: Docker Compose version v2.x.x or higher
```

---

## Phase 6: Configure Ubuntu Firewall (10 minutes)

```bash
# Allow SSH (important - don't lock yourself out!)
sudo ufw allow 22/tcp

# Allow HTTP and HTTPS
sudo ufw allow 80/tcp
sudo ufw allow 443/tcp

# Allow Grafana
sudo ufw allow 3001/tcp

# Allow Prometheus
sudo ufw allow 9090/tcp

# Enable firewall
sudo ufw enable
# Type 'y' when prompted

# Check status
sudo ufw status
```

**Expected output:**
```
Status: active

To                         Action      From
--                         ------      ----
22/tcp                     ALLOW       Anywhere
80/tcp                     ALLOW       Anywhere
443/tcp                    ALLOW       Anywhere
3001/tcp                   ALLOW       Anywhere
9090/tcp                   ALLOW       Anywhere
```

---

## Phase 7: Clone Your Repository (5 minutes)

### Step 7.1: Install Git

```bash
sudo apt install -y git
```

### Step 7.2: Clone Your Project

```bash
# Navigate to home directory
cd ~

# Clone your repository (replace with your GitHub URL)
git clone https://github.com/YOUR_USERNAME/Review_Code.git

# Navigate into project
cd Review_Code

# Verify files
ls -la
```

**You should see:**
- `docker-compose.yml`
- `dotnet-api/`
- `php-service/`
- `react-app/`
- `.env.example`
- etc.

---

## Phase 8: Configure Environment Variables (10 minutes)

### Step 8.1: Create .env File

```bash
# Copy example file
cp .env.example .env

# Edit with nano
nano .env
```

### Step 8.2: Update Environment Variables

**Update these values:**

```env
# Database (Docker internal networking)
DB_HOST=mysql
DB_PORT=3306
DB_NAME=code_review_tool
DB_USER=root
DB_PASSWORD=YourSecurePassword123!  # Change this!

# JWT Secret (generate a strong one)
JWT_SECRET_KEY=your_super_secret_jwt_key_minimum_32_characters_long_change_this

# Services (Docker internal URLs)
PHP_ANALYSIS_API_URL=http://php-service:8000/api/analyze/files

# GitHub Personal Access Token (optional - for private repos)
GITHUB_PAT=your_github_token_here

# Internal service secret
INTERNAL_SERVICE_SECRET=another_secret_key_change_this_too

# React App API URL (use your Oracle Cloud public IP)
REACT_APP_API_URL=http://123.45.67.89:5116
```

**Save and exit:**
- Press `Ctrl + X`
- Press `Y` (yes)
- Press `Enter`

### Step 8.3: Create MySQL Config

```bash
# Create .my.cnf for MySQL exporter
nano .my.cnf
```

**Paste this:**
```ini
[client]
user=root
password=YourSecurePassword123!
host=mysql
```

**Save and exit** (Ctrl+X, Y, Enter)

---

## Phase 9: Deploy with Docker Compose (20 minutes)

### Step 9.1: Build and Start Services

```bash
# Make sure you're in the project directory
cd ~/Review_Code

# Pull/build images and start services
docker compose up -d

# This will:
# 1. Pull MySQL, Prometheus, Grafana images
# 2. Build .NET API, PHP, React images
# 3. Start all services
# This takes 10-15 minutes on first run
```

### Step 9.2: Monitor Progress

```bash
# Watch logs (Ctrl+C to exit)
docker compose logs -f

# Or check specific service
docker compose logs -f dotnet-api
```

### Step 9.3: Verify All Services Running

```bash
# Check running containers
docker compose ps
```

**Expected output (all should show "Up" and "healthy"):**
```
NAME                        STATUS
code-review-api             Up (healthy)
code-review-db              Up (healthy)
code-review-frontend        Up (healthy)
code-review-grafana         Up
code-review-mysql-exporter  Up
code-review-php             Up (healthy)
code-review-prometheus      Up
```

### Step 9.4: Test Health Endpoints

```bash
# Test .NET API
curl http://localhost:5116/health
# Should return: Healthy

# Test PHP service
curl http://localhost:8000/health
# Should return: {"status":"healthy",...}

# Test React (from your local machine browser)
# Open: http://YOUR_PUBLIC_IP:3000
```

---

## Phase 10: Install and Configure Nginx (30 minutes)

### Step 10.1: Install Nginx

```bash
sudo apt install -y nginx
```

### Step 10.2: Create Nginx Configuration

```bash
# Remove default config
sudo rm /etc/nginx/sites-enabled/default

# Create new config
sudo nano /etc/nginx/sites-available/code-review
```

**Paste this configuration:**

```nginx
# Upstream definitions
upstream react_frontend {
    server localhost:3000;
}

upstream dotnet_api {
    server localhost:5116;
}

upstream grafana {
    server localhost:3001;
}

upstream prometheus {
    server localhost:9090;
}

# Main application
server {
    listen 80;
    server_name YOUR_PUBLIC_IP;  # Replace with your IP

    # React Frontend
    location / {
        proxy_pass http://react_frontend;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection 'upgrade';
        proxy_set_header Host $host;
        proxy_cache_bypass $http_upgrade;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }

    # .NET API
    location /api/ {
        proxy_pass http://dotnet_api/api/;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        
        # CORS headers
        add_header 'Access-Control-Allow-Origin' '*' always;
        add_header 'Access-Control-Allow-Methods' 'GET, POST, PUT, DELETE, OPTIONS' always;
        add_header 'Access-Control-Allow-Headers' 'Authorization, Content-Type' always;
        
        if ($request_method = 'OPTIONS') {
            return 204;
        }
    }
}

# Grafana (separate port)
server {
    listen 3001;
    server_name YOUR_PUBLIC_IP;  # Replace with your IP

    location / {
        proxy_pass http://grafana;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection 'upgrade';
        proxy_set_header Host $host;
        proxy_cache_bypass $http_upgrade;
    }
}

# Prometheus (separate port)
server {
    listen 9090;
    server_name YOUR_PUBLIC_IP;  # Replace with your IP

    location / {
        proxy_pass http://prometheus;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
    }
}
```

**Replace `YOUR_PUBLIC_IP` with your actual IP** (2 places)

**Save and exit** (Ctrl+X, Y, Enter)

### Step 10.3: Enable Configuration

```bash
# Create symbolic link
sudo ln -s /etc/nginx/sites-available/code-review /etc/nginx/sites-enabled/

# Test configuration
sudo nginx -t
# Should show: syntax is ok, test is successful

# Restart Nginx
sudo systemctl restart nginx

# Enable Nginx to start on boot
sudo systemctl enable nginx
```

---

## Phase 11: Test Your Deployment (15 minutes)

### Step 11.1: Test from Browser

**Open these URLs in your browser:**

1. **React Frontend:**
   - `http://YOUR_PUBLIC_IP`
   - Should show login page

2. **Grafana:**
   - `http://YOUR_PUBLIC_IP:3001`
   - Login: `admin` / `admin`

3. **Prometheus:**
   - `http://YOUR_PUBLIC_IP:9090`
   - Should show Prometheus UI

### Step 11.2: Test API Endpoints

**From your local machine:**
```powershell
# Test .NET API health
curl http://YOUR_PUBLIC_IP/api/health

# Test metrics
curl http://YOUR_PUBLIC_IP/api/metrics
```

### Step 11.3: Create Test Account

1. Go to `http://YOUR_PUBLIC_IP`
2. Click **"Register"**
3. Create an account
4. Login
5. Try running an analysis on a public GitHub repo

---

## Phase 12: Optional - Set Up SSL with Let's Encrypt (30 minutes)

**Note:** This requires a domain name. If you don't have one, skip this section.

### Step 12.1: Point Domain to Your IP

1. Buy a domain (or use a free one from Freenom)
2. Add an **A record** pointing to your Oracle Cloud public IP

### Step 12.2: Install Certbot

```bash
sudo apt install -y certbot python3-certbot-nginx
```

### Step 12.3: Get SSL Certificate

```bash
# Replace with your domain
sudo certbot --nginx -d yourdomain.com -d www.yourdomain.com
```

Follow the prompts:
- Enter email
- Agree to terms
- Choose to redirect HTTP to HTTPS

**Certbot will automatically:**
- Get SSL certificate
- Update Nginx config
- Set up auto-renewal

---

## Phase 13: Monitoring & Maintenance

### Step 13.1: Check Service Status

```bash
# Check all containers
docker compose ps

# Check logs
docker compose logs -f

# Check specific service
docker compose logs -f dotnet-api
```

### Step 13.2: Restart Services

```bash
# Restart all services
docker compose restart

# Restart specific service
docker compose restart dotnet-api
```

### Step 13.3: Update Application

```bash
# Pull latest code
cd ~/Review_Code
git pull

# Rebuild and restart
docker compose down
docker compose up -d --build
```

### Step 13.4: Monitor Resources

```bash
# Check disk usage
df -h

# Check memory
free -h

# Check Docker disk usage
docker system df
```

---

## 🎯 Success Checklist

**Your deployment is successful if:**

- [ ] All Docker containers are running (`docker compose ps`)
- [ ] React frontend loads at `http://YOUR_IP`
- [ ] You can register and login
- [ ] API health endpoint responds (`http://YOUR_IP/api/health`)
- [ ] Grafana accessible at `http://YOUR_IP:3001`
- [ ] Prometheus accessible at `http://YOUR_IP:9090`
- [ ] You can run a code analysis successfully
- [ ] Metrics appear in Grafana dashboards

---

## 🔧 Troubleshooting

### Issue: Containers won't start

```bash
# Check logs
docker compose logs

# Check specific service
docker compose logs dotnet-api

# Common fix: rebuild
docker compose down
docker compose up -d --build
```

### Issue: Can't access from browser

```bash
# Check firewall
sudo ufw status

# Check Nginx
sudo nginx -t
sudo systemctl status nginx

# Check if ports are listening
sudo netstat -tlnp | grep -E '80|443|3001|9090'
```

### Issue: Out of memory

```bash
# Check memory
free -h

# Stop some services temporarily
docker compose stop prometheus grafana

# Or increase swap
sudo fallocate -l 4G /swapfile
sudo chmod 600 /swapfile
sudo mkswap /swapfile
sudo swapon /swapfile
```

### Issue: Database connection failed

```bash
# Check MySQL container
docker compose logs mysql

# Restart MySQL
docker compose restart mysql

# Check .env file has correct DB_PASSWORD
cat .env | grep DB_PASSWORD
```

---

## 📊 What You've Accomplished

**Infrastructure:**
- ✅ Provisioned cloud VM (Oracle Cloud)
- ✅ Configured networking & firewall
- ✅ Set up Linux server (Ubuntu)

**Deployment:**
- ✅ Installed Docker & Docker Compose
- ✅ Deployed multi-service application
- ✅ Configured reverse proxy (Nginx)
- ✅ Set up monitoring (Prometheus + Grafana)

**DevOps Skills Demonstrated:**
- ✅ Cloud infrastructure provisioning
- ✅ Linux system administration
- ✅ Container orchestration
- ✅ Reverse proxy configuration
- ✅ Security hardening (firewall)
- ✅ Production monitoring
- ✅ Real-world deployment practices

---

## 🎤 Interview Talking Points

**"Tell me about a project you deployed to the cloud"**

> "I deployed a microservices application to Oracle Cloud's free tier using Docker Compose. The architecture includes a React frontend, .NET API, PHP analysis service, and MySQL database, all running in containers behind an Nginx reverse proxy. I configured security groups, set up SSL with Let's Encrypt, and implemented production monitoring with Prometheus and Grafana. The deployment demonstrates my understanding of cloud infrastructure, containerization, and DevOps best practices."

**Key points to mention:**
- Multi-service architecture
- Docker containerization
- Nginx reverse proxy
- Cloud infrastructure (Oracle Cloud)
- Security configuration (firewall, SSL)
- Production monitoring (Prometheus/Grafana)
- Infrastructure management

---

## 📚 Next Steps

**To make this even more impressive:**

1. **Add a domain name** (use Freenom for free)
2. **Set up SSL** with Let's Encrypt
3. **Create Terraform configuration** (Infrastructure as Code)
4. **Set up automated backups** for database
5. **Configure alerts** in Grafana
6. **Add log aggregation** (ELK stack)
7. **Document your architecture** with diagrams

---

## 💾 Backup Your Work

**Save your configuration:**

```bash
# From your local machine, backup important files
scp -i C:\Users\YourUsername\.ssh\oracle_cloud_key ubuntu@YOUR_IP:~/Review_Code/.env ./backup/.env
scp -i C:\Users\YourUsername\.ssh\oracle_cloud_key ubuntu@YOUR_IP:/etc/nginx/sites-available/code-review ./backup/nginx.conf
```

**Document your setup:**
- Take screenshots of Grafana dashboards
- Note your public IP
- Save your SSH key safely
- Document any customizations

---

## 🎉 Congratulations!

You now have a **production-grade deployment** running on **Oracle Cloud's Always Free Tier**!

This demonstrates **real DevOps skills** that employers value:
- Cloud infrastructure
- Container orchestration
- Production deployment
- Monitoring & observability
- Security best practices

**Add this to your CV/portfolio!** 🚀

---

**Created:** 2026-01-26  
**Platform:** Oracle Cloud Free Tier  
**Cost:** $0 (Always Free)  
**Status:** Production-Ready
