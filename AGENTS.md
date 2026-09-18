# AGENTS.md

## Repository purpose

This is the public production source repository for JuStIIFrEsH mods.

## Sync before work

GitHub `main` is the source of truth. Before starting any task, fetch `origin` and fast-forward the local `main` branch to `origin/main`. Do not begin work from a stale local branch.

Before pushing, fetch `origin` again. If `origin/main` changed while you were working, integrate those changes cleanly before pushing. Do not force-push over remote work unless explicitly instructed.

## Valheim

Production Valheim mods live under:

- `Valheim/FreshCommands`
- `Valheim/FreshDedicatedStorage`

Do not use or recreate `JuStIIFrEsH/Games/Valheim`; that location was retired.

## Public repo rules

Only production source, required production assets, release packaging files, and public documentation should be committed here.

Do not add:

- experimental or test assets
- local build artifacts
- personal machine paths
- private notes or handoff files
- secrets, API tokens, or credentials

Local Valheim references should use:

- `VALHEIM_PATH`
- `VALHEIM_PROFILE_PATH`

## Releases

Thunderstore publishing workflows are in `.github/workflows/`.

Current packaged releases are under `Valheim/production/`.

The Thunderstore token is provided through the repository secret `THUNDERSTORE_TOKEN`.

## Website

Public mod links are surfaced at:

`https://justiifresh.com/valheim/`
