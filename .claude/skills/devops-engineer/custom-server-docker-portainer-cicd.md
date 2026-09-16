# Skill: Custom Server Deployment with Docker, GHCR & Portainer

Load this skill when the deployment target is a self-hosted custom server
(e.g. Ubuntu + Docker) managed with Portainer, rather than a cloud provider.
See `azure-devops-cicd.md` instead for cloud/Azure targets.

## Registry: GitHub Container Registry (GHCR)

- Build and push the Docker image(s) from the existing `cd.yaml` (or add a
  job to it) to `ghcr.io/<owner>/<image>:<tag>`, tagged with the release
  version (e.g. the tag that triggered the GitHub Release).
- Use `docker/build-push-action` in GitHub Actions with
  `permissions: packages: write` and the built-in `GITHUB_TOKEN` — no extra
  registry credentials needed for GHCR from within the same repo/org.
- Tag both the specific version (`v1.4.0`) and, optionally, a moving
  `latest`/`stable` tag if the deployment flow relies on it — but prefer
  pinning the Compose file to the specific version tag for reproducibility
  and easy rollback.

## Portainer setup (single node, multiple stacks)

This kit assumes **one Portainer node** (the custom server) running
**multiple stacks** (one per application/environment) — see
`dotnet-solution-structure.md`'s counterpart for infra: don't create extra
nodes just to "use up" Portainer's node allowance; a single physical
server splitting itself into VMs doesn't add real fault tolerance, it just
adds overhead. Add a new node only when there's genuinely new physical/VM
hardware in the custom server.

Portainer **Business Edition is free forever for up to 3 nodes** and
unlocks GitOps updates (stack webhooks + Git polling), which Portainer CE
does not have. If the user has switched to this free BE tier, both
mechanisms below are available; if still on CE, neither is, and the only
option is a self-hosted GitHub Actions runner (see below) or manual
"Pull and redeploy" in the Portainer UI.

### Recommended flow: webhook triggered from `cd.yaml`

1. In the Portainer stack's settings, enable **GitOps updates → Webhook**
   and copy the generated webhook URL (one-time manual setup — document
   this step, don't assume it's already configured).
2. Store that URL as a GitHub Actions secret (e.g. `PORTAINER_WEBHOOK_URL`)
   — this is a manual, one-time step performed by the user in the repo's
   GitHub settings, never by this agent.
3. Add a final step to `cd.yaml` (after the image is pushed and the
   release is created) that calls the webhook:

```yaml
  - name: Trigger Portainer redeploy
    run: curl -X POST "${{ secrets.PORTAINER_WEBHOOK_URL }}"
```

4. On the Portainer side, make sure **Re-pull image** is enabled on the
   stack so the webhook actually pulls the new tag, not just restarts the
   old one.

### Alternative: Git polling (no webhook secret needed)

Portainer can instead poll the Git repository for the Compose file and
redeploy when the commit hash changes. This only picks up changes to
files in Git, not new image tags on their own — so the release process
needs to also bump the image tag inside the Compose file (or an `.env`
file referenced by it) and let that commit be what Portainer detects.
Simpler to reason about, but adds a "commit the new tag back to the repo"
step to the release flow, which conflicts with keeping this kit's agents
from ever committing/pushing — that step would need to be a plain CI step
in `cd.yaml` (using the workflow's own bot token), not something this
agent performs directly.

### Alternative: self-hosted GitHub Actions runner (works on Portainer CE)

Install a GitHub Actions self-hosted runner on the custom server itself.
The `cd.yaml` job that deploys runs directly on that runner, with local
Docker access:

```yaml
  deploy:
    runs-on: [self-hosted, custom-server]
    steps:
      - run: docker compose -f /opt/stacks/<app>/docker-compose.yml pull
      - run: docker compose -f /opt/stacks/<app>/docker-compose.yml up -d
```

No Portainer automation needed at all — Portainer stays purely a
dashboard. Good fallback if staying on CE or if the user doesn't want to
depend on Portainer's update mechanism.

### Fallback: manual pull and redeploy

No automation: after a release, the user opens Portainer and clicks
**Pull and redeploy** on the stack. Always document this as the manual
equivalent regardless of which automation is chosen, in case the
automation breaks.

## docker-compose.yml shape (Blazor + API + SQL Server)

```yaml
services:
  web:
    image: ghcr.io/<owner>/<app>-web:${APP_VERSION:-latest}
    restart: unless-stopped
    depends_on:
      - api
    ports:
      - "8080:8080"

  api:
    image: ghcr.io/<owner>/<app>-api:${APP_VERSION:-latest}
    restart: unless-stopped
    depends_on:
      - db
    environment:
      - ConnectionStrings__Default=${DB_CONNECTION_STRING}
    env_file:
      - stack.env

  db:
    image: mcr.microsoft.com/mssql/server:2022-latest
    restart: unless-stopped
    environment:
      - ACCEPT_EULA=Y
      - MSSQL_SA_PASSWORD=${DB_SA_PASSWORD}
    volumes:
      - db-data:/var/opt/mssql

volumes:
  db-data:
```

- Pin `APP_VERSION` per stack via Portainer's stack environment variables
  (or an uploaded `.env`) rather than hardcoding — this is what makes
  rollback a one-line change (set `APP_VERSION` back to the previous tag
  and redeploy).
- Never commit real secrets (`DB_SA_PASSWORD`, connection strings) into
  the Compose file or the repo — use `stack.env` uploaded directly in
  Portainer, or Portainer's own environment variable fields.

## Staging via a second stack (same node)

Since this custom server runs one node with multiple stacks already, add a
`staging` stack as a second, independent Compose deployment on the same
node — different container names/ports/volume, same image repository but
usually pinned to a pre-release tag or the `main` branch's build. Promote
to the "production" stack only after validating there. No second node
required for this.

## Backups (SQL Server)

Document, at minimum, a scheduled backup of the `db-data` volume or a
`BACKUP DATABASE` job (e.g. via a small cron container or the host's
crontab calling `docker exec ... sqlcmd`), with backups stored outside the
same volume/disk if possible. This agent can write the backup script and
document the restore procedure, but scheduling/verifying it on the actual
host is the user's responsibility.

## Rollback

Because the image tag is a variable (`APP_VERSION`), rollback is: set it
back to the previous known-good tag in Portainer's stack environment
variables (or the webhook's `?tag=` parameter, or the polled Compose
file/`.env`) and redeploy — no rebuild needed since the previous image is
still in GHCR.
