# 🎭 GitOps Demo Script

Use this script to showcase the power of the GitOps workflow implemented in this project.

## 1. Setup & Baseline
- **Show ArgoCD**: Open `http://localhost:8080`. Point out that all services are `Synced` and `Healthy`.
- **Show App**: Open `http://localhost:3000`. Perform one code analysis to show the system is working.
- **Show Monitoring**: Open `http://localhost:3001`. Show the "Code Review" dashboard with live data.

## 2. Zero-Downtime Scaling
- **Action**: Edit `k8s/base/react-app.yaml` and change `replicas: 2` to `replicas: 4`.
- **Git**: `git add . && git commit -m "scale: increase frontend capacity" && git push origin main`.
- **Observation**:
    - Watch ArgoCD detect the change (or click Refresh).
    - Show the new pods spinning up in the UI.
    - Run `kubectl get pods` to show the rolling update in progress.

## 3. Self-Healing
- **Action**: Manually delete a pod: `kubectl delete pod -l app=php-service`.
- **Observation**:
    - Show ArgoCD UI immediately detecting the missing pod.
    - Watch Kubernetes (instructed by ArgoCD) recreate the pod instantly.
    - **Key takeaway**: "The cluster maintains its desired state even if components fail."

## 4. Configuration Update (Zero Downtime)
- **Action**: Edit `k8s/base/dotnet-api.yaml`. Change `ALLOWED_ORIGINS` in the ConfigMap.
- **Git**: Commit and push.
- **Observation**:
    - Show ArgoCD performing a **Rolling Update**.
    - Explain that K8s starts new pods with the new config before killing the old ones, ensuring zero downtime.

## 5. Rollback (The "Panic" Recovery)
- **Action**: Introduce a "breaking" change. Edit `k8s/base/php-service.yaml` and add a broken `command`:
  `command: ["/bin/sh", "-c", "exit 1"]`
- **Git**: Commit and push.
- **Observation**:
    - Show the PHP pods entering `CrashLoopBackOff`.
    - Show ArgoCD status turning **Red (Degraded)**.
- **The Fix**: `git revert HEAD --no-edit && git push origin main`.
- **Observation**:
    - Watch ArgoCD pull the revert and restore the cluster to a healthy state in seconds.
    - **Key takeaway**: "Git history is our audit trail and our recovery mechanism."
