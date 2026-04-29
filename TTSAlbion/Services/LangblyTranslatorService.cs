using TTSAlbion.Interfaces;

namespace TTSAlbion.Services;

using System.Net.Http;
using System.Net.Http.Json;

public class LangblyTranslatorService : ITranslatorService
{
    private readonly HttpClient _httpClient;
    private readonly TranslatorOptions _options;

    public LangblyTranslatorService(HttpClient httpClient, TranslatorOptions options)
    {
        _httpClient = httpClient;
        _options = options;
    }

    public async Task<string> TranslateAsync(string message, string sourceLang, string targetLang, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.BaseUrl))
            throw new InvalidOperationException("La URL base de Langbly no está configurada.");

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
            throw new InvalidOperationException("La API key de Langbly no está configurada.");

        var request = new TranslateRequest
        {
            Q = message,
            Source = sourceLang,
            Target = targetLang
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl.TrimEnd('/')}/language/translate/v2")
        {
            Content = JsonContent.Create(request)
        };

        httpRequest.Headers.Add("X-API-Key", _options.ApiKey);

        var response = await _httpClient.SendAsync(httpRequest, ct);

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<TranslateResponse>(cancellationToken: ct);

        return result?.Data?.Translations?.FirstOrDefault()?.TranslatedText
               ?? throw new InvalidOperationException("Respuesta inválida del servicio de traducción.");
    }
}
