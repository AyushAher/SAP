# sap-e2e

Playwright + TypeScript UI automation for the SAP portal. This project is independent of `sap-ui` and `SapApi` so module coverage can grow without mixing unit tests and browser flows.

## Prerequisites

Run the portal locally first:

- Redis: `localhost:6379` (login/session cache)
- API: `http://localhost:5033` (`dotnet run` in `SapApi`, launch profile `http`)
- UI: `http://localhost:5173` (`npm run dev` in `sap-ui`)

Default login matches local UAT: `manager` / `1946` / `PBBPL_UAT`. Override with `E2E_USERNAME`, `E2E_PASSWORD`, `E2E_COMPANY_DB`.

## Commands

```bash
cd sap-e2e
npm install
npx playwright install chromium
npm test                              # headless
npm run test:purchase-requests        # headed Chromium, purchase requests only
npm run test:headed                   # all specs, headed
```

Purchase-request sample item/warehouse can be overridden with `E2E_PR_ITEM` and `E2E_PR_WAREHOUSE`.
