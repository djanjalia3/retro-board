# RetroBoard Home K8s Deployment Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Containerize the .NET API and the Angular SPA, deploy them to the home k8s cluster behind a TLS ingress at `retro.<domain>`, with a Postgres deployment in the same namespace and EF migrations applied as a one-shot Job per release.

**Architecture:** Two images (`retro-board-api`, `retro-board-web`). API runs as a single replica (no SignalR backplane in v1). Postgres runs in-cluster on a PVC. Migrations run via a `Job` that completes before the API Deployment becomes Ready. The ingress exposes both API and SPA on the same hostname; SPA at `/`, API at `/api/*`, hub at `/hubs/*`.

**Tech Stack:** Docker (multistage), Kubernetes, nginx (in-cluster SPA host + cluster ingress controller), cert-manager (assumed already configured), Postgres 16.

**Spec reference:** `docs/superpowers/specs/2026-04-25-retro-board-dotnet-rewrite-design.md`

**Prerequisites:**
- Plan 1 (backend) and Plan 2 (Angular client rewrite) are both complete and tested locally end-to-end.
- An existing k8s cluster reachable via `~/Desktop/kubeconfig-fixed.yaml` (or whichever kubeconfig the user uses).
- A container registry that the cluster can pull from (the user's existing `hs-*` services already use one — match it). The placeholder used in this plan is `<REGISTRY>` — replace with the actual hostname (e.g. `registry.example.lan`) at execution time.
- Cert-manager is already set up in the cluster and a `ClusterIssuer` exists (the user's other services use one — match by inspecting an existing manifest in `hs-deployment-manifests`).
- An ingress controller is installed and routes traffic for the user's domain. WebSocket support is enabled (verify by inspecting an existing service that uses WS, or by reading the ingress controller config).

---

## File Structure

```
server/
  Dockerfile                                CREATE   .NET API multistage build
  .dockerignore                             CREATE
Dockerfile.web                              CREATE   nginx + Angular dist
nginx.web.conf                              CREATE   nginx config for SPA
.dockerignore                               CREATE   client-side
deploy/
  retro-board/                              CREATE   Manifests live alongside source for v1.
                                                     If the user prefers them in hs-deployment-manifests,
                                                     move at the end.
    namespace.yaml
    postgres-secret.yaml                    template; real secret applied separately
    postgres-pvc.yaml
    postgres-deployment.yaml
    postgres-service.yaml
    api-config.yaml                         ConfigMap (non-secret config)
    api-secret.yaml                         template
    api-migrations-job.yaml
    api-deployment.yaml
    api-service.yaml
    web-deployment.yaml
    web-service.yaml
    ingress.yaml
```

---

## Task 1: Identify cluster conventions

**Goal:** Avoid guessing — read what other services in this cluster already use.

- [ ] **Step 1: Inspect an existing service's manifests**

```bash
ls /Users/davitjanjalia/Desktop/hs-deployment-manifests
```

Pick one of the existing `hs-*` apps and read its Deployment, Service, and Ingress YAML. Capture:
- Registry hostname / image-pull secret name (if any).
- Ingress controller class (`ingressClassName`) — likely `nginx` or `traefik`.
- TLS strategy: `cert-manager.io/cluster-issuer` annotation value (which `ClusterIssuer`).
- The base domain used (e.g. something like `home.lan` or `<user>.dev`).
- Whether services share a Postgres or each ships its own.

Record findings as a checklist comment in `deploy/retro-board/README.md` (create the file if helpful) — these values get substituted into manifests below.

- [ ] **Step 2: Confirm kubectl context**

```bash
export KUBECONFIG=/Users/davitjanjalia/Desktop/kubeconfig-fixed.yaml
kubectl cluster-info
kubectl get nodes
```

Expected: cluster info prints, nodes are `Ready`.

- [ ] **Step 3: No commit for this discovery task**

This task is pure investigation. The findings drive substitutions in later tasks.

---

## Task 2: API Dockerfile

**Files:**
- Create: `server/Dockerfile`
- Create: `server/.dockerignore`

- [ ] **Step 1: Write `.dockerignore`**

`server/.dockerignore`:

```
**/bin
**/obj
**/.vs
**/*.user
**/.idea
tests/
```

- [ ] **Step 2: Write Dockerfile**

`server/Dockerfile`:

```dockerfile
# syntax=docker/dockerfile:1.7
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Restore separately for better cache reuse.
COPY RetroBoard.sln ./
COPY src/RetroBoard.Domain/*.csproj src/RetroBoard.Domain/
COPY src/RetroBoard.Application/*.csproj src/RetroBoard.Application/
COPY src/RetroBoard.Infrastructure/*.csproj src/RetroBoard.Infrastructure/
COPY src/RetroBoard.Api/*.csproj src/RetroBoard.Api/
COPY tests/RetroBoard.Domain.Tests/*.csproj tests/RetroBoard.Domain.Tests/
COPY tests/RetroBoard.Application.Tests/*.csproj tests/RetroBoard.Application.Tests/
COPY tests/RetroBoard.Api.Tests/*.csproj tests/RetroBoard.Api.Tests/
RUN dotnet restore RetroBoard.sln

COPY . .
RUN dotnet publish src/RetroBoard.Api/RetroBoard.Api.csproj \
    -c Release -o /app /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app ./
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "RetroBoard.Api.dll"]
```

- [ ] **Step 3: Build the image locally**

```bash
cd /Users/davitjanjalia/Desktop/retro-board/server
docker build -t retro-board-api:dev .
```

Expected: image builds; final image size in the hundreds of MB (aspnet runtime).

- [ ] **Step 4: Smoke-run the image**

```bash
cd /Users/davitjanjalia/Desktop/retro-board
docker compose up -d postgres
docker run --rm --network host \
  -e ConnectionStrings__Postgres="Host=localhost;Port=5432;Database=retroboard;Username=retro;Password=retro" \
  -e ASPNETCORE_ENVIRONMENT=Development \
  retro-board-api:dev &
APIPID=$!
sleep 8
curl -s http://localhost:8080/api/boards | jq
kill $APIPID
```

Expected: empty array `[]` (or whatever local boards exist).

- [ ] **Step 5: Commit**

```bash
git add server/Dockerfile server/.dockerignore
git commit -m "build(server): add API Dockerfile"
```

---

## Task 3: SPA Dockerfile + nginx config

**Files:**
- Create: `Dockerfile.web` (repo root)
- Create: `nginx.web.conf` (repo root)
- Create: `.dockerignore` (repo root, if missing)

- [ ] **Step 1: Write `.dockerignore`**

If a root `.dockerignore` does not exist, create it:

```
node_modules
dist
.angular
.idea
.vscode
server/bin
server/obj
**/*.user
```

- [ ] **Step 2: Write nginx config**

`nginx.web.conf`:

```nginx
server {
    listen 80;
    server_name _;
    root /usr/share/nginx/html;
    index index.html;

    # SPA fallback
    location / {
        try_files $uri $uri/ /index.html;
    }

    # Static asset caching
    location ~* \.(?:css|js|woff2?|ttf|eot|svg|png|jpg|jpeg|gif|ico|webp)$ {
        expires 7d;
        add_header Cache-Control "public, max-age=604800, immutable";
        try_files $uri =404;
    }

    location = /index.html {
        add_header Cache-Control "no-store";
    }
}
```

- [ ] **Step 3: Write `Dockerfile.web`**

`Dockerfile.web`:

```dockerfile
# syntax=docker/dockerfile:1.7
FROM node:22-alpine AS build
WORKDIR /src
COPY package.json package-lock.json ./
RUN npm ci
COPY . .
RUN npx ng build --configuration=production

FROM nginx:alpine AS runtime
COPY nginx.web.conf /etc/nginx/conf.d/default.conf
COPY --from=build /src/dist/retro-board/browser /usr/share/nginx/html
EXPOSE 80
```

(If `ng build` writes to a different directory, adjust the `COPY --from=build` line. Check existing `firebase.json` history or `angular.json` `outputPath` to confirm; default for `ng build` in Angular 21 is `dist/<project-name>/browser`.)

- [ ] **Step 4: Build the image locally**

```bash
cd /Users/davitjanjalia/Desktop/retro-board
docker build -f Dockerfile.web -t retro-board-web:dev .
```

Expected: build succeeds.

- [ ] **Step 5: Run and smoke-test**

```bash
docker run --rm -d -p 8081:80 --name retroweb retro-board-web:dev
sleep 2
curl -sI http://localhost:8081/ | head -1
docker rm -f retroweb
```

Expected: `HTTP/1.1 200 OK`.

- [ ] **Step 6: Commit**

```bash
git add Dockerfile.web nginx.web.conf .dockerignore
git commit -m "build(client): add SPA Dockerfile and nginx config"
```

---

## Task 4: Push images to the registry

**Files:** none (operational step).

- [ ] **Step 1: Tag and push the API image**

Replace `<REGISTRY>` with the hostname identified in Task 1 (e.g. `registry.example.lan`). Use a real version tag like `0.1.0` or the short commit SHA.

```bash
cd /Users/davitjanjalia/Desktop/retro-board
TAG=$(git rev-parse --short HEAD)
docker tag retro-board-api:dev <REGISTRY>/retro-board-api:$TAG
docker tag retro-board-api:dev <REGISTRY>/retro-board-api:latest
docker push <REGISTRY>/retro-board-api:$TAG
docker push <REGISTRY>/retro-board-api:latest
```

- [ ] **Step 2: Tag and push the web image**

```bash
docker tag retro-board-web:dev <REGISTRY>/retro-board-web:$TAG
docker tag retro-board-web:dev <REGISTRY>/retro-board-web:latest
docker push <REGISTRY>/retro-board-web:$TAG
docker push <REGISTRY>/retro-board-web:latest
```

- [ ] **Step 3: Verify pushes**

```bash
# Cluster-side: try pulling on a node, or list via the registry's UI/API.
# Adapt to whatever tooling the user has for the registry.
echo "Pushed retro-board-api:$TAG and retro-board-web:$TAG to <REGISTRY>"
```

No git commit — image push is operational.

---

## Task 5: Namespace and Postgres manifests

**Files:**
- Create: `deploy/retro-board/namespace.yaml`
- Create: `deploy/retro-board/postgres-secret.yaml`
- Create: `deploy/retro-board/postgres-pvc.yaml`
- Create: `deploy/retro-board/postgres-deployment.yaml`
- Create: `deploy/retro-board/postgres-service.yaml`

- [ ] **Step 1: Write namespace**

`deploy/retro-board/namespace.yaml`:

```yaml
apiVersion: v1
kind: Namespace
metadata:
  name: retro-board
```

- [ ] **Step 2: Write Postgres secret template**

> Do not commit real credentials. This file is a template; the live Secret is applied separately (see Step 7).

`deploy/retro-board/postgres-secret.yaml`:

```yaml
apiVersion: v1
kind: Secret
metadata:
  name: postgres-credentials
  namespace: retro-board
type: Opaque
stringData:
  POSTGRES_USER: retro
  POSTGRES_PASSWORD: REPLACE_ME
  POSTGRES_DB: retroboard
```

- [ ] **Step 3: Write PVC**

`deploy/retro-board/postgres-pvc.yaml`:

```yaml
apiVersion: v1
kind: PersistentVolumeClaim
metadata:
  name: postgres-data
  namespace: retro-board
spec:
  accessModes: [ReadWriteOnce]
  resources:
    requests:
      storage: 5Gi
```

(If the cluster requires a specific `storageClassName`, add it. Inspect another deployment in `hs-deployment-manifests` for the right value.)

- [ ] **Step 4: Write Postgres Deployment**

`deploy/retro-board/postgres-deployment.yaml`:

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: postgres
  namespace: retro-board
spec:
  replicas: 1
  strategy:
    type: Recreate
  selector:
    matchLabels:
      app: postgres
  template:
    metadata:
      labels:
        app: postgres
    spec:
      containers:
        - name: postgres
          image: postgres:16
          ports:
            - containerPort: 5432
          envFrom:
            - secretRef:
                name: postgres-credentials
          volumeMounts:
            - name: data
              mountPath: /var/lib/postgresql/data
          readinessProbe:
            exec:
              command: [pg_isready, -U, retro, -d, retroboard]
            initialDelaySeconds: 5
            periodSeconds: 5
          livenessProbe:
            exec:
              command: [pg_isready, -U, retro, -d, retroboard]
            initialDelaySeconds: 30
            periodSeconds: 30
      volumes:
        - name: data
          persistentVolumeClaim:
            claimName: postgres-data
```

- [ ] **Step 5: Write Postgres Service**

`deploy/retro-board/postgres-service.yaml`:

```yaml
apiVersion: v1
kind: Service
metadata:
  name: postgres
  namespace: retro-board
spec:
  selector:
    app: postgres
  ports:
    - port: 5432
      targetPort: 5432
```

- [ ] **Step 6: Apply namespace + Postgres**

```bash
export KUBECONFIG=/Users/davitjanjalia/Desktop/kubeconfig-fixed.yaml
kubectl apply -f deploy/retro-board/namespace.yaml
# Apply secret with a real password (do not commit). Use one of:
#   (a) edit a copy of postgres-secret.yaml with the real password and apply,
#   (b) imperative kubectl create secret:
kubectl -n retro-board create secret generic postgres-credentials \
  --from-literal=POSTGRES_USER=retro \
  --from-literal=POSTGRES_PASSWORD="$(openssl rand -hex 16)" \
  --from-literal=POSTGRES_DB=retroboard
kubectl apply -f deploy/retro-board/postgres-pvc.yaml
kubectl apply -f deploy/retro-board/postgres-deployment.yaml
kubectl apply -f deploy/retro-board/postgres-service.yaml
```

- [ ] **Step 7: Wait for Postgres ready**

```bash
kubectl -n retro-board rollout status deploy/postgres
kubectl -n retro-board exec deploy/postgres -- pg_isready -U retro -d retroboard
```

Expected: `accepting connections`.

- [ ] **Step 8: Commit manifests (template only — real secret stays out of git)**

```bash
git add deploy/retro-board/namespace.yaml \
        deploy/retro-board/postgres-secret.yaml \
        deploy/retro-board/postgres-pvc.yaml \
        deploy/retro-board/postgres-deployment.yaml \
        deploy/retro-board/postgres-service.yaml
git commit -m "deploy(retro-board): namespace + Postgres manifests"
```

---

## Task 6: API config, secret, migrations Job, Deployment, Service

**Files:**
- Create: `deploy/retro-board/api-config.yaml`
- Create: `deploy/retro-board/api-secret.yaml`
- Create: `deploy/retro-board/api-migrations-job.yaml`
- Create: `deploy/retro-board/api-deployment.yaml`
- Create: `deploy/retro-board/api-service.yaml`

- [ ] **Step 1: Write ConfigMap (non-secret API settings)**

`deploy/retro-board/api-config.yaml`:

```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: api-config
  namespace: retro-board
data:
  ASPNETCORE_ENVIRONMENT: Production
  ASPNETCORE_URLS: http://0.0.0.0:8080
```

- [ ] **Step 2: Write API secret template**

`deploy/retro-board/api-secret.yaml`:

```yaml
apiVersion: v1
kind: Secret
metadata:
  name: api-secrets
  namespace: retro-board
type: Opaque
stringData:
  ConnectionStrings__Postgres: "Host=postgres;Port=5432;Database=retroboard;Username=retro;Password=REPLACE_ME"
```

- [ ] **Step 3: Write migrations Job**

> The Job runs `dotnet RetroBoard.Api.dll migrate-and-exit`-style behavior is not built in; instead we shell out to `dotnet ef database update`. The simplest approach is to add a `--migrate` startup mode to the API. To avoid plan bloat, we use `Microsoft.EntityFrameworkCore.Tools` baked into the image is overkill — instead leverage `db.Database.MigrateAsync()` already wired into Development. For Production, add a one-line opt-in in `Program.cs`:

Modify `server/src/RetroBoard.Api/Program.cs` to also migrate when env var `RUN_MIGRATIONS=1` is set, before serving traffic:

Find the `if (app.Environment.IsDevelopment())` block in `Program.cs` and replace it with:

```csharp
if (app.Environment.IsDevelopment() ||
    string.Equals(Environment.GetEnvironmentVariable("RUN_MIGRATIONS"), "1", StringComparison.Ordinal))
{
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<RetroBoard.Infrastructure.Persistence.BoardDbContext>();
    await db.Database.MigrateAsync();
    if (string.Equals(Environment.GetEnvironmentVariable("MIGRATIONS_ONLY"), "1", StringComparison.Ordinal))
    {
        return;
    }
}
```

Now the same image can run as a migration job (`RUN_MIGRATIONS=1 MIGRATIONS_ONLY=1`) or as the main API (no env vars).

Rebuild and re-push the API image (Tasks 2–4) before continuing. Bump the tag.

`deploy/retro-board/api-migrations-job.yaml`:

```yaml
apiVersion: batch/v1
kind: Job
metadata:
  name: api-migrate
  namespace: retro-board
spec:
  backoffLimit: 2
  ttlSecondsAfterFinished: 600
  template:
    spec:
      restartPolicy: OnFailure
      containers:
        - name: migrate
          image: <REGISTRY>/retro-board-api:<TAG>
          env:
            - name: RUN_MIGRATIONS
              value: "1"
            - name: MIGRATIONS_ONLY
              value: "1"
          envFrom:
            - configMapRef:
                name: api-config
            - secretRef:
                name: api-secrets
```

- [ ] **Step 4: Write API Deployment**

`deploy/retro-board/api-deployment.yaml`:

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: api
  namespace: retro-board
spec:
  replicas: 1
  strategy:
    type: Recreate   # Single replica with sticky SignalR connections; avoid rollout overlap.
  selector:
    matchLabels:
      app: api
  template:
    metadata:
      labels:
        app: api
    spec:
      containers:
        - name: api
          image: <REGISTRY>/retro-board-api:<TAG>
          ports:
            - containerPort: 8080
          envFrom:
            - configMapRef:
                name: api-config
            - secretRef:
                name: api-secrets
          readinessProbe:
            httpGet:
              path: /api/boards
              port: 8080
            initialDelaySeconds: 5
            periodSeconds: 10
          livenessProbe:
            httpGet:
              path: /api/boards
              port: 8080
            initialDelaySeconds: 30
            periodSeconds: 30
          resources:
            requests:
              cpu: 100m
              memory: 256Mi
            limits:
              cpu: 1
              memory: 1Gi
```

- [ ] **Step 5: Write API Service**

`deploy/retro-board/api-service.yaml`:

```yaml
apiVersion: v1
kind: Service
metadata:
  name: api
  namespace: retro-board
spec:
  selector:
    app: api
  ports:
    - name: http
      port: 80
      targetPort: 8080
```

- [ ] **Step 6: Apply secret, ConfigMap, run migrations Job**

```bash
kubectl -n retro-board create secret generic api-secrets \
  --from-literal=ConnectionStrings__Postgres="Host=postgres;Port=5432;Database=retroboard;Username=retro;Password=$(kubectl -n retro-board get secret postgres-credentials -o jsonpath='{.data.POSTGRES_PASSWORD}' | base64 -d)"
kubectl apply -f deploy/retro-board/api-config.yaml

# Substitute <REGISTRY> and <TAG> in the Job manifest before applying. Example using sed inline:
sed -e 's#<REGISTRY>#registry.example.lan#g' -e 's#<TAG>#0.1.0#g' \
  deploy/retro-board/api-migrations-job.yaml | kubectl apply -f -
kubectl -n retro-board wait --for=condition=complete --timeout=120s job/api-migrate
kubectl -n retro-board logs job/api-migrate
```

Expected: `Done.` or `Applied migration ...` log entries; job completes without error.

- [ ] **Step 7: Apply API Deployment + Service**

```bash
sed -e 's#<REGISTRY>#registry.example.lan#g' -e 's#<TAG>#0.1.0#g' \
  deploy/retro-board/api-deployment.yaml | kubectl apply -f -
kubectl apply -f deploy/retro-board/api-service.yaml
kubectl -n retro-board rollout status deploy/api
kubectl -n retro-board port-forward svc/api 8080:80 &
PF=$!
sleep 3
curl -s http://localhost:8080/api/boards | jq
kill $PF
```

Expected: empty array (fresh DB).

- [ ] **Step 8: Commit (manifests; templates only)**

```bash
git add deploy/retro-board/api-config.yaml \
        deploy/retro-board/api-secret.yaml \
        deploy/retro-board/api-migrations-job.yaml \
        deploy/retro-board/api-deployment.yaml \
        deploy/retro-board/api-service.yaml \
        server/src/RetroBoard.Api/Program.cs
git commit -m "deploy(retro-board): API ConfigMap, migrations Job, Deployment, Service"
```

---

## Task 7: Web (SPA) Deployment + Service

**Files:**
- Create: `deploy/retro-board/web-deployment.yaml`
- Create: `deploy/retro-board/web-service.yaml`

- [ ] **Step 1: Write web Deployment**

`deploy/retro-board/web-deployment.yaml`:

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: web
  namespace: retro-board
spec:
  replicas: 2
  selector:
    matchLabels:
      app: web
  template:
    metadata:
      labels:
        app: web
    spec:
      containers:
        - name: web
          image: <REGISTRY>/retro-board-web:<TAG>
          ports:
            - containerPort: 80
          readinessProbe:
            httpGet:
              path: /
              port: 80
            initialDelaySeconds: 3
            periodSeconds: 10
          resources:
            requests:
              cpu: 20m
              memory: 32Mi
            limits:
              cpu: 200m
              memory: 128Mi
```

- [ ] **Step 2: Write web Service**

`deploy/retro-board/web-service.yaml`:

```yaml
apiVersion: v1
kind: Service
metadata:
  name: web
  namespace: retro-board
spec:
  selector:
    app: web
  ports:
    - name: http
      port: 80
      targetPort: 80
```

- [ ] **Step 3: Apply**

```bash
sed -e 's#<REGISTRY>#registry.example.lan#g' -e 's#<TAG>#0.1.0#g' \
  deploy/retro-board/web-deployment.yaml | kubectl apply -f -
kubectl apply -f deploy/retro-board/web-service.yaml
kubectl -n retro-board rollout status deploy/web
```

- [ ] **Step 4: Commit**

```bash
git add deploy/retro-board/web-deployment.yaml deploy/retro-board/web-service.yaml
git commit -m "deploy(retro-board): web Deployment and Service"
```

---

## Task 8: Ingress with TLS and WebSocket support

**Files:**
- Create: `deploy/retro-board/ingress.yaml`

> Substitute the actual hostname (e.g. `retro.home.lan`), the actual `ingressClassName`, and the actual `cert-manager.io/cluster-issuer` from Task 1. The example below uses placeholders.

- [ ] **Step 1: Write Ingress**

`deploy/retro-board/ingress.yaml`:

```yaml
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: retro-board
  namespace: retro-board
  annotations:
    cert-manager.io/cluster-issuer: <CLUSTER_ISSUER>
    # If using ingress-nginx, longer timeouts help SignalR long-poll fallback.
    nginx.ingress.kubernetes.io/proxy-read-timeout: "3600"
    nginx.ingress.kubernetes.io/proxy-send-timeout: "3600"
spec:
  ingressClassName: <INGRESS_CLASS>
  tls:
    - hosts:
        - retro.<DOMAIN>
      secretName: retro-board-tls
  rules:
    - host: retro.<DOMAIN>
      http:
        paths:
          - path: /api
            pathType: Prefix
            backend:
              service:
                name: api
                port:
                  number: 80
          - path: /hubs
            pathType: Prefix
            backend:
              service:
                name: api
                port:
                  number: 80
          - path: /
            pathType: Prefix
            backend:
              service:
                name: web
                port:
                  number: 80
```

- [ ] **Step 2: Apply**

```bash
sed -e 's#<CLUSTER_ISSUER>#letsencrypt-prod#g' \
    -e 's#<INGRESS_CLASS>#nginx#g' \
    -e 's#<DOMAIN>#example.lan#g' \
  deploy/retro-board/ingress.yaml | kubectl apply -f -
kubectl -n retro-board describe ingress retro-board
```

Expected: ingress object created; cert-manager begins issuing/looking up the TLS cert.

- [ ] **Step 3: Wait for cert**

```bash
# If cert-manager creates a Certificate resource, wait until Ready.
kubectl -n retro-board get certificate
# Or rely on the Secret appearing:
until kubectl -n retro-board get secret retro-board-tls >/dev/null 2>&1; do sleep 5; done
```

- [ ] **Step 4: DNS**

Make sure DNS for `retro.<DOMAIN>` resolves to the ingress controller's external/load-balancer IP. (Likely already wired up if other services share the same domain. Otherwise add a record in the user's DNS — out of scope for this plan.)

- [ ] **Step 5: Smoke test from a workstation**

```bash
curl -sI https://retro.<DOMAIN>/ | head -1
curl -s https://retro.<DOMAIN>/api/boards | jq
```

Expected: `HTTP/2 200` for the SPA index, `[]` (or boards) from the API.

- [ ] **Step 6: Open in a browser, test full UX**

Browse to `https://retro.<DOMAIN>`, repeat the manual checklist from Plan 2 Task 7 against the deployed instance:
- Create a board.
- Open the same board in two browsers.
- Add card → seen in both.
- Vote → seen in both, idempotent per session.
- Delete → seen in both.
- Refresh → state restored.
- Browser DevTools network tab confirms WebSocket connection (or long-polling fallback) to `/hubs/board`.

- [ ] **Step 7: Commit**

```bash
git add deploy/retro-board/ingress.yaml
git commit -m "deploy(retro-board): ingress with TLS and WS-friendly timeouts"
```

---

## Task 9: Release script (optional but recommended)

**Files:**
- Create: `deploy/retro-board/release.sh`

This script bundles build, push, migration, and rollout into a single command for repeat releases.

- [ ] **Step 1: Write the script**

`deploy/retro-board/release.sh`:

```bash
#!/usr/bin/env bash
set -euo pipefail

REGISTRY="${REGISTRY:?set REGISTRY (e.g. registry.example.lan)}"
TAG="${1:-$(git rev-parse --short HEAD)}"
NS=retro-board
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"

echo ">> Building images @ $TAG"
docker build -t "$REGISTRY/retro-board-api:$TAG" -f "$ROOT/server/Dockerfile" "$ROOT/server"
docker build -t "$REGISTRY/retro-board-web:$TAG" -f "$ROOT/Dockerfile.web" "$ROOT"

echo ">> Pushing"
docker push "$REGISTRY/retro-board-api:$TAG"
docker push "$REGISTRY/retro-board-web:$TAG"

echo ">> Running migrations"
kubectl -n "$NS" delete job/api-migrate --ignore-not-found
sed -e "s#<REGISTRY>#$REGISTRY#g" -e "s#<TAG>#$TAG#g" \
  "$ROOT/deploy/retro-board/api-migrations-job.yaml" | kubectl apply -f -
kubectl -n "$NS" wait --for=condition=complete --timeout=180s job/api-migrate

echo ">> Rolling out api"
sed -e "s#<REGISTRY>#$REGISTRY#g" -e "s#<TAG>#$TAG#g" \
  "$ROOT/deploy/retro-board/api-deployment.yaml" | kubectl apply -f -
kubectl -n "$NS" rollout status deploy/api

echo ">> Rolling out web"
sed -e "s#<REGISTRY>#$REGISTRY#g" -e "s#<TAG>#$TAG#g" \
  "$ROOT/deploy/retro-board/web-deployment.yaml" | kubectl apply -f -
kubectl -n "$NS" rollout status deploy/web

echo ">> Done. https://retro.<DOMAIN> should be live."
```

- [ ] **Step 2: Make it executable and commit**

```bash
chmod +x deploy/retro-board/release.sh
git add deploy/retro-board/release.sh
git commit -m "deploy(retro-board): add release script"
```

- [ ] **Step 3: Smoke-run it once**

```bash
cd /Users/davitjanjalia/Desktop/retro-board
REGISTRY=registry.example.lan deploy/retro-board/release.sh 0.1.1
```

Expected: build → push → migrate → rollout completes; the deployed site shows the new build.

---

## Task 10: Optional — relocate manifests to `hs-deployment-manifests`

**When to do this:** if/when the user prefers all cluster manifests in their central repo. Skip otherwise.

- [ ] **Step 1: Move and adjust**

```bash
mkdir -p /Users/davitjanjalia/Desktop/hs-deployment-manifests/retro-board
git mv deploy/retro-board/* /Users/davitjanjalia/Desktop/hs-deployment-manifests/retro-board/
# (or copy if hs-deployment-manifests is a separate repo)
```

Update the release script's `ROOT` and manifest paths accordingly.

- [ ] **Step 2: Commit in both repos**

```bash
git -C /Users/davitjanjalia/Desktop/retro-board commit -am "deploy: move manifests to hs-deployment-manifests"
git -C /Users/davitjanjalia/Desktop/hs-deployment-manifests add retro-board
git -C /Users/davitjanjalia/Desktop/hs-deployment-manifests commit -m "feat(retro-board): import manifests"
```

---

## Done state

- `https://retro.<DOMAIN>` serves the new SPA.
- API and SignalR hub live at the same hostname under `/api/*` and `/hubs/*`.
- Postgres persists data across pod restarts via the PVC.
- `release.sh` rebuilds, pushes, migrates, and rolls out in one command.
- The Firebase-hosted site continues to run unchanged at its previous URL — until the user decides to retire it (out of scope for this plan).
