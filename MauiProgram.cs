using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using VoiceDictateDemo.Pages;
using VoiceDictateDemo.Services;
using VoiceDictateDemo.ViewModels;

namespace VoiceDictateDemo;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.UseMauiCommunityToolkit()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		// SpeechRecognitionService owns Offline → Online fallback at listen time.
		builder.Services.AddSingleton<ISpeechRecognitionService, SpeechRecognitionService>();
		builder.Services.AddSingleton<IFieldExtractionService, OpenAiFieldExtractionService>();
		builder.Services.AddTransient<RegisterProgressViewModel>();
		builder.Services.AddTransient<RegisterProgressPage>();
		builder.Services.AddSingleton<AppShell>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
