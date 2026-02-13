using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Chat;
using Shared.Models;

namespace Api.Services;

public class OpenAICoachService : ICoachService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private const int MaxRetries = 2;
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);

    private readonly ILogger<OpenAICoachService> _logger;
    private readonly OpenAIClient _openAIClient;

    // Language display names for prompts
    private static readonly Dictionary<string, string> LanguageNames = new()
    {
        ["de"] = "German",
        ["es"] = "Spanish",
        ["fr"] = "French",
        ["it"] = "Italian",
        ["pt"] = "Portuguese",
        ["nl"] = "Dutch"
    };

    public OpenAICoachService(IConfiguration configuration, ILogger<OpenAICoachService> logger)
    {
        _logger = logger;
        
        var apiKey = configuration["OpenAI:ApiKey"] 
            ?? throw new InvalidOperationException("OpenAI:ApiKey configuration is required");
        
        _openAIClient = new OpenAIClient(apiKey);
    }

    public async Task<CoachResponse> AnalyzeAsync(CoachRequest request)
    {
        var modelId = AIModelInfo.GetApiModelId(request.Model);
        var chatClient = _openAIClient.GetChatClient(modelId);

        _logger.LogInformation("Using model {Model} ({ModelId})", request.Model, modelId);

        var systemPrompt = BuildSystemPrompt(request);
        var userPrompt = BuildUserPrompt(request);

        var chatOptions = new ChatCompletionOptions
        {
            Temperature = 0.3f, // Lower temperature for more consistent output
            MaxOutputTokenCount = 2500,
            ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat()
        };

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(userPrompt)
        };

        ChatCompletion completion = null!;
        for (int attempt = 0; attempt <= MaxRetries; attempt++)
        {
            try
            {
                _logger.LogInformation("Sending request to OpenAI (attempt {Attempt}/{MaxRetries})",
                    attempt + 1, MaxRetries + 1);

                using var cts = new CancellationTokenSource(RequestTimeout);
                var result = await chatClient.CompleteChatAsync(messages, chatOptions, cts.Token);
                completion = result.Value;

                _logger.LogInformation("Received response from OpenAI ({InputTokens} in, {OutputTokens} out)",
                    completion.Usage?.InputTokenCount ?? 0,
                    completion.Usage?.OutputTokenCount ?? 0);
                break;
            }
            catch (OperationCanceledException) when (attempt < MaxRetries)
            {
                _logger.LogWarning("OpenAI request timed out (attempt {Attempt}), retrying...", attempt + 1);
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt))); // 1s, 2s
            }
            catch (Exception ex) when (attempt < MaxRetries && IsTransient(ex))
            {
                _logger.LogWarning(ex, "Transient OpenAI error (attempt {Attempt}), retrying...", attempt + 1);
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)));
            }
            catch (OperationCanceledException)
            {
                throw new InvalidOperationException(
                    "The AI took too long to respond. Please try again with shorter text.");
            }
        }

        var responseContent = completion.Content[0].Text;

        try
        {
            var parsed = JsonSerializer.Deserialize<OpenAIResponseDto>(responseContent, JsonOptions)
                ?? throw new InvalidOperationException("Failed to parse OpenAI response");

            return new CoachResponse
            {
                OriginalText = request.Text,
                MinimalFix = parsed.MinimalFix ?? request.Text,
                UpgradedText = parsed.UpgradedText ?? request.Text,
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
                    Phrase = p.Phrase ?? "",
                    Pattern = p.Pattern,
                    Translation = p.Translation ?? "",
                    Level = p.Level,
                    GrammaticalInfo = p.GrammaticalInfo,
                    Notes = p.Notes,
                    Tags = p.Tags ?? []
                }).ToList() ?? [],
                ErrorTags = parsed.ErrorTags ?? [],
                RegisterNote = parsed.RegisterNote,
                Alternative = parsed.Alternative is not null ? new AlternativeInterpretation
                {
                    OriginalPhrase = parsed.Alternative.OriginalPhrase ?? "",
                    PrimaryMeaning = parsed.Alternative.PrimaryMeaning ?? "",
                    AlternativeMeaning = parsed.Alternative.AlternativeMeaning ?? "",
                    AlternativeText = parsed.Alternative.AlternativeText ?? ""
                } : null,
                ModelUsed = modelId,
                Usage = completion.Usage is not null ? new TokenUsage
                {
                    InputTokens = completion.Usage.InputTokenCount,
                    OutputTokens = completion.Usage.OutputTokenCount
                } : null
            };
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse OpenAI response: {Response}", responseContent);
            throw new InvalidOperationException("The AI returned an invalid response format", ex);
        }
    }

    private static string BuildSystemPrompt(CoachRequest request)
    {
        var languageName = LanguageNames.GetValueOrDefault(request.TargetLanguage, "German");
        var levelName = request.TargetLevel.ToString();

        // Feedback language rules — based on learner's target level
        var feedbackLanguageRules = request.TargetLevel switch
        {
            ProficiencyLevel.A1 or ProficiencyLevel.A2 or ProficiencyLevel.B1 => $"""
                ## FEEDBACK LANGUAGE
                The learner is at {levelName} level. Write ALL feedback in ENGLISH:
                - "issue", "whyItMatters", "quickRule" → English
                - "example" → show the {languageName} before → after, but explain in English
                - "registerNote" → English
                - "notes" in phraseBank → English
                - "translation" in phraseBank → English
                - The corrected/upgraded text itself stays in {languageName}, of course
                """,
            ProficiencyLevel.B2 => $"""
                ## FEEDBACK LANGUAGE
                The learner is at B2 level. Write feedback primarily in {languageName}, but:
                - Use simple, clear {languageName} (not C1+ complexity)
                - Include English translations in parentheses for grammar terms
                  e.g., "Nebensatz (subordinate clause)", "Dativ (dative case)"
                - "translation" in phraseBank → English
                - If an explanation would be confusing in {languageName}, fall back to English
                """,
            _ => $"""
                ## FEEDBACK LANGUAGE
                The learner is at {levelName} level. Write ALL feedback in {languageName}:
                - "issue", "whyItMatters", "quickRule" → {languageName} at {levelName} level
                - "translation" in phraseBank → English (this always stays English for reference)
                - Use natural, precise {languageName} in your explanations
                """
        };

        // Register rules - based on user's EXPLICIT choice of formality
        var registerRules = request.Register switch
        {
            Register.Colloquial => """
                - The user has chosen COLLOQUIAL/INFORMAL register
                - Informal language (du, casual expressions) is EXPECTED and correct
                - Do NOT flag informal language as an error
                - Focus feedback on grammar, clarity, and natural colloquial phrasing
                """,
            Register.Neutral => """
                - The user has chosen NEUTRAL register
                - Either du or Sie may be appropriate depending on context
                - Focus on clear, professional but not overly formal language
                - Only flag register if there's INTERNAL INCONSISTENCY (mixing Sie and du in same text)
                """,
            Register.Formal => """
                - The user has chosen FORMAL register
                - Flag informal language (du, casual expressions) as register issues
                - Recommend Sie and appropriate formal/professional phrasing
                - Note opportunities to elevate the language
                """,
            _ => ""
        };
        
        // Context info - helps AI understand the scenario (but register is the user's choice)
        var contextInfo = request.Context switch
        {
            Context.Recruiter => "The text is for communication with a recruiter. Professional first impressions matter.",
            Context.CoverLetter => "The text is a cover letter (Bewerbungsschreiben). Structure and professional polish are important.",
            Context.Behoerden => "The text is for a government office (Behörde). Clear, precise, and respectful language is typical.",
            Context.Landlord => "The text is for a landlord. Polite and clear communication is appropriate.",
            Context.Bank => "The text is for a bank. Professional and precise language is expected.",
            Context.Insurance => "The text is for an insurance company. Clear, formal communication is typical.",
            Context.General => "This is general communication with no specific formality expectations.",
            _ => ""
        };

        var jsonSchema = """
            {
                "minimalFix": "corrected text preserving original meaning and intent exactly",
                "upgradedText": "natural, idiomatic, level-appropriate version",
                "registerNote": "only if Sie/du mixing detected: explain and state which was normalized to, or null",
                "alternative": {
                    "originalPhrase": "the ambiguous phrase from original text",
                    "primaryMeaning": "what you interpreted it as (shown in upgradedText)",
                    "alternativeMeaning": "what else it could mean",
                    "alternativeText": "full upgraded text using the alternative interpretation"
                },
                "variants": {
                    "colloquial": "informal version or null",
                    "neutral": "neutral version or null",
                    "formal": "formal version or null"
                },
                "feedback": [
                    {
                        "issue": "Brief description",
                        "whyItMatters": "One line why",
                        "quickRule": "Memorable rule",
                        "example": "before → after",
                        "tag": "CATEGORY_CODE"
                    }
                ],
                "phraseBank": [
                    {
                        "phrase": "concrete phrase from upgraded text",
                        "pattern": "abstract pattern, e.g. jdm bei etw (Dat) helfen",
                        "translation": "English meaning",
                        "level": "A1|A2|B1|B2|C1|C2 — intrinsic difficulty of this phrase",
                        "grammaticalInfo": "case requirements or null",
                        "notes": "usage notes or null",
                        "tags": ["verb-prep", "idiom", "connector"]
                    }
                ],
                "errorTags": ["CATEGORY_CODE", ...]
            }
            
            NOTE: "alternative" should be null unless there is GENUINE ambiguity where the phrase 
            could reasonably mean two different things. Do not invent ambiguity.
            """;

        return $"""
            You are a {languageName} writing coach helping learners improve their {languageName} writing.
            The learner is targeting {levelName} proficiency level.
            
            {feedbackLanguageRules}
            
            ## STEP 1: MEANING LOCK (do this first!)
            Before making ANY corrections, understand the user's INTENT for each sentence.
            - What tense did they intend? (present, future, past)
            - What modality? (must, want to, will)
            - What is their purpose?
            
            CRITICAL: Do NOT change the user's intended meaning. If they wrote "über B2 springen" (skip over B2), 
            do not rewrite it as "B2 erreichen" (reach B2) — that changes the meaning!
            Only fix grammar/spelling while preserving the original intent.
            
            ## STEP 2: REGISTER DETECTION
            Analyze the INPUT text for register indicators:
            - FORMAL indicators: Sie, Ihnen, Ihr, "Guten Tag", "Sehr geehrte", "Mit freundlichen Grüßen"
            - INFORMAL indicators: du, dir, dich, "Hallo", "Hi", "LG"
            
            If the input MIXES Sie and du forms, this is an internal inconsistency.
            Note it in "registerNote" and normalize to the dominant register.
            
            USER'S CHOSEN REGISTER:
            {registerRules}
            
            CONTEXT INFORMATION:
            {contextInfo}

            ## STEP 3: AMBIGUITY CHECK
            If a phrase in the original text could reasonably be interpreted two different ways:
            - Provide your best interpretation in "upgradedText"
            - Provide the alternative in the "alternative" field
            - Only do this for GENUINE ambiguity (not stylistic choices)
            
            Examples of genuine ambiguity:
            - Unclear pronoun reference
            - Phrase that could be literal or figurative
            - Modal stacking with unclear scope
            - Unclear temporal reference
            
            If there is NO genuine ambiguity, set "alternative" to null.

            ## STEP 4: CORRECTIONS
            Your role is to:
            1. Fix grammar, spelling, and structure (minimal fix - preserve original meaning exactly!)
            2. Upgrade to natural, idiomatic {levelName}-appropriate {languageName}
            3. Provide targeted feedback using the EXACT category codes below
            4. Extract reusable phrase PATTERNS from the upgraded text
            
            ## ERROR CATEGORY CODES (use ONLY these)
            - ORTHOGRAPHY: spelling, capitalization, English/German false friends (is→ist, have→habe)
            - COLLOCATION: fixed expressions, idioms, word combinations
            - REGISTER: formal/informal mismatch or inconsistency
            - CLAUSE_STRUCTURE: subordinate clause verb ORDER (not just position), modal stacking, Ersatzinfinitiv
            - VERB_PATTERN: verb + preposition + case (e.g., warten auf + Akk)
            - STYLE_CONCISION: wordiness, unnatural phrasing, overly literal translations
            
            CRITICAL FEEDBACK RULES:
            - Do NOT say "verb goes to the end" — that's too generic
            - If the issue is modal verb stacking (werden + müssen), explain the STACKING issue
            - If verbs are in the wrong ORDER within a cluster, explain the ORDER
            - Be SPECIFIC about what's actually wrong, not generic rules
            
            ## PHRASE BANK REQUIREMENTS
            For each phrase, provide:
            - The concrete phrase from your upgraded text
            - The ABSTRACT PATTERN showing slots: e.g., "jdm bei etw (Dat) helfen", "ein Zertifikat erwerben"
            - A CEFR "level" (A1–C2) reflecting the INTRINSIC difficulty of this phrase — NOT the learner's target level.
              Examples: "Guten Tag" → A1, "sich auf etw bewerben" → B2, "einer Herausforderung gewachsen sein" → C1
            - This helps learners recognize and reuse the pattern
            
            ## OUTPUT FORMAT
            Respond ONLY with valid JSON:
            {jsonSchema}
            """;
    }

    private static string BuildUserPrompt(CoachRequest request)
    {
        var languageName = LanguageNames.GetValueOrDefault(request.TargetLanguage, "German");
        
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
            Context.Behoerden => "letter to a government office (Behörde)",
            Context.Landlord => "email to a landlord",
            Context.Bank => "communication with a bank",
            Context.Insurance => "communication with an insurance company",
            Context.General => "general communication",
            _ => "general"
        };

        return $"""
            Analyze and improve this {languageName} text.
            
            Context: {contextText}
            Target register: {registerText}
            Goal: {goalText}
            Target proficiency: {request.TargetLevel}
            
            TEXT TO ANALYZE:
            {request.Text}
            
            INSTRUCTIONS:
            1. First, understand the INTENT of each sentence (meaning lock)
            2. Check for Sie/du mixing (register consistency)
            3. Provide minimal fix (grammar only, PRESERVE MEANING)
            4. Provide upgraded version at {request.TargetLevel} level
            5. Give 3-5 feedback items with EXACT category codes
            6. Extract 5-10 phrase PATTERNS from your upgraded text
            
            CRITICAL REMINDERS:
            - User chose "{registerText}" register — respect this choice
            - Do NOT change the user's intended meaning
            - If something like "über B2 springen" appears, preserve that intent (skip over B2), don't change it to "B2 erreichen" (reach B2)
            - Use ONLY the specified category codes for feedback tags
            """;
    }

    // DTOs for parsing OpenAI response
    private record OpenAIResponseDto
    {
        public string? MinimalFix { get; init; }
        public string? UpgradedText { get; init; }
        public string? RegisterNote { get; init; }
        public VariantsDto? Variants { get; init; }
        public List<FeedbackDto>? Feedback { get; init; }
        public List<PhraseDto>? PhraseBank { get; init; }
        public List<string>? ErrorTags { get; init; }
        public AlternativeDto? Alternative { get; init; }
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
        public string? Phrase { get; init; }
        public string? Pattern { get; init; }
        public string? Translation { get; init; }
        public string? Level { get; init; }
        public string? GrammaticalInfo { get; init; }
        public string? Notes { get; init; }
        public List<string>? Tags { get; init; }
    }

    private record AlternativeDto
    {
        public string? OriginalPhrase { get; init; }
        public string? PrimaryMeaning { get; init; }
        public string? AlternativeMeaning { get; init; }
        public string? AlternativeText { get; init; }
    }

    /// <summary>
    /// Determines if an exception is likely transient and worth retrying.
    /// </summary>
    private static bool IsTransient(Exception ex)
    {
        if (ex is HttpRequestException) return true;
        if (ex.GetType().Name == "ClientResultException") return true;
        if (ex.InnerException is not null) return IsTransient(ex.InnerException);
        return false;
    }
}
