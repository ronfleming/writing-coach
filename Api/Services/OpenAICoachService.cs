using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Chat;
using Shared.Models;

namespace Api.Services;

public class OpenAICoachService : ICoachService
{
    private readonly ILogger<OpenAICoachService> _logger;
    private readonly ChatClient _chatClient;
    private const string SystemPrompt = """
        You are a German writing coach helping learners improve their German writing.
        Your role is to:
        1. Fix grammar, spelling, and word order issues (minimal fix)
        2. Upgrade the text to natural, idiomatic C1-level German (C1 upgrade)
        3. Provide targeted feedback on the top issues
        4. Extract reusable phrases for the learner's phrase bank

        IMPORTANT RULES:
        - Preserve the original meaning
        - Don't invent facts or add information not in the original
        - Focus on verb+preposition+case packages when relevant
        - Provide feedback that is actionable and memorable
        - Keep explanations concise

        Respond ONLY with valid JSON matching this exact structure:
        {
            "minimalFix": "...",
            "c1Upgrade": "...",
            "variants": {
                "colloquial": "...",
                "neutral": "...",
                "formal": "..."
            },
            "feedback": [
                {
                    "issue": "Brief description",
                    "whyItMatters": "Why this matters",
                    "quickRule": "Memorable rule",
                    "example": "before → after",
                    "tag": "error-category"
                }
            ],
            "phraseBank": [
                {
                    "german": "phrase",
                    "english": "meaning",
                    "case": "Akkusativ|Dativ|etc or null",
                    "notes": "usage notes or null",
                    "tags": ["verb-prep", "idiom", "connector"]
                }
            ],
            "errorTags": ["prep-case", "word-order", ...]
        }

        Error tag categories to use: prep-case, word-order, article-declension, verb-conjugation, 
        noun-gender, compound-words, comma-rules, sentence-structure, register-mismatch, 
        collocation, idiom, false-friend
        """;

    public OpenAICoachService(IConfiguration configuration, ILogger<OpenAICoachService> logger)
    {
        _logger = logger;
        
        var apiKey = configuration["OpenAI:ApiKey"] 
            ?? throw new InvalidOperationException("OpenAI:ApiKey configuration is required");
        
        var client = new OpenAIClient(apiKey);
        _chatClient = client.GetChatClient("gpt-4o-mini");
    }

    public async Task<CoachResponse> AnalyzeAsync(CoachRequest request)
    {
        var userPrompt = BuildUserPrompt(request);
        
        _logger.LogInformation("Sending request to OpenAI for text analysis");

        var chatOptions = new ChatCompletionOptions
        {
            Temperature = 0.3f, // Lower temperature for more consistent output
            MaxOutputTokenCount = 2000,
            ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat()
        };

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(SystemPrompt),
            new UserChatMessage(userPrompt)
        };

        var completion = await _chatClient.CompleteChatAsync(messages, chatOptions);
        var responseContent = completion.Value.Content[0].Text;

        _logger.LogInformation("Received response from OpenAI");

        try
        {
            var parsed = JsonSerializer.Deserialize<OpenAIResponseDto>(responseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new InvalidOperationException("Failed to parse OpenAI response");

            return new CoachResponse
            {
                OriginalText = request.Text,
                MinimalFix = parsed.MinimalFix ?? request.Text,
                C1Upgrade = parsed.C1Upgrade ?? request.Text,
                Variants = parsed.Variants is not null ? new StyleVariants
                {
                    Colloquial = parsed.Variants.Colloquial,
                    Neutral = parsed.Variants.Neutral,
                    Formal = parsed.Variants.Formal
                } : null,
                Feedback = parsed.Feedback?.Select(f => new FeedbackItem
                {
                    Issue = f.Issue ?? "",
                    WhyItMatters = f.WhyItMatters ?? "",
                    QuickRule = f.QuickRule ?? "",
                    Example = f.Example ?? "",
                    Tag = f.Tag
                }).ToList() ?? [],
                PhraseBank = parsed.PhraseBank?.Select(p => new PhraseEntry
                {
                    German = p.German ?? "",
                    English = p.English ?? "",
                    Case = p.Case,
                    Notes = p.Notes,
                    Tags = p.Tags ?? []
                }).ToList() ?? [],
                ErrorTags = parsed.ErrorTags ?? []
            };
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse OpenAI response: {Response}", responseContent);
            throw new InvalidOperationException("The AI returned an invalid response format", ex);
        }
    }

    private static string BuildUserPrompt(CoachRequest request)
    {
        var registerText = request.Register switch
        {
            Register.Colloquial => "colloquial/informal",
            Register.Neutral => "neutral",
            Register.Formal => "formal/professional",
            _ => "neutral"
        };

        var goalText = request.Goal switch
        {
            Goal.Correct => "correct grammar and spelling",
            Goal.Shorten => "make more concise",
            Goal.Clarify => "make clearer and easier to understand",
            Goal.Persuade => "make more persuasive",
            Goal.Diplomatic => "make more diplomatic and tactful",
            _ => "correct"
        };

        var contextText = request.Context switch
        {
            Context.Recruiter => "email to a recruiter",
            Context.CoverLetter => "cover letter / Bewerbungsschreiben",
            Context.Behoerden => "formal letter to a government office (Behörde)",
            Context.Landlord => "email to a landlord",
            Context.Bank => "formal communication with a bank",
            Context.Insurance => "communication with an insurance company",
            Context.General => "general communication",
            _ => "general"
        };

        return $"""
            Analyze and improve this German text.
            
            Context: {contextText}
            Target register: {registerText}
            Goal: {goalText}
            Target proficiency: {request.TargetLevel}
            
            TEXT TO ANALYZE:
            {request.Text}
            
            Provide:
            1. A minimal fix (grammar/spelling only, preserve original style)
            2. A C1-level upgrade (natural, idiomatic, register-appropriate)
            3. Style variants if the register differs from requested
            4. Top 3-5 feedback items focusing on the most impactful issues
            5. 5-10 reusable phrases from the improved text
            6. Error tags categorizing the issues found
            """;
    }

    // Internal DTOs for parsing OpenAI response
    private record OpenAIResponseDto
    {
        public string? MinimalFix { get; init; }
        public string? C1Upgrade { get; init; }
        public VariantsDto? Variants { get; init; }
        public List<FeedbackDto>? Feedback { get; init; }
        public List<PhraseDto>? PhraseBank { get; init; }
        public List<string>? ErrorTags { get; init; }
    }

    private record VariantsDto
    {
        public string? Colloquial { get; init; }
        public string? Neutral { get; init; }
        public string? Formal { get; init; }
    }

    private record FeedbackDto
    {
        public string? Issue { get; init; }
        public string? WhyItMatters { get; init; }
        public string? QuickRule { get; init; }
        public string? Example { get; init; }
        public string? Tag { get; init; }
    }

    private record PhraseDto
    {
        public string? German { get; init; }
        public string? English { get; init; }
        public string? Case { get; init; }
        public string? Notes { get; init; }
        public List<string>? Tags { get; init; }
    }
}

