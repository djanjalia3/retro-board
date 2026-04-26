# Deployment

How code reaches **https://retroboard.win**.

**Status:** Active since 2026-04-26. Pushes to `master` that pass CI auto-deploy.

## Topology

Production runs on a self-hosted Ubuntu mini PC (`drascula`) on a home network, exposed publicly via a Cloudflare Tunnel. No outer auth wall — anyone with a board slug can join. The previous Firebase-hosted retro board was retired the same day; the apex `retroboard.win` was repointed from Firebase to the new tunnel.

```
GitHub  ──CI──▶ tests pass ──┐
                              ▼
              .github/workflows/deploy.yml
              (Tailscale + SSH to drascula)
                              │
                              ▼
            ~/docker/retro-board/  (server)
              git pull
              docker compose -f docker-compose.prod.yml up -d --build
              health probe → localhost:8081
                              │
                              ▼
            cloudflared ── tunnel ── Cloudflare ── User
```

The runner connects to drascula over Tailscale (no inbound port-forwarding on the home router) and runs the deploy commands over SSH. cloudflared on drascula maintains four outbound QUIC connections to the Cloudflare edge; public traffic for `retroboard.win` is steered into those connections.

## Stack on drascula

Four containers, defined in `docker-compose.prod.yml`:

| Service | Image | Role |
|---|---|---|
| `db` | `postgres:16-alpine` | Persistent state. Healthchecked via `pg_isready`. Data in named volume `retro-board_db-data`. No host port. |
| `api` | built from `./server` | ASP.NET Core 9 + SignalR. Runs EF Core migrations on every startup. Listens on `:8080` internally. |
| `web` | built from `./Dockerfile.web` | nginx serving the Angular production bundle. Reverse-proxies `/api/` and `/hubs/` to `api:8080` (Origin stripped, WebSocket upgrade headers passed). Maps host port `${WEB_PORT:-8081}` → container `:80`. |
| `cloudflared` | `cloudflare/cloudflared:latest` | Outbound-only tunnel connector. Runs the `retro-board` named tunnel via `CLOUDFLARED_TOKEN`. |

## Pipelines

Two GitHub Actions workflows under `.github/workflows/`:

| Workflow | Triggers | What it does |
|---|---|---|
| `ci.yml` | push/PR to `master` | .NET build + `dotnet test` in `server/` (Testcontainers boots its own Postgres in the runner) and `npx ng build --configuration=production` at repo root |
| `deploy.yml` | CI succeeds on `master`, **or** manual `workflow_dispatch` | Connects to drascula, pulls, rebuilds containers, health-checks |

Deploy is **never** triggered by a CI failure. The `workflow_run` filter only fires when CI's `conclusion == 'success'`. Manual dispatch is a panic button — it skips CI entirely, so use it for things like rolling back via `git revert` and pushing a hotfix you've already vetted.

A `concurrency: deploy-prod` group prevents two deploys from running at once. New deploys queue behind in-flight ones; they don't cancel each other.

## What's currently configured

Use this as the source-of-truth checklist when re-bootstrapping.

| Where | What | Notes |
|---|---|---|
| GitHub repo secrets (`djanjalia3/retro-board`) | `DEPLOY_SSH_KEY` | Private half of an Ed25519 keypair. Created 2026-04-26. Distinct from the family-finance deploy key. |
| GitHub repo secrets | `TS_OAUTH_CLIENT_ID` / `TS_OAUTH_SECRET` | Tailscale OAuth client `github-actions-retro-board-deploy`. Distinct from the family-finance OAuth client. |
| Tailscale admin | OAuth client `github-actions-retro-board-deploy` | Scope: **Auth Keys (Write)**. |
| Tailscale ACL | `tag:ci` ownership and `drascula:22` access | Already configured for family-finance; reused. See ACL block below. |
| drascula | Public key in `~alucsard/.ssh/authorized_keys` | Comment field on the line is `github-actions-retro-board-deploy`. |
| Cloudflare Zero Trust | Tunnel `retro-board` | Public Hostname `retroboard.win → http://web:80`. Token stored in `~/docker/retro-board/.env` on drascula as `CLOUDFLARED_TOKEN`. |
| Cloudflare DNS (`retroboard.win` zone) | Apex CNAME pointing at `<tunnel-id>.cfargotunnel.com` | Auto-created by the tunnel's Public Hostname; proxied (orange cloud). The previous Firebase A records were deleted manually before the tunnel hostname could be saved. |

**ACL block** (already present in your Tailscale ACL JSON from the family-finance setup):

```jsonc
{
  "tagOwners": {
    "tag:ci": ["autogroup:admin"]
  },
  "acls": [
    { "action": "accept", "src": ["tag:ci"], "dst": ["drascula:22"] }
  ]
}
```

The runner gets an ephemeral Tailscale identity tagged `tag:ci` and is allowed to reach `drascula:22` only.

## First-time setup (re-bootstrapping from scratch)

Skip if the table above is fully satisfied. These steps recreate everything the auto-deploy depends on.

### 1. Create a deploy SSH key

On your laptop:

```bash
ssh-keygen -t ed25519 -f /tmp/rb-deploy -C "github-actions-retro-board-deploy" -N ""
ssh alucsard@drascula.tail884b89.ts.net 'cat >> ~/.ssh/authorized_keys' < /tmp/rb-deploy.pub
gh secret set DEPLOY_SSH_KEY --repo djanjalia3/retro-board < /tmp/rb-deploy
rm /tmp/rb-deploy /tmp/rb-deploy.pub
```

### 2. Create a Tailscale OAuth client

1. https://login.tailscale.com/admin/settings/oauth → **Generate OAuth client**
2. Description: `github-actions-retro-board-deploy`
3. Scope: **Auth Keys → Write**
4. Generate; copy Client ID and Secret immediately.

```bash
printf '%s' '<client-id>'  | gh secret set TS_OAUTH_CLIENT_ID  --repo djanjalia3/retro-board
printf '%s' '<secret>'      | gh secret set TS_OAUTH_SECRET     --repo djanjalia3/retro-board
```

### 3. Create the Cloudflare tunnel

1. Cloudflare One → Networks → Tunnels → **Create a tunnel** → Cloudflared.
2. Name: `retro-board`. Save.
3. Skip the connector install screen (we run cloudflared via docker compose, not the dashboard install command). Click Next.
4. Public Hostname: subdomain empty, domain `retroboard.win`, type HTTP, URL `web:80`. Save.
5. From the install screen (or the Configure tab afterwards) copy the token (the `eyJ…` string after `--token` in the Docker install command).
6. On drascula:

```bash
echo "CLOUDFLARED_TOKEN=eyJ…" >> ~/docker/retro-board/.env
chmod 600 ~/docker/retro-board/.env
cd ~/docker/retro-board
docker compose -f docker-compose.prod.yml --env-file .env up -d cloudflared
```

The tunnel should show **Healthy** in the Zero Trust dashboard within ~30s.

### 4. Repo on drascula

```bash
cd ~/docker
git clone https://github.com/djanjalia3/retro-board.git
cd retro-board
cat > .env <<EOF
POSTGRES_PASSWORD=$(openssl rand -hex 16)
WEB_PORT=8081
CLOUDFLARED_TOKEN=<paste-from-step-3>
EOF
chmod 600 .env
docker compose -f docker-compose.prod.yml --env-file .env up -d --build
```

### 5. Trigger a test deploy

```bash
gh workflow run deploy.yml --ref master --repo djanjalia3/retro-board
gh run watch
```

Look for `==> web is responding` in the Health check step.

## Rotation

| Secret | When | How |
|---|---|---|
| `DEPLOY_SSH_KEY` | Suspected compromise of the GitHub Actions runner or the private key | Generate new keypair, append public to drascula `authorized_keys`, `gh secret set DEPLOY_SSH_KEY < new-priv-key`, **then** remove the old line from `authorized_keys`. |
| `TS_OAUTH_CLIENT_ID` / `TS_OAUTH_SECRET` | If the secret value leaked anywhere (chat transcripts, CI logs, etc.) | Tailscale admin → revoke the OAuth client → generate a new one with the same description → re-set both repo secrets. |
| `CLOUDFLARED_TOKEN` | If leaked | Cloudflare One → Tunnels → `retro-board` → Configure → **Refresh token** → update `CLOUDFLARED_TOKEN` in `~/docker/retro-board/.env` → `docker compose -f docker-compose.prod.yml --env-file .env up -d cloudflared`. |
| `POSTGRES_PASSWORD` | Quarterly or on suspected compromise | Edit `.env` on drascula → `docker compose -f docker-compose.prod.yml --env-file .env up -d --build` (the api will reconnect; **be aware**: if the Postgres volume already has the old password baked in, you'll need to update it via `psql ALTER USER retro WITH PASSWORD ...` first or wipe the volume — initdb only honours the env var on a fresh data dir). |

## Common operations

### Roll back

```bash
ssh alucsard@drascula.tail884b89.ts.net
cd ~/docker/retro-board
git log --oneline -5            # find the commit before the bad one
git checkout <good-sha>
docker compose -f docker-compose.prod.yml --env-file .env up -d --build
```

Then push a `git revert <bad-sha>` so `master` reflects reality and auto-deploy doesn't re-apply the bad commit on next merge.

### Watch a deploy

Actions tab in the repo. Or live from the server:

```bash
ssh alucsard@drascula.tail884b89.ts.net
docker compose -f docker-compose.prod.yml -p retro-board logs -f --tail=100
```

### Manual deploy

Push to `master` is the normal path; CI auto-deploys. Use the manual button when:

- You're rolling back via `git checkout` on the server and need to rebuild without a commit
- CI is broken but you've validated and want to ship anyway
- You changed an `.env` value on the server and need to recreate containers

```bash
gh workflow run deploy.yml --ref master --repo djanjalia3/retro-board
```

### Pause auto-deploy temporarily

You can't disable from the GitHub UI without editing the workflow. To pause: comment out the `workflow_run` block in `.github/workflows/deploy.yml` and push. Re-enable when ready.

## What's NOT in this pipeline

- **Database migrations as a separate step.** The api runs `db.Database.MigrateAsync()` on startup. Because the compose `depends_on` waits for `db` to be `service_healthy`, this is safe; the api will not start until Postgres accepts connections.
- **Container registry.** The server builds from source on every deploy. Saves the rebuild-vs-pull tradeoff for when build time becomes painful.
- **Cloudflare Access (auth wall).** Not configured. The board UI is intentionally open — anyone with a slug can join. If you ever need to gate it: Zero Trust → Access → Applications → Add a self-hosted application → host `retroboard.win` → email-OTP policy.
- **Secret rotation automation.** All four rotation paths above are manual.
- **Backups.** Tracked under `~/docs/home-server-operations.md` § Backups (still TODO across all services on drascula).

## Gotchas

- **`crypto.randomUUID()` requires a secure context.** Plain HTTP from a non-localhost origin (e.g. raw Tailscale `http://drascula.tail884b89.ts.net:8081`) makes the function undefined. The board page uses it for `retro-session-id`. We added a `crypto.getRandomValues`-based fallback in `board.component.ts` so the LAN URL still works.
- **API CORS allowlist is dev-only.** It only knows about `http://localhost:4200` and `http://localhost:4201`. Production would be cross-origin against the public hostname if it weren't for nginx stripping the `Origin` header on the `/api/` and `/hubs/` proxy locations. If you ever change the topology so the browser hits the API directly, you'll need to make `WithOrigins` config-driven.
- **Initial deploy without a Postgres healthcheck deadlocks the api.** Compose now uses `depends_on: { db: { condition: service_healthy } }` with `pg_isready`. If you fork/copy this repo and the api crashes on startup with `Connection refused`, that's likely been removed.
- **HTTP 400 on first push to GitHub when the pack is large.** `git config http.postBuffer 524288000` solves it; it was needed once because the .NET rewrite added thousands of files in a single push. Set per-repo, not global.
- **The Tailscale OAuth UI no longer shows a per-client tags picker** (as of mid-2026). Tag scoping is via the ACL `tagOwners` block plus the action's runtime `tags:` input.
- **Cloudflare won't let you point a tunnel hostname at a name that already has an A/CNAME.** Delete the conflicting record first (firebase, in our case), then save the tunnel hostname.

## See also

- `~/docs/home-server-operations.md` on drascula (and a synced copy on the laptop) — server-level operations: hardware, OS, AdGuard, Vaultwarden, Tailscale, all services.
- `docs/USER_GUIDE.md` in this repo — end-user walkthrough.
- `.github/workflows/ci.yml`, `.github/workflows/deploy.yml`.
- `docker-compose.prod.yml`, `Dockerfile.web`, `nginx.web.conf`, `server/Dockerfile`.
