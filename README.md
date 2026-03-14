<p align="center">
  <img src="frontend/public/studue-logo.svg" alt="Studue" width="520" />
</p>

<p align="center">
  <a href="https://studue.ch">studue.ch</a>
</p>

<p align="center">
  <img src="docs/screenshot.png" alt="Studue screenshot" width="900" />
</p>

## Overview

Studue is a lightweight assignment tracker for the IT25a_WIN class.

- React + Vite frontend
- Java 21 backend with the standard library only
- GitHub Enterprise OAuth via `github.zhaw.ch`
- JSON file storage for assignments, logs, and access control

## Deployment Artifacts

The GitHub Actions workflow on `main` builds the app, uploads three artifacts, and can deploy them to the server over SSH:

- `frontend-dist` with the static frontend files
- `backend-build` with the backend distribution archives and jars
- `deploy-script` with `scripts/deploy-artifacts.sh`

### Automated deployment

The deploy job copies those three artifacts to the server with `scp`, then runs `scripts/deploy-artifacts.sh` remotely over `ssh`.

Configure these GitHub Actions secrets:

- `DEPLOY_SSH_PRIVATE_KEY`: private key used by GitHub Actions
- `DEPLOY_SSH_KNOWN_HOSTS`: `known_hosts` entry for the target server

The workflow currently deploys to `studue@studue.ch` on port `22`.

The remote user must be able to:

- connect over SSH
- create and remove the temporary deploy directory under `/tmp`
- run `sudo /usr/local/bin/studue-deploy.sh /tmp/studue-deploy-*`

Recommended sudoers setup:

```sudoers
studue ALL=(root) NOPASSWD: /usr/local/bin/studue-deploy.sh *
```

### Manual fallback

If needed, you can still deploy a run manually on the server:

```bash
gh run download <run-id> -R FlurinBruehwiler/studue
chmod +x deploy-script/deploy-artifacts.sh
sudo ./deploy-script/deploy-artifacts.sh
sudo systemctl restart studue
sudo systemctl reload caddy
```

Default deployment locations used by the script:

- frontend: `/var/www/studue`
- backend releases: `/opt/studue/releases`
- current backend symlink: `/opt/studue/current`
- persistent data: `/var/lib/studue/data`

You can override paths with environment variables such as `WWW_ROOT`, `RELEASES_DIR`, `CURRENT_LINK`, `DATA_DIR`, `FRONTEND_ARTIFACT_DIR`, and `BACKEND_ARTIFACT_DIR`.
