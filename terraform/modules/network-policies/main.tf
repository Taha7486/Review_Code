# ═══════════════════════════════════════════════════════════════════════════════
# Step 3.3 + 3.4  — Default-deny baseline + DNS allow
#
# SAFETY RULE: these two resources have no depends_on between them so Terraform
# creates them in parallel. Never add a dependency that would cause default_deny
# to be created before allow_dns_egress — a DNS outage would follow immediately.
# Both are included in every apply together with the app policies below.
# ═══════════════════════════════════════════════════════════════════════════════

resource "kubernetes_network_policy" "default_deny_all" {
  metadata {
    name      = "default-deny-all"
    namespace = var.namespace
    labels    = var.labels
  }
  spec {
    pod_selector {} # empty = all pods in namespace
    policy_types = ["Ingress", "Egress"]
    # No ingress or egress rules → deny all traffic in both directions
  }
}

# Step 3.4 — DNS egress for every pod in the namespace.
# CoreDNS pods are in kube-system, labeled k8s-app=kube-dns.
# namespace_selector + pod_selector in the same "to" block = AND semantics.
resource "kubernetes_network_policy" "allow_dns_egress" {
  metadata {
    name      = "allow-dns-egress"
    namespace = var.namespace
    labels    = var.labels
  }
  spec {
    pod_selector {} # all pods
    policy_types = ["Egress"]
    egress {
      to {
        namespace_selector {
          match_labels = {
            "kubernetes.io/metadata.name" = "kube-system"
          }
        }
        pod_selector {
          match_labels = {
            "k8s-app" = "kube-dns"
          }
        }
      }
      ports {
        port     = "53"
        protocol = "UDP"
      }
      ports {
        port     = "53"
        protocol = "TCP"
      }
    }
  }
}

# ═══════════════════════════════════════════════════════════════════════════════
# Step 3.5 — Application allow policies
# ═══════════════════════════════════════════════════════════════════════════════

# External browser → react-app:80 via NodePort 30000.
# Calico enforces NetworkPolicy after kube-proxy DNAT, so an explicit allow is
# required. Empty "from" in a non-empty ingress rule means "from any source".
resource "kubernetes_network_policy" "allow_react_app_ingress" {
  metadata {
    name      = "allow-react-app-ingress"
    namespace = var.namespace
    labels    = var.labels
  }
  spec {
    pod_selector {
      match_labels = {
        "app" = "react-app"
      }
    }
    policy_types = ["Ingress"]
    ingress {
      # No "from" block = allow from any source (NodePort traffic post-DNAT)
      ports {
        port     = "80"
        protocol = "TCP"
      }
    }
  }
}

# react-app (nginx) → code-review-api ClusterIP → dotnet-api pods:5116.
# Note: nginx.conf proxies /api/ to code-review-api:5116 (ClusterIP service).
# Both the ClusterIP "code-review-api" and NodePort "dotnet-api" select the
# same pods (app=dotnet-api), so a pod-selector policy covers both paths.
resource "kubernetes_network_policy" "allow_react_app_egress" {
  metadata {
    name      = "allow-react-app-egress"
    namespace = var.namespace
    labels    = var.labels
  }
  spec {
    pod_selector {
      match_labels = {
        "app" = "react-app"
      }
    }
    policy_types = ["Egress"]
    egress {
      to {
        pod_selector {
          match_labels = {
            "app" = "dotnet-api"
          }
        }
      }
      ports {
        port     = "5116"
        protocol = "TCP"
      }
    }
  }
}

# dotnet-api ingress: from react-app and from prometheus (metrics scrape).
# Two separate ingress rules — both on port 5116 but from different sources.
resource "kubernetes_network_policy" "allow_dotnet_api_ingress" {
  metadata {
    name      = "allow-dotnet-api-ingress"
    namespace = var.namespace
    labels    = var.labels
  }
  spec {
    pod_selector {
      match_labels = {
        "app" = "dotnet-api"
      }
    }
    policy_types = ["Ingress"]
    ingress {
      from {
        pod_selector {
          match_labels = {
            "app" = "react-app"
          }
        }
      }
      ports {
        port     = "5116"
        protocol = "TCP"
      }
    }
    ingress {
      from {
        pod_selector {
          match_labels = {
            "app" = "prometheus"
          }
        }
      }
      ports {
        port     = "5116"
        protocol = "TCP"
      }
    }
  }
}

# dotnet-api egress:
#   → mysql:3306      (EF Core database, also used by initContainer nc probe)
#   → php-service:8000 (analysis engine calls)
#   → public internet:443 (GitHub API via Octokit/HttpClient)
#
# Port 443 uses ipBlock with private-range exceptions so dotnet-api cannot reach
# internal services (Vault API, kube-apiserver) on port 443 — only public IPs.
resource "kubernetes_network_policy" "allow_dotnet_api_egress" {
  metadata {
    name      = "allow-dotnet-api-egress"
    namespace = var.namespace
    labels    = var.labels
  }
  spec {
    pod_selector {
      match_labels = {
        "app" = "dotnet-api"
      }
    }
    policy_types = ["Egress"]
    egress {
      to {
        pod_selector {
          match_labels = {
            "app" = "mysql"
          }
        }
      }
      ports {
        port     = "3306"
        protocol = "TCP"
      }
    }
    egress {
      to {
        pod_selector {
          match_labels = {
            "app" = "php-service"
          }
        }
      }
      ports {
        port     = "8000"
        protocol = "TCP"
      }
    }
    egress {
      to {
        ip_block {
          cidr = "0.0.0.0/0"
          except = [
            "10.0.0.0/8",
            "172.16.0.0/12",
            "192.168.0.0/16",
          ]
        }
      }
      ports {
        port     = "443"
        protocol = "TCP"
      }
    }
  }
}

# mysql ingress: only from dotnet-api on port 3306.
# The dotnet-api initContainer (busybox nc -z mysql 3306) shares the pod
# network namespace, so it is covered by this same pod-selector rule.
resource "kubernetes_network_policy" "allow_mysql_ingress" {
  metadata {
    name      = "allow-mysql-ingress"
    namespace = var.namespace
    labels    = var.labels
  }
  spec {
    pod_selector {
      match_labels = {
        "app" = "mysql"
      }
    }
    policy_types = ["Ingress"]
    ingress {
      from {
        pod_selector {
          match_labels = {
            "app" = "dotnet-api"
          }
        }
      }
      ports {
        port     = "3306"
        protocol = "TCP"
      }
    }
  }
}

# php-service ingress: from dotnet-api (analysis calls) and prometheus (scrape).
resource "kubernetes_network_policy" "allow_php_service_ingress" {
  metadata {
    name      = "allow-php-service-ingress"
    namespace = var.namespace
    labels    = var.labels
  }
  spec {
    pod_selector {
      match_labels = {
        "app" = "php-service"
      }
    }
    policy_types = ["Ingress"]
    ingress {
      from {
        pod_selector {
          match_labels = {
            "app" = "dotnet-api"
          }
        }
      }
      ports {
        port     = "8000"
        protocol = "TCP"
      }
    }
    ingress {
      from {
        pod_selector {
          match_labels = {
            "app" = "prometheus"
          }
        }
      }
      ports {
        port     = "8000"
        protocol = "TCP"
      }
    }
  }
}

# ═══════════════════════════════════════════════════════════════════════════════
# Step 3.6 — Prometheus scraping policy
# ═══════════════════════════════════════════════════════════════════════════════

# prometheus egress:
#   → dotnet-api:5116   (pod-IP scrape via kubernetes_sd_configs role:pod)
#   → php-service:8000  (pod-IP scrape)
#   → 0.0.0.0/0:443     (Kubernetes API server at kubernetes.default.svc:443
#                         for pod SD discovery — API server is not a pod so
#                         pod_selector cannot be used; allow broad port 443)
resource "kubernetes_network_policy" "allow_prometheus_egress" {
  metadata {
    name      = "allow-prometheus-egress"
    namespace = var.namespace
    labels    = var.labels
  }
  spec {
    pod_selector {
      match_labels = {
        "app" = "prometheus"
      }
    }
    policy_types = ["Egress"]
    egress {
      to {
        pod_selector {
          match_labels = {
            "app" = "dotnet-api"
          }
        }
      }
      ports {
        port     = "5116"
        protocol = "TCP"
      }
    }
    egress {
      to {
        pod_selector {
          match_labels = {
            "app" = "php-service"
          }
        }
      }
      ports {
        port     = "8000"
        protocol = "TCP"
      }
    }
    egress {
      to {
        ip_block {
          cidr = "0.0.0.0/0"
        }
      }
      ports {
        port     = "443"
        protocol = "TCP"
      }
    }
  }
}

# prometheus ingress: only from grafana on port 9090 (datasource query).
resource "kubernetes_network_policy" "allow_prometheus_ingress" {
  metadata {
    name      = "allow-prometheus-ingress"
    namespace = var.namespace
    labels    = var.labels
  }
  spec {
    pod_selector {
      match_labels = {
        "app" = "prometheus"
      }
    }
    policy_types = ["Ingress"]
    ingress {
      from {
        pod_selector {
          match_labels = {
            "app" = "grafana"
          }
        }
      }
      ports {
        port     = "9090"
        protocol = "TCP"
      }
    }
  }
}

# grafana egress: only to prometheus:9090 (datasource).
resource "kubernetes_network_policy" "allow_grafana_egress" {
  metadata {
    name      = "allow-grafana-egress"
    namespace = var.namespace
    labels    = var.labels
  }
  spec {
    pod_selector {
      match_labels = {
        "app" = "grafana"
      }
    }
    policy_types = ["Egress"]
    egress {
      to {
        pod_selector {
          match_labels = {
            "app" = "prometheus"
          }
        }
      }
      ports {
        port     = "9090"
        protocol = "TCP"
      }
    }
  }
}
