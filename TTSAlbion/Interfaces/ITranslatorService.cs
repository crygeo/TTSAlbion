using System.Text.Json.Serialization;

namespace TTSAlbion.Interfaces;

public interface ITranslatorService
{
    Task<string> TranslateAsync(string message, string sourceLang, string targetLang, CancellationToken ct = default);
}
public class TranslatorOptions
{
    public string BaseUrl { get; init; } = string.Empty;
    public string ApiKey { get; init; } = string.Empty;
}

// Request
public class TranslateRequest
{
    [JsonPropertyName("q")]
    public string Q { get; set; } = string.Empty;

    [JsonPropertyName("source")]
    public string Source { get; set; } = string.Empty;

    [JsonPropertyName("target")]
    public string Target { get; set; } = string.Empty;
}

// Response
public class TranslateResponse
{
    [JsonPropertyName("data")]
    public TranslateData Data { get; set; } = new();
}

public class TranslateData
{
    [JsonPropertyName("translations")]
    public List<TranslationItem> Translations { get; set; } = new();
}

public class TranslationItem
{
    [JsonPropertyName("translatedText")]
    public string TranslatedText { get; set; } = string.Empty;

    [JsonPropertyName("detectedSourceLanguage")]
    public string DetectedSourceLanguage { get; set; } = string.Empty;
}