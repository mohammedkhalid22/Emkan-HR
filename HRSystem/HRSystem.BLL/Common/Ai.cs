namespace HRSystem.BLL.Common;

// A file sent along with the prompt (a CV as PDF or image)
public record AiFile(byte[] Content, string MimeType);

// Implemented in the Web layer for the provider chosen in appsettings ("Ai" section),
// so switching provider never touches the services
public interface IAiClient
{
    // False until an API key is set
    bool IsConfigured { get; }

    // Shown next to saved results ("gemini-3.1-flash-lite")
    string ModelName { get; }

    // Asks for a JSON answer. On failure the error is a message the user can act on.
    Task<ServiceResult<string>> GenerateJsonAsync(string instructions, string prompt, AiFile? file, CancellationToken cancellationToken = default);
}

public class AiOptions
{
    public const string Section = "Ai";

    public string Provider { get; set; } = "Gemini";
    public string? ApiKey { get; set; }
    public string Model { get; set; } = "gemini-3.1-flash-lite";

    // Tried in order when the main model is busy or gone
    public string[] FallbackModels { get; set; } = [];
}
