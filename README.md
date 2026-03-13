# studue
studue.ch

## Deployment artifacts

The GitHub Actions build on `main` uploads three artifacts:

- `frontend-dist` with the static frontend files
- `backend-build` with the backend distribution archives and jars
- `deploy-script` with `scripts/deploy-artifacts.sh`

Typical server-side flow:

```bash
gh run download <run-id> -R FlurinBruehwiler/studue
chmod +x deploy-artifacts.sh
sudo ./deploy-artifacts.sh
sudo systemctl restart studue
sudo systemctl reload caddy
```

Default deployment locations used by the script:

- frontend: `/var/www/studue`
- backend releases: `/opt/studue/releases`
- current backend symlink: `/opt/studue/current`
- persistent data: `/var/lib/studue/data`

You can override paths with environment variables such as `WWW_ROOT`, `RELEASES_DIR`, `CURRENT_LINK`, `DATA_DIR`, `FRONTEND_ARTIFACT_DIR`, and `BACKEND_ARTIFACT_DIR`.
