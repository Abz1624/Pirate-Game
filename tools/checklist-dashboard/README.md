# Rogue Pirates — Checklist Dashboard

Local web overview for project checklists stored as Markdown.

## Included documents

| Tab | File |
|---|---|
| Integration Checklist | `Rogue_Pirates_Integration_Checklist.md` |
| Implementation Backlog | `Rogue_Pirates_Implementation_Backlog.md` |
| Traceability (plan/12) | `plan/12_TRACEABILITY_CHECKLIST.md` |

## Run locally

Browsers block `fetch()` on `file://` URLs, so start a tiny static server from the **repo root**:

### Windows (PowerShell)

```powershell
.\tools\checklist-dashboard\serve.ps1
```

### macOS / Linux

```bash
chmod +x tools/checklist-dashboard/serve.sh
./tools/checklist-dashboard/serve.sh
```

Then open: **http://localhost:8080/tools/checklist-dashboard/**

## Features

- Per-document stats: total, complete, in progress, needs fix
- Milestone progress bars (integration checklist)
- Filter by status, **tag**, and search by ID/text/description
- Tags and short descriptions on integration checklist rows
- **Refresh** reloads `.md` files after you edit them in the repo

## Updating checklists

1. Edit the markdown files as usual.
2. Click **Refresh** in the dashboard (or reload the page).

No build step required.
