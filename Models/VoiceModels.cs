namespace VoiceDictateDemo.Models;

public enum VoiceUiState
{
	Idle,
	Listening,
	AwaitingConfirm,
	Processing
}

public sealed class DialogOption(string text, object? value)
{
	public string Text { get; } = text;
	public object? Value { get; } = value;
}

public sealed class FieldSchemaItem
{
	public required string Name { get; init; }
	public required string Type { get; init; }
	public required string Title { get; init; }
	public bool Required { get; init; }
	public List<string>? Options { get; init; }
}

public sealed class VoiceExtractionRequest
{
	public required string Transcript { get; init; }
	public required List<FieldSchemaItem> Fields { get; init; }
}

public sealed class VoiceExtractionResponse
{
	public Dictionary<string, VoiceFieldValue> Fields { get; set; } = new(StringComparer.OrdinalIgnoreCase);
	public List<string> Warnings { get; set; } = [];
}

public sealed class VoiceFieldValue
{
	public string? OptionTitle { get; set; }
	public int? TotalMinutes { get; set; }
	public string? Value { get; set; }
}
