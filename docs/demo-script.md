# GitOps Demo Script

A walkthrough to showcase the GitOps workflow, Kubernetes self-healing, and the CI/CD pipeline.

---

## Prerequisites

Before starting the demo, verify the following are in place:

- Kind cluster is running and all pods are healthy:
  ```powershell
  kubectl get pods -n codereview   # all 10 pods should be 1/1 Running
  ```
- A GitHub PAT is configured in Vault and at least one code analysis has been run through the UI (so Grafana has data to show).
- You are logged in to GitHub — the demo pushes to `main`, which triggers CI (~3–5 min per push).
- Tabs pre-opened in the browser: ArgoCD (https://localhost:8080), React UI (http://localhost:3000), Grafana (http://localhost:3001), GitHub Actions (https://github.com/Taha7486/Review_Code/actions).

---

## 1. Baseline walkthrough (~3 min)

Open each tab and briefly describe what you're looking at:

**ArgoCD (https://localhost:8080)**
- Application `codereview-app` is `Synced` and `Healthy`.
- Explain: "ArgoCD watches the `deploy` branch. The cluster state you see here is defined entirely in Git."

**React UI (http://localhost:3000)**
- Trigger one code analysis on a public GitHub repo.
- Explain: "The UI calls the .NET API, which fetches code from GitHub and sends it to the PHP analyzer. Results are stored in MySQL and rendered here."

**Grafana (http://localhost:3001)**
- Show the "Code Review" dashboard — analyses count, files analyzed, issues breakdown, average duration.

---

## 2. Zero-Downtime Scaling via GitOps (~8 min including CI)

**What to say:** "I don't touch the cluster directly — I change Git. The pipeline does the rest."

**Action:**
1. Edit `k8s/base/react-app.yaml` and change `replicas: 2` to `replicas: 4`.
2. Push:
   ```bash
   git add k8s/base/react-app.yaml
   git commit -m "scale: increase frontend to 4 replicas for demo"
   git push origin main
   ```

**While CI runs (GitHub Actions tab, ~3–5 min):**
- Show the pipeline stages: test → Trivy scan → build & push → update deploy branch.
- "The deploy branch is the only branch ArgoCD reads. CI resets it to match `main` and updates the image tags."

**After CI completes (ArgoCD tab):**
- Click Refresh or wait for the auto-sync poll (~30 s).
- Watch the rolling update: 2 old pods terminate, 4 new ones start.
- ```powershell
  kubectl get pods -n codereview -l app=react-app -w
  ```
- "Zero downtime: Kubernetes starts the new pods and passes health checks before terminating the old ones."

**Cleanup (push to main to revert):**
```bash
git revert HEAD --no-edit && git push origin main
```

---

## 3. Self-Healing (~2 min)

**What to say:** "ArgoCD's `selfHeal` flag means if someone manually changes the cluster state, ArgoCD reverts it automatically."

**Action:**
```powershell
kubectl scale deployment/php-service -n codereview --replicas=0
```

**Observation (ArgoCD tab):**
- The app immediately shows `OutOfSync` — the cluster has 0 php-service replicas but the deploy branch specifies 2.
- Within ~5–10 seconds, ArgoCD detects the diff and syncs: the php-service pods come back to 2.
- ```powershell
  kubectl get pods -n codereview -l app=php-service -w
  ```

**Key takeaway:** "You cannot permanently break this cluster through `kubectl`. ArgoCD will always reconcile back to what Git says."

---

## 4. Configuration Update — Zero Downtime (~8 min including CI)

**Action:**
1. Edit `k8s/base/react-app.yaml` — update `REACT_APP_API_URL` or any ConfigMap value.
2. Push:
   ```bash
   git add k8s/base/react-app.yaml
   git commit -m "config: update API URL for demo"
   git push origin main
   ```

**After CI + ArgoCD sync:**
- ArgoCD performs a **rolling update**: new pods with the new ConfigMap start up, old pods terminate only after the new ones are healthy.
- Show pods being replaced one at a time in `kubectl get pods -n codereview -w`.

**Cleanup:**
```bash
git revert HEAD --no-edit && git push origin main
```

---

## 5. Rollback — The "Panic" Recovery (~10 min including 2× CI)

**What to say:** "Git history is our audit trail and our recovery mechanism."

**Action — introduce a breaking change:**
1. Edit `k8s/base/php-service.yaml`. In the `containers` spec, add:
   ```yaml
   command: ["/bin/sh", "-c", "exit 1"]
   ```
2. Push:
   ```bash
   git add k8s/base/php-service.yaml
   git commit -m "demo: intentionally break php-service"
   git push origin main
   ```

**After CI + ArgoCD sync:**
- Show php-service pods entering `CrashLoopBackOff`:
  ```powershell
  kubectl get pods -n codereview -l app=php-service
  ```
- ArgoCD shows the app `Degraded`.
- The React UI still loads (react-app and dotnet-api are unaffected), but analysis requests will fail.

**The fix — one command:**
```bash
git revert HEAD --no-edit && git push origin main
```

**After the second CI run + ArgoCD sync:**
- php-service pods return to `Running`.
- ArgoCD returns to `Healthy`.
- "We didn't SSH into anything. We didn't run kubectl rollout. We just reverted a Git commit and the platform healed itself."

---

## Demo notes

- **CI pipeline duration:** ~3–5 min per push. Use the wait time to narrate what each CI stage does (tests, Trivy scan, image build, deploy branch update).
- **If a CI run fails:** The deploy branch is not updated, so the cluster is unaffected. Show this as a feature: "Bad code cannot reach the cluster — CI is the gate."
- **ArgoCD sync interval:** Default is ~3 min auto-poll. Click **Refresh** in the UI for an immediate check after CI updates the deploy branch.
