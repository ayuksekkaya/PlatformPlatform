using System.CommandLine;
using System.CommandLine.NamingConventionBinder;
using System.Text;
using Karambolo.PO;
using OpenAI.Chat;
using PlatformPlatform.DeveloperCli.Installation;
using PlatformPlatform.DeveloperCli.Utilities;
using Spectre.Console;

namespace PlatformPlatform.DeveloperCli.Commands;

public class TranslateCommand : Command
{
    public TranslateCommand() : base(
        "translate",
        $"Update language files with missing translations powered by {OpenAiTranslationService.ModelName}"
    )
    {
        var languageOption = new Option<string?>(
            ["<language>", "--language", "-l"],
            "The name of the language to translate (e.g `da-DK`)"
        );

        AddOption(languageOption);

        Handler = CommandHandler.Create<string?>(Execute);
    }

    private static async Task<int> Execute(string? language)
    {
        Prerequisite.Ensure(Prerequisite.Dotnet);

        try
        {
            var translationFile = GetTranslationFile(language);
            await RunTranslation(translationFile);
            return 0;
        }
        catch (Exception e)
        {
            AnsiConsole.MarkupLine($"[red]Translation failed. {e.Message}[/]");
            return 1;
        }
    }

    private static string GetTranslationFile(string? language)
    {
        var translationFiles = Directory.GetFiles(Configuration.ApplicationFolder, "*.po", SearchOption.AllDirectories)
            .Where(f => !f.Contains("node_modules") &&
                        !f.EndsWith("en-US.po") &&
                        !f.EndsWith("pseudo.po")
            )
            .ToDictionary(s => s.Replace(Configuration.ApplicationFolder, ""), f => f);

        if (language is not null)
        {
            var translationFile = translationFiles.Values
                .FirstOrDefault(f => f.Equals($"{language}.po", StringComparison.OrdinalIgnoreCase));

            return translationFile
                   ?? throw new InvalidOperationException($"Translation file for language '{language}' not found.");
        }

        var prompt = new SelectionPrompt<string>()
            .Title("Please select the file to translate")
            .AddChoices(translationFiles.Keys);

        var selection = AnsiConsole.Prompt(prompt);
        return translationFiles[selection];
    }

    private static async Task RunTranslation(string translationFile)
    {
        var poCatalog = await ReadTranslationFile(translationFile);
        var entries = poCatalog.EnsureOnlySingularEntries();

        AnsiConsole.MarkupLine($"Language detected: {poCatalog.Language}");

        var translationService = OpenAiTranslationService.Create();
        var translator = new Translator(translationService, poCatalog.Language);
        var translated = await translator.Translate(entries);

        if (translated.Any())
        {
            foreach (var translatedEntry in translated)
            {
                poCatalog.UpdateEntry(translatedEntry);
            }

            await WriteTranslationFile(translationFile, poCatalog);
        }

        AnsiConsole.MarkupLine($"Total tokens used: [yellow]{translationService.UsageStatistics.TotalTokens}[/]. Translation cost: [yellow]${translationService.UsageStatistics.TotalCost:F4}[/]");
    }

    private static async Task<POCatalog> ReadTranslationFile(string translationFile)
    {
        var translationContent = await File.ReadAllTextAsync(translationFile);
        var poParser = new POParser();
        var poParseResult = poParser.Parse(new StringReader(translationContent));
        if (!poParseResult.Success)
        {
            throw new InvalidOperationException($"Failed to parse PO file. {poParseResult.Diagnostics}");
        }

        if (poParseResult.Catalog.Language is null)
        {
            throw new InvalidOperationException($"Failed to parse PO file {translationFile}. Language not found.");
        }

        return poParseResult.Catalog;
    }

    private static async Task WriteTranslationFile(string translationFile, POCatalog poCatalog)
    {
        var poGenerator = new POGenerator(new POGeneratorSettings { IgnoreEncoding = true, IgnoreLongLines = true });
        var fileStream = File.OpenWrite(translationFile);
        poGenerator.Generate(fileStream, poCatalog);
        await fileStream.FlushAsync();
        fileStream.Close();

        AnsiConsole.MarkupLine($"[green]Translated file saved to {translationFile}[/]");
        AnsiConsole.MarkupLine("[yellow]WARNING: Please proofread to make sure the language is inclusive.[/]");
    }

    private sealed class Translator(OpenAiTranslationService translationService, string targetLanguage)
    {
        private readonly string _englishToTargetLanguagePrompt = CreatePrompt("English", targetLanguage);
        private readonly string _targetLanguageToEnglishPrompt = CreatePrompt(targetLanguage, "English");

        public async Task<IReadOnlyCollection<POSingularEntry>> Translate(IReadOnlyCollection<POSingularEntry> translationEntries)
        {
            var translatedEntries = translationEntries.Where(x => x.HasTranslation()).ToList();
            var nonTranslatedEntries = translationEntries.Where(x => !x.HasTranslation()).ToList();
            AnsiConsole.MarkupLine($"Keys missing translation: {nonTranslatedEntries.Count}");
            if (nonTranslatedEntries.Count == 0)
            {
                AnsiConsole.MarkupLine("[green]Translation completed, nothing to translate.[/]");
                return [];
            }

            var toReturn = new List<POSingularEntry>();
            foreach (var nonTranslatedEntry in nonTranslatedEntries)
            {
                var translated = await TranslateSingleEntry(translatedEntries.AsReadOnly(), nonTranslatedEntry);
                if (translated == null) // User chose to stop
                {
                    AnsiConsole.MarkupLine("[yellow]Translation process stopped. Saving changes collected so far.[/]");
                    break;
                }

                if (!translated.HasTranslation()) continue;
                translatedEntries.Add(translated);
                toReturn.Add(translated);
            }

            if (toReturn.Count > 0)
            {
                AnsiConsole.MarkupLine($"[green]{toReturn.Count} entries have been translated.[/]");
            }
            else
            {
                AnsiConsole.MarkupLine("[yellow]No entries were translated.[/]");
            }

            return toReturn;
        }

        private async Task<POSingularEntry?> TranslateSingleEntry(
            IReadOnlyCollection<POSingularEntry> translatedEntries,
            POSingularEntry nonTranslatedEntry
        )
        {
            var currentPrompt = _englishToTargetLanguagePrompt;

            while (true)
            {
                AnsiConsole.MarkupLine($"Translating: [cyan]{nonTranslatedEntry.Key.Id}[/]");
                POSingularEntry translated = null!;
                POSingularEntry reverseTranslated = null!;
                await AnsiConsole.Status().StartAsync("Initialize translation...", async context =>
                    {
                        translated = await translationService.Translate(
                            currentPrompt, translatedEntries, nonTranslatedEntry, context
                        );

                        // Translate back into the original language and check if translation matches
                        AnsiConsole.MarkupLine($"Translated to: [cyan]{translated.GetTranslation()}[/]");

                        AnsiConsole.MarkupLine("Checking translation...");
                        var reverseTranslations = translatedEntries.Select(x => x.ReverseKeyAndTranslation()).ToArray();
                        reverseTranslated = await translationService.Translate(
                            _targetLanguageToEnglishPrompt,
                            reverseTranslations,
                            translated.ReverseKeyAndTranslation(),
                            context
                        );
                    }
                );

                if (string.Equals(reverseTranslated.GetTranslation(), translated.Key.Id, StringComparison.OrdinalIgnoreCase))
                {
                    AnsiConsole.MarkupLine("[green]Reverse translation is matching.[/]");
                    return translated;
                }

                AnsiConsole.MarkupLine($"[yellow]Reverse translation is not matching. Reverse translation is[/] [cyan]{reverseTranslated.GetTranslation()}[/]");

                var choice = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("What would you like to do?")
                        .AddChoices("Accept translation", "Try again", "Provide context for retranslation", "Input own translation", "Skip", "Stop and save")
                );

                switch (choice)
                {
                    case "Accept translation":
                        AnsiConsole.MarkupLine("[green]Translation accepted.[/]");
                        return translated;
                    case "Try again":
                        AnsiConsole.MarkupLine("[green]Trying translation again...[/]");
                        continue;
                    case "Provide context for retranslation":
                        var context = AnsiConsole.Ask<string>("Please provide context for the translation:");
                        currentPrompt = _englishToTargetLanguagePrompt + $"\nAdditional context for translation: {context}";
                        AnsiConsole.MarkupLine("[green]Context added. Retranslating...[/]");
                        continue;
                    case "Input own translation":
                        var userTranslation = AnsiConsole.Ask<string>("Please input your own translation:");
                        if (string.IsNullOrWhiteSpace(userTranslation))
                        {
                            AnsiConsole.MarkupLine("[red]Invalid translation. Please try again.[/]");
                            continue;
                        }

                        return translated.ApplyTranslation(userTranslation);
                    case "Skip":
                        AnsiConsole.MarkupLine("[yellow]Translation skipped.[/]");
                        return translated.ApplyTranslation(string.Empty);
                    case "Stop and save":
                        AnsiConsole.MarkupLine("[yellow]Stopping translation process.[/]");
                        return null;
                }
            }
        }

        private static string CreatePrompt(string sourceLanguage, string targetLanguage)
        {
            return $"""
                    You are a translation service translating from {sourceLanguage} to {targetLanguage}.
                    Return only the translation, not the original text or any other information.
                    If the original text contains punctuation or special characters, it is very important to replicate them. Do not try to correct bad grammar in the original text.

                    E.g., if the original text is "enter **Your-Name* with no-more-than/#five%characters..!"
                    Then a translation to, e.g., Danish should be "Indtast **Dit-Navn* med maksimalt/#fem%bogstaver..!"
                    """;
        }
    }

    private sealed class OpenAiTranslationService
    {
        private readonly ChatClient _client;
        public readonly Gpt4OUsageStatistics UsageStatistics = new();
        public const string ModelName = "gpt-4o";

        private OpenAiTranslationService(string apiKey)
        {
            _client = new ChatClient(ModelName, apiKey);
        }

        public static OpenAiTranslationService Create()
        {
            var apiKey = GetApiKey();
            return new OpenAiTranslationService(apiKey);
        }

        private static string GetApiKey()
        {
            const string apiKeySecretName = "OpenAIApiKey";
            var apiKey = SecretHelper.GetSecret(apiKeySecretName);
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                return apiKey;
            }

            AnsiConsole.MarkupLine("OpenAPI Key is missing.");
            apiKey = AnsiConsole.Ask<string>("[yellow]Please enter your OpenAPI Key.[/]");
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                SecretHelper.SetSecret(apiKeySecretName, apiKey);
                return apiKey;
            }

            throw new InvalidOperationException("Invalid OpenAPI Key provided.");
        }

        public async Task<POSingularEntry> Translate(
            string systemPrompt,
            IReadOnlyCollection<POSingularEntry> existingTranslations,
            POSingularEntry nonTranslatedEntry,
            StatusContext context
        )
        {
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(systemPrompt)
            };

            foreach (var translation in existingTranslations)
            {
                messages.Add(new UserChatMessage(translation.Key.Id));
                messages.Add(new AssistantChatMessage(translation.GetTranslation()));
            }

            messages.Add(new UserChatMessage(nonTranslatedEntry.Key.Id));
            context.Status("Translating (thinking...)");

            StringBuilder content = new();
            var streamingUpdate = _client.CompleteChatStreamingAsync(messages);
            await foreach (var update in streamingUpdate)
            {
                if (update.Usage != null)
                {
                    UsageStatistics.Update(update.Usage);
                }

                content.Append(update?.ContentUpdate.FirstOrDefault()?.Text ?? "");
                var percent = Math.Round(content.Length / (nonTranslatedEntry.Key.Id.Length * 1.2) * 100); // +20% is a guess
                context.Status($"Translating {Math.Min(100, percent)}%");
            }

            context.Status("Translating 100%");

            var translated = content.ToString();
            return nonTranslatedEntry.ApplyTranslation(translated);
        }

        public record Gpt4OUsageStatistics
        {
            private const decimal NonCachedInputPricePerThousandTokens = 0.0025m;
            private const decimal CachedInputPricePerThousandTokens = 0.00125m;
            private const decimal OutputPricePerThousandTokens = 0.01m;

            private int _nonCachedInputTokenCount;
            private int _outputTokenCount;
            private int _cachedInputTokenCount;

            public decimal TotalCost
            {
                get
                {
                    var nonCachedInputCost = (_nonCachedInputTokenCount / 1000m) * NonCachedInputPricePerThousandTokens;
                    var cachedInputPrice = (_cachedInputTokenCount / 1000m) * CachedInputPricePerThousandTokens;
                    var outputCost = (_outputTokenCount / 1000m) * OutputPricePerThousandTokens;

                    return nonCachedInputCost + cachedInputPrice + outputCost;
                }
            }

            public int TotalTokens => _nonCachedInputTokenCount + _outputTokenCount + _cachedInputTokenCount;

            public void Update(ChatTokenUsage usage)
            {
                _cachedInputTokenCount += usage.InputTokenDetails.CachedTokenCount;
                _nonCachedInputTokenCount += (usage.InputTokenCount - usage.InputTokenDetails.CachedTokenCount);
                _outputTokenCount += usage.OutputTokenCount;
            }
        }
    }
}

public static class Extensions
{
    public static string GetTranslation(this POSingularEntry poEntry)
    {
        var translation = poEntry.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(translation))
        {
            throw new InvalidOperationException("No translation was found.");
        }

        return translation;
    }

    public static bool HasTranslation(this POSingularEntry poEntry)
    {
        return !string.IsNullOrWhiteSpace(poEntry.Translation);
    }

    public static POSingularEntry ReverseKeyAndTranslation(this POSingularEntry poEntry)
    {
        var key = new POKey(poEntry.GetTranslation(), null, poEntry.Key.ContextId);
        var entry = new POSingularEntry(key)
        {
            Translation = poEntry.Key.Id,
            Comments = poEntry.Comments
        };

        return entry;
    }

    public static POSingularEntry ApplyTranslation(this POSingularEntry poEntry, string translation)
    {
        return new POSingularEntry(poEntry.Key)
        {
            Translation = translation,
            Comments = poEntry.Comments
        };
    }

    public static IReadOnlyCollection<POSingularEntry> EnsureOnlySingularEntries(this POCatalog catalog)
    {
        if (catalog.Values.Any(x => x is not POSingularEntry))
        {
            throw new NotSupportedException("Only single translations are supported.");
        }

        return catalog.Values.OfType<POSingularEntry>().ToArray();
    }

    public static void UpdateEntry(this POCatalog poCatalog, POSingularEntry translatedEntry)
    {
        var key = translatedEntry.Key;
        var poEntry = poCatalog[key];
        if (poEntry is POSingularEntry)
        {
            var index = poCatalog.IndexOf(poEntry);
            poCatalog.Remove(key);
            poCatalog.Insert(index, translatedEntry);
        }
        else
        {
            throw new InvalidOperationException($"Plural is currently not supported. Key: '{key.Id}'");
        }
    }
}
