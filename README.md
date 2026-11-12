# Cuadre

Local management app for a small retail business: inventory, daily close by inventory difference,
cash boxes and Profit First envelopes. Single user, runs on one Windows PC, works offline.

- Requirements: [`docs/REQUISITOS_APP.md`](docs/REQUISITOS_APP.md)
- Product proposal: [`docs/PROPUESTA_PRODUCTO.md`](docs/PROPUESTA_PRODUCTO.md)

## Stack

| Layer | Tech |
|---|---|
| API | ASP.NET Core 10 Minimal APIs, layered: `Cuadre.Api` / `Cuadre.Application` / `Cuadre.Contracts` |
| Data | SQLite (WAL, `synchronous=FULL`) + Dapper + DbUp migrations |
| Client | Angular 22, Angular CDK, own components |
| Desktop | Edge in `--app` mode, launched by the API; the server stops when the window closes |
| Tests | xUnit v3 integration tests against a real temporary SQLite file; Vitest on the client |

Conventions: money is stored as integer **cents**, percentages as integer **basis points**,
business dates as `yyyy-MM-dd`, timestamps as UTC. Code is in English; the UI is in Spanish.

## Run (development)

```bash
# API on http://127.0.0.1:5180 (data in ./.data, never committed)
dotnet run --project Server/Cuadre.Api --launch-profile Development

# Client on http://localhost:4200 (proxies /api to the API)
cd Client
npm install
npm start
```

> The repo path contains `&` ("Finanzas & Negocios"), which breaks the `node_modules/.bin` shims under
> `cmd.exe`. Always use the npm scripts (`npm start`, `npm test`, `npm run build`), never `npx ng ...`.

## Run (desktop)

```bash
cd Client && npm run build          # publishes into Server/Cuadre.Api/wwwroot
cd .. && dotnet run --project Server/Cuadre.Api --launch-profile Desktop
```

## Tests

```bash
dotnet test Cuadre.slnx
cd Client && npm test
```
