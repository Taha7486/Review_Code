terraform {
  required_version = ">= 1.6.0"

  required_providers {
    kubernetes = {
      source  = "hashicorp/kubernetes"
      version = "~> 2.38"
    }
    helm = {
      source  = "hashicorp/helm"
      version = "~> 2.17"
    }
  }
}

provider "kubernetes" {
  config_path    = var.kubeconfig_path
  config_context = var.kube_context
}

provider "helm" {
  kubernetes {
    config_path    = var.kubeconfig_path
    config_context = var.kube_context
  }
}

module "namespaces" {
  source = "../../modules/namespace"

  for_each = toset(["codereview", "vault"])

  name = each.key
  labels = {
    "app.kubernetes.io/name"       = each.key
    "app.kubernetes.io/managed-by" = "terraform"
    "app.kubernetes.io/part-of"    = each.key == "codereview" ? "codereview" : "platform"
  }
}

module "service_accounts" {
  source = "../../modules/service-accounts"

  namespace = module.namespaces["codereview"].name
  service_accounts = {
    sa-react-app = {
      component = "frontend"
    }
    sa-dotnet-api = {
      component = "api"
    }
    sa-php-service = {
      component = "analyzer"
    }
    sa-mysql = {
      component = "database"
    }
    sa-grafana = {
      component = "monitoring"
    }
    prometheus = {
      component = "monitoring"
    }
  }
}

module "prometheus_rbac" {
  source = "../../modules/rbac/prometheus"

  namespace            = module.namespaces["codereview"].name
  service_account_name = module.service_accounts.names["prometheus"]
}

module "vault" {
  source       = "../../modules/vault"
  namespace    = module.namespaces["vault"].name
  ui_node_port = 30200
}

# Vault policies and auth roles are configured by configure-vault.sh (vault CLI)
# after vault operator init + unseal. The Vault Terraform provider cannot be used
# here because it validates its token against the Vault API at plan time — before
# the Vault pod exists. See scripts/configure-vault.sh and terraform/README.md.

module "vault_secrets_operator" {
  source                = "../../modules/vault-secrets-operator"
  namespace             = module.namespaces["vault"].name
  app_namespace         = module.namespaces["codereview"].name
  vault_address         = "http://vault.vault.svc.cluster.local:8200"
  vault_connection_name = "vault-connection"

  vault_auths = {
    "dotnet-api-vault-auth" = {
      role            = "dotnet-api-role"
      service_account = "sa-dotnet-api"
    }
    "grafana-vault-auth" = {
      role            = "grafana-role"
      service_account = "sa-grafana"
    }
  }

  depends_on = [module.vault]
}

module "network_policies" {
  source    = "../../modules/network-policies"
  namespace = module.namespaces["codereview"].name

  depends_on = [module.namespaces]
}
