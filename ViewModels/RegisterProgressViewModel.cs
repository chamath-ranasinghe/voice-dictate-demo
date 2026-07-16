using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VoiceDictateDemo.Models;
using VoiceDictateDemo.Services;

namespace VoiceDictateDemo.ViewModels;

public partial class RegisterProgressViewModel : ObservableObject
{
	private readonly ISpeechRecognitionService _speech;
	private readonly IFieldExtractionService _extraction;

	public RegisterProgressViewModel(ISpeechRecognitionService speech, IFieldExtractionService extraction)
	{
		_speech = speech;
		_extraction = extraction;

		ProgressStatusOptions =
		[
			new DialogOption("Open", "open"),
			new DialogOption("In progress", "in_progress"),
			new DialogOption("Finished", "finished"),
			new DialogOption("Approved", "approved")
		];

		HourOptions = VoiceFieldApplier.GenerateHourOptions();

		_speech.PartialTranscriptReceived += (_, text) =>
			MainThread.BeginInvokeOnMainThread(() => PartialTranscript = text);
		_speech.FinalTranscriptReceived += (_, text) =>
			MainThread.BeginInvokeOnMainThread(() =>
			{
				if (!string.IsNullOrWhiteSpace(text))
					ConfirmedTranscript = text;
			});
	}

	public string Title => "Register progress";
	public string JobSubtitle => "Current status: Open - (Open)";

	public List<DialogOption> ProgressStatusOptions { get; }
	public List<DialogOption> HourOptions { get; }

	[ObservableProperty]
	public partial VoiceUiState VoiceState { get; set; } = VoiceUiState.Idle;

	[ObservableProperty]
	public partial string PartialTranscript { get; set; } = string.Empty;

	[ObservableProperty]
	public partial string ConfirmedTranscript { get; set; } = string.Empty;

	[ObservableProperty]
	public partial string StatusMessage { get; set; } = string.Empty;

	[ObservableProperty]
	public partial string ProgressStatusText { get; set; } = string.Empty;

	[ObservableProperty]
	public partial string? ProgressStatusId { get; set; }

	[ObservableProperty]
	public partial string HoursText { get; set; } = string.Empty;

	[ObservableProperty]
	public partial decimal? HoursValue { get; set; }

	[ObservableProperty]
	public partial string ActivitiesText { get; set; } = string.Empty;

	[ObservableProperty]
	public partial string PhotoLabel { get; set; } = "Select photo";

	[ObservableProperty]
	public partial bool HasPhoto { get; set; }

	public bool IsListening => VoiceState == VoiceUiState.Listening;
	public bool IsAwaitingConfirm => VoiceState == VoiceUiState.AwaitingConfirm;
	public bool IsProcessing => VoiceState == VoiceUiState.Processing;
	public bool ShowVoiceBanner => VoiceState is VoiceUiState.Listening or VoiceUiState.AwaitingConfirm or VoiceUiState.Processing;
	public bool CanConfirmTranscript => !string.IsNullOrWhiteSpace(ConfirmedTranscript) && VoiceState == VoiceUiState.AwaitingConfirm;
	public bool CanUseMic => VoiceState is VoiceUiState.Idle or VoiceUiState.Listening;

	public string MicToolbarText => IsListening ? "Stop" : "Mic";
	public Color MicToolbarColor => IsListening ? Color.FromArgb("#DC3545") : Color.FromArgb("#0077C8");

	public bool AllPropertiesAreValid => !string.IsNullOrWhiteSpace(ProgressStatusText);

	partial void OnVoiceStateChanged(VoiceUiState value)
	{
		OnPropertyChanged(nameof(IsListening));
		OnPropertyChanged(nameof(IsAwaitingConfirm));
		OnPropertyChanged(nameof(IsProcessing));
		OnPropertyChanged(nameof(ShowVoiceBanner));
		OnPropertyChanged(nameof(CanConfirmTranscript));
		OnPropertyChanged(nameof(CanUseMic));
		OnPropertyChanged(nameof(MicToolbarText));
		OnPropertyChanged(nameof(MicToolbarColor));
	}

	partial void OnConfirmedTranscriptChanged(string value)
	{
		OnPropertyChanged(nameof(CanConfirmTranscript));
	}

	partial void OnProgressStatusTextChanged(string value)
	{
		OnPropertyChanged(nameof(AllPropertiesAreValid));
	}

	public void SetProgressStatus(DialogOption option)
	{
		ProgressStatusText = option.Text;
		ProgressStatusId = option.Value?.ToString();
	}

	public void SetHours(decimal value, string label)
	{
		HoursValue = value;
		HoursText = label;
	}

	[RelayCommand]
	private async Task PickProgressStatusAsync()
	{
		var choice = await Shell.Current.DisplayActionSheetAsync(
			"Progress status",
			"Cancel",
			null,
			ProgressStatusOptions.Select(o => o.Text).ToArray());

		if (string.IsNullOrEmpty(choice) || choice == "Cancel")
			return;

		var option = ProgressStatusOptions.First(o => o.Text == choice);
		SetProgressStatus(option);
	}

	[RelayCommand]
	private async Task PickHoursAsync()
	{
		// Show a practical subset for the demo action sheet (0–8h)
		var subset = HourOptions
			.Where(o => o.Value is decimal d && d <= 8m)
			.Select(o => o.Text)
			.ToArray();

		var choice = await Shell.Current.DisplayActionSheetAsync("Worked hours", "Cancel", null, subset);
		if (string.IsNullOrEmpty(choice) || choice == "Cancel")
			return;

		var option = HourOptions.First(o => o.Text == choice);
		SetHours((decimal)option.Value!, option.Text);
	}

	[RelayCommand]
	private void TogglePhoto()
	{
		HasPhoto = !HasPhoto;
		PhotoLabel = HasPhoto ? "demo-photo.jpg (tap to clear)" : "Select photo";
	}

	[RelayCommand]
	private async Task ToggleVoiceAsync()
	{
		try
		{
			if (VoiceState == VoiceUiState.Listening)
			{
				var transcript = await _speech.StopListeningAsync();
				ConfirmedTranscript = string.IsNullOrWhiteSpace(transcript) ? ConfirmedTranscript : transcript;
				PartialTranscript = string.Empty;
				VoiceState = VoiceUiState.AwaitingConfirm;
				StatusMessage = string.IsNullOrWhiteSpace(ConfirmedTranscript)
					? "No speech detected. Edit the text or re-record."
					: "Review the transcript, then Confirm to fill fields.";
				return;
			}

			if (VoiceState is VoiceUiState.AwaitingConfirm or VoiceUiState.Processing)
				return;

			if (!await _speech.EnsurePermissionsAsync())
			{
				StatusMessage = "Microphone / speech permission denied. You can still fill fields manually.";
				return;
			}

			PartialTranscript = string.Empty;
			ConfirmedTranscript = string.Empty;
			VoiceState = VoiceUiState.Listening;
			await _speech.StartListeningAsync(CultureInfo.GetCultureInfo("en-US"));

			var engine = _speech is SpeechRecognitionService srs ? srs.ActiveEngineName : "STT";
			StatusMessage = $"Listening ({engine})… tap the mic again to stop.";
		}
		catch (Exception ex)
		{
			VoiceState = VoiceUiState.Idle;
			StatusMessage = $"Speech error: {ex.Message}";
		}
	}

	[RelayCommand]
	private async Task ConfirmTranscriptAsync()
	{
		if (string.IsNullOrWhiteSpace(ConfirmedTranscript))
		{
			StatusMessage = "Transcript is empty. Re-record or type something first.";
			return;
		}

		VoiceState = VoiceUiState.Processing;
		StatusMessage = "Extracting fields…";

		try
		{
			await EnvConfig.EnsureLoadedAsync();
			if (!EnvConfig.HasApiKey)
			{
				VoiceState = VoiceUiState.AwaitingConfirm;
				StatusMessage = $"OpenAI API key is missing. {EnvConfig.LastLoadError ?? "Put OPENAI_API_KEY in Resources/Raw/openai.env and rebuild."}";
				return;
			}

			var schema = VoiceFieldSchemaBuilder.Build(this);
			var response = await _extraction.ExtractAsync(new VoiceExtractionRequest
			{
				Transcript = ConfirmedTranscript.Trim(),
				Fields = schema
			});

			var warnings = VoiceFieldApplier.Apply(this, response, fillEmptyOnly: true);
			VoiceState = VoiceUiState.Idle;

			StatusMessage = warnings.Count == 0
				? "Fields updated. Review and tap OK."
				: "Fields updated with notes: " + string.Join(" ", warnings);
		}
		catch (Exception ex)
		{
			VoiceState = VoiceUiState.AwaitingConfirm;
			StatusMessage = $"Extraction failed: {ex.Message}";
		}
	}

	[RelayCommand]
	private void CancelTranscript()
	{
		ConfirmedTranscript = string.Empty;
		PartialTranscript = string.Empty;
		VoiceState = VoiceUiState.Idle;
		StatusMessage = "Voice input cancelled.";
	}

	[RelayCommand]
	private async Task ReRecordAsync()
	{
		ConfirmedTranscript = string.Empty;
		PartialTranscript = string.Empty;
		VoiceState = VoiceUiState.Idle;
		await ToggleVoiceAsync();
	}

	[RelayCommand]
	private async Task OkAsync()
	{
		if (!AllPropertiesAreValid)
		{
			await Shell.Current.DisplayAlertAsync("Validation", "Progress status is required.", "OK");
			return;
		}

		await Shell.Current.DisplayAlertAsync(
			"Submitted (demo)",
			$"Progress: {ProgressStatusText}\nHours: {HoursText}\nActivities: {ActivitiesText}\nPhoto: {(HasPhoto ? "set (ignored by voice)" : "none")}",
			"OK");
	}

	[RelayCommand]
	private async Task CloseAsync()
	{
		await _speech.CancelListeningAsync();
		VoiceState = VoiceUiState.Idle;
		await Shell.Current.DisplayAlertAsync("Close", "Demo dialog closed (no navigation stack).", "OK");
	}
}
