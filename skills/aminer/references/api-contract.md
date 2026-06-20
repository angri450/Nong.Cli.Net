# AMiner API Contract

## Credentials

`nong aminer` reads the AMiner JWT from either:

- `NONG_LIT_AMINER_KEY`
- `AMINER_API_KEY`

Do not print real token values. Report only whether the needed variable is present.

## Cost Boundary

The AMiner surface mixes free and paid endpoints. Use the current CLI help and JSON manifest as the source of truth for command names and price labels.

- Free discovery: `scholar`, `paper`, `rec`, `patent`, `org`, `venue`, `paper-info`, `patent-info`
- Paid research/deep-dive: `paper-pro`, `paper-detail`, `paper-qa`, `paper-citations`, `deep-research`, `scholar-detail`, `scholar-figure`, `scholar-stat`, `scholar-papers`, `scholar-patents`, `scholar-projects`, `org-detail`, `org-patents`, `patent-detail`

Never silently upgrade a user from a free path to a paid path without saying so.

## Workflow

Typical low-cost routing:

```powershell
nong aminer scholar --name "张钹" --json
nong aminer paper --title "large language model" --json
nong aminer paper-info --ids 53e9a331b7602d9701e7b0d1 --json
```

Typical deeper routing:

```powershell
nong aminer scholar-detail --id <scholar-id> --json
nong aminer scholar-papers --id <scholar-id> --json
nong aminer paper-detail --id <paper-id> --json
```

## Boundaries

Do not scrape aminer.cn pages, fabricate IDs, hide pricing, or claim that AMiner API output is exhaustive or peer-reviewed truth. Use `lit` for OpenAlex/Crossref/Unpaywall workflows and `metaso` for web/document search plus reader/chat.
