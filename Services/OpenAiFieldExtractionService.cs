using System.ClientModel;
using System.Text.Json;
using OpenAI.Chat;
using VoiceDictateDemo.Models;

namespace VoiceDictateDemo.Services;

public sealed class OpenAiFieldExtractionService : IFieldExtractionService
{
	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNameCaseInsensitive = true,
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase
	};

	public async Task<VoiceExtractionResponse> ExtractAsync(VoiceExtractionRequest request, CancellationToken cancellationToken = default)
	{
		await EnvConfig.EnsureLoadedAsync().ConfigureAwait(false);

		if (!EnvConfig.HasApiKey)
		{
			var detail = string.IsNullOrWhiteSpace(EnvConfig.LastLoadError)
				? "Set OPENAI_API_KEY in Resources/Raw/openai.env and rebuild."
				: EnvConfig.LastLoadError;
			throw new InvalidOperationException($"OpenAI API key is missing. {detail}");
		}

		var client = new ChatClient(EnvConfig.OpenAiModel, EnvConfig.OpenAiApiKey);

		var schemaJson = JsonSerializer.Serialize(request.Fields, JsonOptions);
		var systemPrompt =
			"""
			You extract structured field values for a job hour-registration form from a technician transcript.
			Return ONLY valid JSON matching this shape:
			{
			  "fields": {
			    "ProgressStatus": { "optionTitle": "..." },
			    "Hours": { "totalMinutes": 120 },
			    "Text": { "value": "..." }
			  },
			  "warnings": ["optional messages"]
			}
			Rules:
			- Only use field names from the provided schema.
			- Never fill Image or photo fields.
			- For Options fields, optionTitle MUST be one of the allowed option titles (exact match preferred).
			- For Hours, return totalMinutes as an integer (e.g. 2 hours = 120).
			- For String fields, put cleaned work description in value.
			- Omit fields you cannot confidently extract.
			- Do not invent IDs.
			""";

		var userPrompt =
			$"""
			Field schema:
			{schemaJson}

			Transcript:
			{request.Transcript}
			""";

		var messages = new List<ChatMessage>
		{
			new SystemChatMessage(systemPrompt),
			new UserChatMessage(userPrompt)
		};

		var options = new ChatCompletionOptions
		{
			ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat()
		};

		ClientResult<ChatCompletion> result = await client.CompleteChatAsync(messages, options, cancellationToken);
		var text = string.Join('\n', result.Value.Content.Select(c => c.Text)).Trim();

		var response = JsonSerializer.Deserialize<VoiceExtractionResponse>(text, JsonOptions)
			?? new VoiceExtractionResponse();

		response.Fields ??= new Dictionary<string, VoiceFieldValue>(StringComparer.OrdinalIgnoreCase);
		response.Warnings ??= [];
		return response;
	}
}
