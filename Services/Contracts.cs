using System.Globalization;
using System.Text;
using VoiceDictateDemo.Models;

namespace VoiceDictateDemo.Services;

public static class EnvConfig
{
	private static readonly SemaphoreSlim LoadLock = new(1, 1);
	private static bool _loaded;

	public static string OpenAiApiKey { get; private set; } = string.Empty;
	public static string OpenAiModel { get; private set; } = "gpt-4o-mini";
	public static string? LastLoadError { get; private set; }

	/// <summary>
	/// Loads OPENAI_* settings from packaged openai.env (preferred) or legacy .env.
	/// Safe to call multiple times; awaited before Confirm so the key is ready.
	/// </summary>
	public static async Task EnsureLoadedAsync()
	{
		if (_loaded && HasApiKey)
			return;

		await LoadLock.WaitAsync().ConfigureAwait(false);
		try
		{
			if (_loaded && HasApiKey)
				return;

			LastLoadError = null;
			OpenAiApiKey = string.Empty;

			var loadedFrom = await TryLoadFromPackageAsync("openai.env").ConfigureAwait(false)
				?? await TryLoadFromPackageAsync(".env").ConfigureAwait(false);

			if (loadedFrom is null)
			{
				var rootCandidates = new[]
				{
					Path.Combine(FileSystem.AppDataDirectory, "openai.env"),
					Path.Combine(FileSystem.AppDataDirectory, ".env"),
					Path.Combine(AppContext.BaseDirectory, "openai.env"),
					Path.Combine(AppContext.BaseDirectory, ".env"),
				};

				foreach (var path in rootCandidates)
				{
					if (!File.Exists(path))
						continue;
					ApplyLines(await File.ReadAllTextAsync(path).ConfigureAwait(false));
					loadedFrom = path;
					break;
				}
			}

			_loaded = true;

			if (!HasApiKey)
			{
				LastLoadError = loadedFrom is null
					? "Could not open openai.env from the app package. Rebuild after adding Resources/Raw/openai.env."
					: $"Loaded '{loadedFrom}' but OPENAI_API_KEY is missing or still a placeholder.";
			}
		}
		finally
		{
			LoadLock.Release();
		}
	}

	private static async Task<string?> TryLoadFromPackageAsync(string fileName)
	{
		try
		{
			await using var stream = await FileSystem.OpenAppPackageFileAsync(fileName).ConfigureAwait(false);
			using var reader = new StreamReader(stream, Encoding.UTF8);
			var content = await reader.ReadToEndAsync().ConfigureAwait(false);
			ApplyLines(content);
			return fileName;
		}
		catch (Exception ex)
		{
			LastLoadError = $"OpenAppPackageFileAsync('{fileName}'): {ex.Message}";
			return null;
		}
	}

	private static void ApplyLines(string content)
	{
		// Strip UTF-8 BOM if present
		if (content.Length > 0 && content[0] == '\uFEFF')
			content = content[1..];

		foreach (var rawLine in content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
		{
			var line = rawLine.Trim();
			if (line.Length == 0 || line.StartsWith('#') || !line.Contains('='))
				continue;

			var idx = line.IndexOf('=');
			var key = line[..idx].Trim();
			var value = line[(idx + 1)..].Trim().Trim('"').Trim('\'');

			if (key.Equals("OPENAI_API_KEY", StringComparison.OrdinalIgnoreCase))
				OpenAiApiKey = value;
			else if (key.Equals("OPENAI_MODEL", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(value))
				OpenAiModel = value;
		}
	}

	public static bool HasApiKey =>
		!string.IsNullOrWhiteSpace(OpenAiApiKey)
		&& !OpenAiApiKey.Contains("YOUR_KEY", StringComparison.OrdinalIgnoreCase)
		&& OpenAiApiKey is not ("sk-..." or "CHANGEME");
}

public interface ISpeechRecognitionService
{
	bool IsListening { get; }
	event EventHandler<string>? PartialTranscriptReceived;
	event EventHandler<string>? FinalTranscriptReceived;

	Task<bool> EnsurePermissionsAsync(CancellationToken cancellationToken = default);
	Task StartListeningAsync(CultureInfo culture, CancellationToken cancellationToken = default);
	Task<string> StopListeningAsync(CancellationToken cancellationToken = default);
	Task CancelListeningAsync(CancellationToken cancellationToken = default);
}

public interface IFieldExtractionService
{
	Task<VoiceExtractionResponse> ExtractAsync(VoiceExtractionRequest request, CancellationToken cancellationToken = default);
}
