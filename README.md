# Writing Coach

AI-powered writing assistant for German language learners. Corrects grammar, upgrades text to your target CEFR level, and builds a personal phrase bank from each session.

## Features

- **Minimal Fix** — Grammar and spelling corrections, preserving your original style
- **Level Upgrade** — Rewrites text with natural phrasing appropriate for your target level (A1–C2)
- **Targeted Feedback** — Categorized error explanations with memorable rules and examples
- **Phrase Bank** — Extracts reusable patterns (verb+preposition+case, idioms, connectors) with CEFR levels
- **Session History** — Full history of coaching sessions stored in Cosmos DB
- **Phrase Tracking** — Mark phrases as learning/learned, favorite them, filter and search
- **Ambiguity Detection** — Flags genuinely ambiguous phrases with alternative interpretations
- **Register Awareness** — Respects your chosen formality level; detects Sie/du mixing
- **Model Selection** — Choose between GPT-4o-mini, GPT-4.1-mini, GPT-4o, and GPT-4.1

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Frontend | Blazor WebAssembly (.NET 9) |
| Backend | Azure Functions v4 (.NET 8, isolated worker) |
| Database | Azure Cosmos DB (NoSQL, serverless) |
| AI | OpenAI API (structured JSON output) |
| Hosting | Azure Static Web Apps |

## Project Structure

```
writing-coach/
├── Client/          Blazor WASM frontend
│   ├── Components/  Shared display components
│   ├── Layout/      Shell layout and navigation
│   ├── Pages/       Route pages (Coach, History, Phrases, About)
│   └── Services/    API client services
├── Api/             Azure Functions backend
│   ├── Functions/   HTTP endpoints (Coach, Sessions, Phrases, Admin)
│   └── Services/    OpenAI integration, Cosmos DB repositories
├── Shared/          Models shared between Client and Api
│   └── Models/      Request/response DTOs, Cosmos DB documents
└── docs/            Planning documents (gitignored)
```

## Local Development

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download) and [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Azure Functions Core Tools v4](https://docs.microsoft.com/azure/azure-functions/functions-run-local)
- [Azurite](https://www.npmjs.com/package/azurite) (`npm install -g azurite`)
- [OpenAI API key](https://platform.openai.com/api-keys)
- Azure Cosmos DB connection string (free tier or serverless)

### Setup

1. Clone the repo and configure secrets:
   ```
   git clone https://github.com/ronfleming/writing-coach.git
   cd writing-coach
   ```

2. Create `Api/local.settings.json`:
   ```json
   {
     "IsEncrypted": false,
     "Values": {
       "AzureWebJobsStorage": "UseDevelopmentStorage=true",
       "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
       "OpenAI__ApiKey": "<your-openai-key>",
       "CosmosDb__ConnectionString": "<your-cosmos-connection-string>"
     }
   }
   ```

3. Start all services:
   ```powershell
   .\start-dev.ps1
   ```

4. Open http://localhost:5042

### Manual Startup

```powershell
# Terminal 1: Azurite (storage emulator for Azure Functions)
azurite --silent

# Terminal 2: API
cd Api
func start

# Terminal 3: Client
cd Client
dotnet watch run
```

### Port Cleanup

If you get "address already in use" errors:
```powershell
.\kill-ports.ps1
```

## Pending

- Individual phrase/session hard delete
- Authentication (Azure SWA built-in providers)
- Premium model gating (requires auth)
- BYOK (bring your own OpenAI key)
- Anki export for phrase bank
- Error analytics dashboard
- Multi-language support (data model ready)

## License

MIT

---

Built by [Ron Fleming](https://ronfleming.com)
