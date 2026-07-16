using VoiceDictateDemo.Pages;

namespace VoiceDictateDemo;

public partial class AppShell : Shell
{
	public AppShell(RegisterProgressPage page)
	{
		InitializeComponent();

		Items.Clear();
		Items.Add(new ShellContent
		{
			Title = "Register progress",
			Route = "RegisterProgress",
			Content = page
		});
	}
}
