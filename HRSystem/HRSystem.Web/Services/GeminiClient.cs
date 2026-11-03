using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using HRSystem.BLL.Common;
using Microsoft.Extensions.Options;

namespace HRSystem.Web.Services;

// Google Gemini over its REST API (generateContent). The key comes from appsettings "Ai:ApiKey".
public class GeminiClient : IAiClient
{
    private const string Endpoint = "https://generativelanguage.googleapis.com/v1beta/models/{0}:generateContent";

    private readonly HttpClient _http;
    private readonly IOptionsMonitor<AiOptions> _monitor;
    private readonly ILogger<GeminiClient> _logger;

    public GeminiClient(HttpClient http, IOptionsMonitor<AiOptions> options, ILogger<GeminiClient> logger)
    {
        _http = http;
        _monitor = options;
        _logger = logger;
    }

    // Read on every call, so a new key or model in appsettings works without restarting
    private AiOptions _options => _monitor.CurrentValue;

    // The template file ships with a "PASTE_..." placeholder
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.ApiKey) && !_options.ApiKey.StartsWith("PASTE_");

    public string ModelName => _options.Model;

    public async Task<ServiceResult<string>> GenerateJsonAsync(string instructions, string prompt, AiFile? file, CancellationToken cancellationToken = default)
    {
        var parts = new JsonArray();
        if (file is not null)
            parts.Add(new JsonObject { ["inlineData"] = new JsonObject { ["mimeType"] = file.MimeType, ["data"] = Convert.ToBase64String(file.Content) } });
        parts.Add(new JsonObject { ["text"] = prompt });

        var body = new JsonObject
        {
            ["systemInstruction"] = new JsonObject { ["parts"] = new JsonArray(new JsonObject { ["text"] = instructions }) },
            ["contents"] = new JsonArray(new JsonObject { ["role"] = "user", ["parts"] = parts }),
            ["generationConfig"] = new JsonObject { ["responseMimeType"] = "application/json", ["temperature"] = 0.2 }
        };

        var json = body.ToJsonString();

        // Free-tier models are often "busy" (503) for a few seconds: wait and retry,
        // then try the backup models before giving up
        var models = new[] { _options.Model }.Concat(_options.FallbackModels).Distinct().ToList();
        ServiceResult<string> result = ServiceResult<string>.Failure("خدمة الذكاء الاصطناعي مشغولة دلوقتي. جرّب كمان شوية");

        try
        {
            foreach (var model in models)
            {
                for (var attempt = 1; attempt <= 2; attempt++)
                {
                    var (status, text) = await SendAsync(model, json, cancellationToken);
                    if (status == HttpStatusCode.OK)
                        return ReadAnswer(text);

                    _logger.LogWarning("Gemini {Model} returned {Status}: {Body}", model, (int)status, Trim(text));
                    result = ServiceResult<string>.Failure(ErrorMessage(status, text));

                    if (!IsBusy(status))
                    {
                        // A missing model can still be replaced by a backup; anything else (bad key, bad file) can't
                        if (status != HttpStatusCode.NotFound) return result;
                        break;
                    }
                    if (attempt == 1)
                        await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
                }
            }
            return result;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ServiceResult<string>.Failure("خدمة الذكاء الاصطناعي اتأخرت في الرد. جرّب تاني");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Could not reach Gemini");
            return ServiceResult<string>.Failure("مش قادرين نوصل لخدمة الذكاء الاصطناعي. اتأكد من الإنترنت وجرّب تاني");
        }
    }

    private async Task<(HttpStatusCode Status, string Body)> SendAsync(string model, string json, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, string.Format(Endpoint, Uri.EscapeDataString(model)))
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("x-goog-api-key", _options.ApiKey);

        using var response = await _http.SendAsync(request, cancellationToken);
        return (response.StatusCode, await response.Content.ReadAsStringAsync(cancellationToken));
    }

    private static bool IsBusy(HttpStatusCode status)
        => status is HttpStatusCode.ServiceUnavailable or HttpStatusCode.TooManyRequests
            or HttpStatusCode.InternalServerError or HttpStatusCode.GatewayTimeout;

    private ServiceResult<string> ReadAnswer(string json)
    {
        var root = JsonNode.Parse(json);
        var candidate = root?["candidates"]?[0];
        if (candidate is null)
        {
            var blocked = root?["promptFeedback"]?["blockReason"]?.GetValue<string>();
            _logger.LogWarning("Gemini gave no answer (block reason: {Reason})", blocked);
            return ServiceResult<string>.Failure("خدمة الذكاء الاصطناعي رفضت تقرا الملف ده");
        }

        var answer = string.Concat(candidate["content"]?["parts"]?.AsArray()
            .Select(p => p?["text"]?.GetValue<string>() ?? "") ?? []);

        if (string.IsNullOrWhiteSpace(answer))
        {
            _logger.LogWarning("Gemini answer was empty (finish reason: {Reason})", candidate["finishReason"]?.GetValue<string>());
            return ServiceResult<string>.Failure("خدمة الذكاء الاصطناعي رجّعت رد فاضي. جرّب تاني");
        }
        return ServiceResult<string>.Success(answer);
    }

    private static string ErrorMessage(HttpStatusCode status, string body) => status switch
    {
        HttpStatusCode.BadRequest when body.Contains("API key", StringComparison.OrdinalIgnoreCase)
            => "مفتاح الـ API بتاع Gemini غلط. راجعه في الإعدادات",
        HttpStatusCode.BadRequest => "خدمة الذكاء الاصطناعي مقبلتش الملف. جرّب PDF تاني أو صورة أوضح",
        HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "مفتاح الـ API مش مسموح له يستخدم الخدمة دي",
        HttpStatusCode.NotFound => "اسم الموديل في الإعدادات (Ai:Model) مش موجود عند Gemini",
        HttpStatusCode.TooManyRequests => "وصلت للحد المسموح في الخطة المجانية. استنى دقيقة وجرّب تاني",
        _ => "خدمة الذكاء الاصطناعي مشغولة دلوقتي. جرّب كمان شوية"
    };

    private static string Trim(string text) => text.Length > 500 ? text[..500] : text;
}
