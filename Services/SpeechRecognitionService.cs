using System.Globalization;
using System.Text;
using CommunityToolkit.Maui.Media;

namespace VoiceDictateDemo.Services;

/// <summary>
/// Prefers offline OS STT; if StartListenAsync fails, retries once with online OS STT.
/// Online STT is the platform speech stack (e.g. Google / Apple), not OpenAI Whisper.
/// </summary>
public sealed class SpeechRecognitionService : ISpeechRecognitionService
{
	private readonly ISpeechToText _offline = OfflineSpeechToText.Default;
	private readonly ISpeechToText _online = SpeechToText.Default;
	private readonly StringBuilder _transcript = new();

	private ISpeechToText _active;
	private bool _listening;

	public SpeechRecognitionService()
	{
		_active = _offline;
	}

	public bool IsListening => _listening;

	/// <summary>True when the active engine is SpeechToText.Default (online/OS cloud-backed).</summary>
	public bool IsUsingOnlineStt => ReferenceEquals(_active, _online);

	public string ActiveEngineName => IsUsingOnlineStt ? "Online (OS)" : "Offline (on-device)";

	public event EventHandler<string>? PartialTranscriptReceived;
	public event EventHandler<string>? FinalTranscriptReceived;

	public async Task<bool> EnsurePermissionsAsync(CancellationToken cancellationToken = default)
	{
		var mic = await Permissions.RequestAsync<Permissions.Microphone>();
		// Request on the offline engine first; online uses the same OS permissions on Android/iOS.
		var speech = await _active.RequestPermissions(cancellationToken);
		if (!speech)
			speech = await _online.RequestPermissions(cancellationToken);

		return mic == PermissionStatus.Granted && speech;
	}

	public async Task StartListeningAsync(CultureInfo culture, CancellationToken cancellationToken = default)
	{
		if (_listening)
			return;

		_transcript.Clear();

		var options = new SpeechToTextOptions
		{
			Culture = culture,
			ShouldReportPartialResults = true
		};

		// 1) Prefer offline
		_active = _offline;
		try
		{
			await StartWithEngineAsync(_active, options, cancellationToken);
			return;
		}
		catch (Exception offlineEx)
		{
			await SafeStopAsync(_offline, cancellationToken);
			Unsubscribe(_offline);

			// 2) Fallback to online OS STT
			_active = _online;
			try
			{
				await StartWithEngineAsync(_active, options, cancellationToken);
			}
			catch (Exception onlineEx)
			{
				Unsubscribe(_online);
				_listening = false;
				throw new InvalidOperationException(
					$"Speech recognition failed. Offline: {offlineEx.Message}. Online: {onlineEx.Message}",
					onlineEx);
			}
		}
	}

	public async Task<string> StopListeningAsync(CancellationToken cancellationToken = default)
	{
		if (_listening)
		{
			await SafeStopAsync(_active, cancellationToken);
			_listening = false;
		}

		Unsubscribe(_active);
		return _transcript.ToString().Trim();
	}

	public async Task CancelListeningAsync(CancellationToken cancellationToken = default)
	{
		if (_listening)
		{
			await SafeStopAsync(_active, cancellationToken);
			_listening = false;
		}

		Unsubscribe(_active);
		_transcript.Clear();
	}

	private async Task StartWithEngineAsync(ISpeechToText engine, SpeechToTextOptions options, CancellationToken cancellationToken)
	{
		engine.RecognitionResultUpdated += OnPartial;
		engine.RecognitionResultCompleted += OnCompleted;
		await engine.StartListenAsync(options, cancellationToken);
		_listening = true;
	}

	private static async Task SafeStopAsync(ISpeechToText engine, CancellationToken cancellationToken)
	{
		try
		{
			await engine.StopListenAsync(cancellationToken);
		}
		catch
		{
			// ignore stop errors during fallback / cancel
		}
	}

	private void OnPartial(object? sender, SpeechToTextRecognitionResultUpdatedEventArgs e)
	{
		PartialTranscriptReceived?.Invoke(this, e.RecognitionResult);
	}

	private void OnCompleted(object? sender, SpeechToTextRecognitionResultCompletedEventArgs e)
	{
		if (e.RecognitionResult.IsSuccessful && !string.IsNullOrWhiteSpace(e.RecognitionResult.Text))
		{
			_transcript.Clear();
			_transcript.Append(e.RecognitionResult.Text);
			FinalTranscriptReceived?.Invoke(this, e.RecognitionResult.Text);
		}
	}

	private void Unsubscribe(ISpeechToText engine)
	{
		engine.RecognitionResultUpdated -= OnPartial;
		engine.RecognitionResultCompleted -= OnCompleted;
	}
}
