# AGENTS.md

## Repository purpose

This is the public production source repository for JuStIIFrEsH mods.

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
