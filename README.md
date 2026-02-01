# ✍️ Writing Coach

An AI-powered writing assistant that helps you transform rough drafts into polished, natural German prose — with targeted learning feedback.

## What It Does

- **Minimal Fix**: Corrects grammar, spelling, and word order while preserving your original style
- **C1 Upgrade**: Enhances your text with natural phrasing, idioms, and register-appropriate language
- **Targeted Feedback**: Highlights your top issues with memorable rules and examples
- **Phrase Bank**: Extracts reusable expressions (especially verb+preposition+case packages)
- **Error Tracking**: Identifies patterns in your mistakes for focused practice

## Technology Stack

| Layer | Technology |
|-------|-----------|
| Frontend | Blazor WebAssembly |
| Backend | Azure Functions (C#) |
| Database | Azure Cosmos DB |
| AI | OpenAI GPT-4o-mini |
| Hosting | Azure Static Web Apps |

## Local Development

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download) or later
- [Azure Functions Core Tools v4](https://docs.microsoft.com/azure/azure-functions/functions-run-local)
- [Azurite](https://docs.microsoft.com/azure/storage/common/storage-use-azurite) (Azure Storage Emulator)
- [Azure CLI](https://docs.microsoft.com/cli/azure/install-azure-cli)
- OpenAI API key ([Get one here](https://platform.openai.com/api-keys))

### Quick Start

1. **Clone the repository**
   ```bash
   git clone https://github.com/ronfleming/writing-coach.git
   cd writing-coach
   ```

2. **Configure your OpenAI API key**
   ```bash
   cd Api
   # Edit local.settings.json and replace "your-api-key-here" with your actual key
   ```

3. **Start all services**
   ```powershell
   .\start-local.ps1
   ```

4. **Open the app**
   - Navigate to http://localhost:5042

### Manual Startup (Alternative)

If you prefer to start services individually:

```powershell
# Terminal 1: Start Azurite
azurite --silent

# Terminal 2: Start the API
cd Api
func start --port 7071

# Terminal 3: Start the Client
cd Client
dotnet watch run --urls "http://localhost:5042"
```

## Project Structure

```
writing-coach/
├── Client/          # Blazor WebAssembly frontend
├── Api/             # Azure Functions backend
├── Shared/          # Shared models and DTOs
├── docs/            # Planning documents (gitignored)
└── .github/         # CI/CD workflows
```

## License

MIT License - see [LICENSE](LICENSE) for details.

---

Built with ❤️ for German learners by [Ron Fleming](https://ronfleming.com)

