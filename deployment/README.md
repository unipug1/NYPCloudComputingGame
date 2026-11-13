# NYP Open House AI backend

Work branch: `openrouter-integration`. Project: `project-bbbe7f81-286f-4758-9d6` (the existing **My First Project**, reused with permission because creation quota is exhausted).

The Unity client posts to Cloud Run in `asia-southeast1`. Cloud Run distributes requests across at most two instances, with concurrency 20 per instance and no minimum instances. Firestore keeps a persistent request lease for each of the twenty registered kiosks, preventing duplicate active requests across instances and revisions. This limits active game requests to 20. Event counters are partitioned by kiosk to avoid serializing all players on a hot global counter. Gemini **3.5 Flash-Lite**, global Vertex endpoint, is primary; minimal thinking and 512 maximum output tokens keep responses short. Both providers stream the guard's words into the existing dialogue box. Trust, emotion, choices and dead-end decisions are applied only after the completed JSON validates.

OpenRouter is a server-side backup using `nvidia/nemotron-3.5-lightning:free`, with a 14-second provider timeout, one attempt per provider, and free-provider price restrictions. The original Llama free model is no longer in OpenRouter's catalog. Free backups are best-effort: OpenRouter currently limits free variants to 20 requests/minute and 50/day, or 1,000/day for accounts with qualifying prior credit purchases. This is not enough for sustained event-wide failover; a funded, inexpensive paid backup needs separate cost approval and configuration. If no free provider is available, or both providers fail, Unity shows a neutral offline reply with no trust penalty. There is no paid-model escalation. Cloud Run uses its service identity for Vertex; there are no Google keys in Unity. Per-kiosk keys gate the endpoint; provider keys are held in Secret Manager.

## Current provisioning defaults

**Current live status (14 November 2026): Vertex testing is enabled.** Billing credits were reviewed separately and expire after the event; private billing details are kept outside source control. One live request passed: first streamed dialogue arrived in 1,990 ms and the validated response completed in 2,967 ms from this development machine. These are single-request observations, not a 20-player model latency guarantee. The OpenRouter key is installed and a free Nemotron backup request passed in 4,345 ms. The previously selected Gemma free upstream returned 429, so the live configuration was changed to the tested Nemotron route.

The deployment script defaults paid model traffic to **disabled**; use `--enable-ai` when intentionally redeploying with it enabled. Cloud infrastructure provisioning does not confirm credit coverage, and the first request's credit application will appear later in billing reports. Credits belong to the billing account, are shared with its other projects, and do not renew when a project is created. OpenRouter has separate billing and cannot spend Google credits.

| Control | Testing, until 6 January 2027 | Event, 7-9 January 2027 |
| --- | --- | --- |
| Provider attempts per Singapore calendar day | 100 | 6,000 |
| Provider attempts for entire phase | 500 | 18,000 |
| Conservative model reservation at US$0.01/attempt | US$5 maximum | US$180 maximum |
| Simultaneous active requests | 20 | 20 |
| Per-kiosk spacing | 2 seconds, one active request | Same |
| History sent | Last four exchanges, maximum 12,000 UTF-8 bytes including system prompt | Same |

The event allowance cannot be used by testing. Each of the twenty kiosks gets a non-transferable event share of **300 provider attempts per day, 900 over the event**, for a combined 6,000/day and 18,000 total. Testing uses the shared global counter because its traffic is infrequent. A fallback consumes a second attempt. Failed requests also consume their reserved attempts. Counters are persisted in Firestore and survive deployments, cold starts and scaling. No requests are automatically retried. A request UUID supports replay without another model invocation; a conflicting payload or kiosk using the same UUID is rejected. Expiring leases recover slots after crashes. After **10 January 2027 00:00 Asia/Singapore**, the application refuses new AI requests. The three event days are interpreted in Singapore local time, where NYP is located.

Additional Cloud Billing spend caps are scoped to this project, exclude credits when measuring gross spend, and reset monthly:

- Vertex: **SGD 100/month**.
- Cloud Run: **SGD 15/month**.

Cloud Billing caps have reporting/enforcement delay and are not instantaneous hard limits. Application counters restrict model calls before they are sent. The US$0.01 reservation is a conservative estimate for the pinned model and bounded text payload, not a contractual price guarantee. Infrastructure (Firestore, builds, image storage, networking) is separate; Cloud Run's cap does not cover these services. No provisioned model throughput, dedicated load balancer, Redis, or always-on VM is used.

At published global Gemini 3.5 Flash-Lite rates of US$0.30/M input tokens and US$2.50/M output tokens, an illustrative turn with 1,500 input tokens and 250 output tokens is **US$0.001075**. 18,000 such turns would be **US$19.35** in model usage, before credits and infrastructure. Actual usage depends on conversations and event attendance, and the spend cap can stop usage before all attempt allowances are used. Recheck prices and remaining shared credits shortly before the event.

Sources: [model and retirement date](https://docs.cloud.google.com/gemini-enterprise-agent-platform/models/gemini/3-5-flash-lite), [model pricing](https://cloud.google.com/model-garden/pricing), [trial and credit terms](https://docs.cloud.google.com/free/docs/free-cloud-features), [spend cap limitations](https://docs.cloud.google.com/billing/docs/how-to/budgets-spend-caps), [Cloud Run autoscaling](https://docs.cloud.google.com/run/docs/about-instance-autoscaling).

## Deploy and enable

Run from the repository root with the existing gcloud login:

```powershell
python deployment/deploy.py --project project-bbbe7f81-286f-4758-9d6 --billing-account 01D1F3-1E6532-02F1D6
```

This creates the regional Firestore database if absent, a runtime service account, two secrets, the two service-specific spend caps, an Artifact Registry repository, and the Cloud Run service. Only `backend/` is sent to Cloud Build. Rerunning this script preserves stored counters and keys. It deploys with paid model calls disabled by default. The billing currency in this script is SGD for this account.

After reviewing the credits, enable the already deployed service without rebuilding:

```powershell
gcloud run services update nyp-game-ai --project=project-bbbe7f81-286f-4758-9d6 --region=asia-southeast1 --account=pushkal.vashist@amztech.ai --update-env-vars=PAID_AI_ENABLED=true
```

To disable it, set `PAID_AI_ENABLED=false`. An immediate application kill switch is the boolean field `disabled=true` on Firestore document `gameAi/control`; it also blocks backups. Do not delete that document to reset the limits. Changing model or pricing settings requires reviewing the cost allowance.

The `nyp-game-openrouter-key` secret is configured. New installations initially use `NOT_CONFIGURED`, which disables backup calls. Add a real OpenRouter key as a new secret version in [Secret Manager](https://console.cloud.google.com/security/secret-manager/secret/nyp-game-openrouter-key/versions?project=project-bbbe7f81-286f-4758-9d6), then roll a new revision so it loads `latest`:

```powershell
gcloud run services update nyp-game-ai --project=project-bbbe7f81-286f-4758-9d6 --region=asia-southeast1 --account=pushkal.vashist@amztech.ai --update-secrets=OPENROUTER_API_KEY=nyp-game-openrouter-key:latest
```

## Configure the 20 game computers

Deployment writes ignored per-device configuration files to `deployment/kiosk-configs.local.d/`, and configures this Unity Editor as kiosk01 in `UserSettings/nyp-ai-config.json`. Keep the `device-keys.local.json` file private; the authoritative keys are also in Secret Manager. Give each game computer a different kiosk configuration to avoid sharing its concurrency slot.

For a standalone build, copy its kiosk JSON to `Application.persistentDataPath/nyp-ai-config.json`, or set `NYP_AI_URL` and `NYP_AI_GAME_KEY` in that process's environment. Provider keys must never be put in these files. The keys are intended for the 20 controlled event computers, not an unrestricted public browser release. Browser origins are denied by default; a WebGL release needs an explicit origin allowlist and a client streaming test because browser buffering can differ from the desktop player.

## Verification and event preparation

```powershell
npm ci --prefix backend
npm test --prefix backend
```

The 15 automated tests cover 20 parallel simulated players, provider streaming and fallback, failed-call accounting, input validation, authentication, database failures, replay prevention, Unicode chunk boundaries, total event budget shares, and Singapore event dates. Tests do not spend model credits. A one-off Cloud Run verification job also passed against the actual Firestore database: two independent clients admitted all 20 event requests in **648 ms**, rejected a busy kiosk and an unregistered 21st, persisted counts, and released all leases. This measures database admission, not model response latency. It used isolated counter documents and removed its test data without changing production allowances. `/health` does not invoke a model.

Before 7 January, check credit expiry and eligible services, verify the deployed model with a small live smoke test, configure the backup key, and run the game on the actual devices. There is no automatic reminder or future scheduled task. Keep minimum instances at zero during testing; optionally prewarm with `/health` when opening the booth. The autoscaling cap and per-device credentials already support 20 players; no dedicated load balancer is needed.

The installed Unity 6000.3.13f1 Editor currently has no active license, so its batch startup stops before a play test. All 48 game C# scripts were separately compiled with that Editor's Roslyn compiler and installed Unity/package assemblies, with no compile errors. This is a compilation check, not a rendered game or streaming play test. The live Vertex streaming smoke test passed after credit review; the authenticated OpenRouter backup request also passed and its response matched the game schema.
