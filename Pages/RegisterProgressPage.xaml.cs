using VoiceDictateDemo.ViewModels;

namespace VoiceDictateDemo.Pages;

public partial class RegisterProgressPage : ContentPage
{
	public RegisterProgressPage(RegisterProgressViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
	}
}
