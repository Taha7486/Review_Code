# Network Policies Module

Terraform owns all `NetworkPolicy` resources in the `codereview` namespace.
Applied in a single `terraform apply` so that default-deny and DNS-allow are
never separated by a time window.

## Allowed traffic matrix (source of truth)

| Policy | Direction | From | To | Port |
|--------|-----------|------|----|------|
| allow-dns-egress | Egress | all pods | kube-system/k8s-app=kube-dns | 53 UDP+TCP |
| allow-react-app-ingress | Ingress | any (NodePort) | app=react-app | 80 TCP |
| allow-react-app-egress | Egress | app=react-app | app=dotnet-api | 5116 TCP |
| allow-dotnet-api-ingress | Ingress | app=react-app, app=prometheus | app=dotnet-api | 5116 TCP |
| allow-dotnet-api-egress | Egress | app=dotnet-api | app=mysql | 3306 TCP |
| allow-dotnet-api-egress | Egress | app=dotnet-api | app=php-service | 8000 TCP |
| allow-dotnet-api-egress | Egress | app=dotnet-api | 0.0.0.0/0 (excl. RFC-1918) | 443 TCP |
| allow-mysql-ingress | Ingress | app=dotnet-api | app=mysql | 3306 TCP |
| allow-php-service-ingress | Ingress | app=dotnet-api, app=prometheus | app=php-service | 8000 TCP |
| allow-prometheus-egress | Egress | app=prometheus | app=dotnet-api | 5116 TCP |
| allow-prometheus-egress | Egress | app=prometheus | app=php-service | 8000 TCP |
| allow-prometheus-egress | Egress | app=prometheus | 0.0.0.0/0 | 443 TCP (k8s SD) |
| allow-prometheus-ingress | Ingress | app=grafana | app=prometheus | 9090 TCP |
| allow-grafana-egress | Egress | app=grafana | app=prometheus | 9090 TCP |

## Explicitly denied paths

- php-service → dotnet-api (any direction) — no path in matrix
- php-service → mysql — no path in matrix
- react-app → php-service — no path in matrix
- External → dotnet-api:5116 (NodePort 30116) — blocked; use react-app UI
- External → prometheus:9090 (NodePort 30090) — blocked; internal monitoring only
- External → grafana:3000 (NodePort 30001) — blocked; internal monitoring only

## VSO / Vault note

VSO operator runs in the `vault` namespace and connects to Vault in the same
namespace. No cross-namespace policy is needed in `codereview` for VSO traffic.
App pods consume VSO-synced K8s Secrets and do NOT contact Vault directly.

## Kubelet health probes

`livenessProbe` / `readinessProbe` traffic originates from the node (kubelet),
not from a pod. Most CNIs (including Calico on kind) exempt kubelet probe
traffic from NetworkPolicy enforcement. Probes are verified to work correctly
after policy application — see phase3_imp.md Step 3.7.
