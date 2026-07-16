using System.Globalization;
using VoiceDictateDemo.Models;

namespace VoiceDictateDemo.Services;

public static class VoiceFieldSchemaBuilder
{
	public static List<FieldSchemaItem> Build(ViewModels.RegisterProgressViewModel form)
	{
		return
		[
			new FieldSchemaItem
			{
				Name = "ProgressStatus",
				Type = "Options",
				Title = "Progress status",
				Required = true,
				Options = form.ProgressStatusOptions.Select(o => o.Text).ToList()
			},
			new FieldSchemaItem
			{
				Name = "Hours",
				Type = "Hours",
				Title = "Worked hours",
				Required = false
			},
			new FieldSchemaItem
			{
				Name = "Text",
				Type = "String",
				Title = "Activities",
				Required = false
			}
		];
	}
}

public static class VoiceFieldApplier
{
	public static List<string> Apply(ViewModels.RegisterProgressViewModel form, VoiceExtractionResponse response, bool fillEmptyOnly = true)
	{
		var warnings = new List<string>(response.Warnings);

		foreach (var (name, value) in response.Fields)
		{
			switch (name.ToUpperInvariant())
			{
				case "PROGRESSSTATUS":
					if (fillEmptyOnly && !string.IsNullOrWhiteSpace(form.ProgressStatusText))
					{
						warnings.Add("Progress status already filled — skipped.");
						break;
					}
					if (!TryApplyProgressStatus(form, value.OptionTitle, warnings))
						warnings.Add($"Could not match progress status '{value.OptionTitle}'.");
					break;

				case "HOURS":
					if (fillEmptyOnly && form.HoursValue.HasValue)
					{
						warnings.Add("Hours already filled — skipped.");
						break;
					}
					if (!TryApplyHours(form, value.TotalMinutes, warnings))
						warnings.Add("Could not apply hours.");
					break;

				case "TEXT":
					if (fillEmptyOnly && !string.IsNullOrWhiteSpace(form.ActivitiesText))
					{
						warnings.Add("Activities already filled — skipped.");
						break;
					}
					if (!string.IsNullOrWhiteSpace(value.Value))
						form.ActivitiesText = value.Value.Trim();
					break;

				case "IMAGE":
					warnings.Add("Image field is ignored by voice input.");
					break;

				default:
					warnings.Add($"Unknown field '{name}' ignored.");
					break;
			}
		}

		return warnings;
	}

	private static bool TryApplyProgressStatus(ViewModels.RegisterProgressViewModel form, string? optionTitle, List<string> warnings)
	{
		if (string.IsNullOrWhiteSpace(optionTitle))
			return false;

		var match = form.ProgressStatusOptions.FirstOrDefault(o =>
			string.Equals(o.Text, optionTitle.Trim(), StringComparison.OrdinalIgnoreCase));

		if (match is null)
		{
			match = form.ProgressStatusOptions.FirstOrDefault(o =>
				o.Text.Contains(optionTitle.Trim(), StringComparison.OrdinalIgnoreCase)
				|| optionTitle.Trim().Contains(o.Text, StringComparison.OrdinalIgnoreCase));
			if (match is not null)
				warnings.Add($"Used fuzzy match for progress status: '{match.Text}'.");
		}

		if (match is null)
			return false;

		form.SetProgressStatus(match);
		return true;
	}

	private static bool TryApplyHours(ViewModels.RegisterProgressViewModel form, int? totalMinutes, List<string> warnings)
	{
		if (totalMinutes is null || totalMinutes < 0)
			return false;

		var snapped = (int)(Math.Round(totalMinutes.Value / 15.0) * 15);
		if (snapped != totalMinutes)
			warnings.Add($"Hours snapped from {totalMinutes} to {snapped} minutes (15-min grid).");

		var hours = snapped / 60;
		var minutes = snapped % 60;
		form.SetHours(hours + (minutes / 60m), FormatHoursLabel(hours, minutes));
		return true;
	}

	public static string FormatHoursLabel(int hours, int minutes)
	{
		if (hours > 0 && minutes > 0)
			return $"{hours} Hour {minutes} Minutes";
		if (hours > 0)
			return hours == 1 ? "1 Hour" : $"{hours} Hour";
		return $"{minutes} Minutes";
	}

	public static List<DialogOption> GenerateHourOptions()
	{
		var options = new List<DialogOption>();
		for (var hour = 0; hour < 24; hour++)
		{
			for (var minute = 0; minute < 60; minute += 15)
			{
				decimal value = hour + (minute / 60m);
				options.Add(new DialogOption(FormatHoursLabel(hour, minute), value));
			}
		}
		return options;
	}
}
