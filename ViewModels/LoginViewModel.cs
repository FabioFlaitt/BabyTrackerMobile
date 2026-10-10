using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BabyTrackerMobile.Services;

namespace BabyTrackerMobile.ViewModels
{
    public partial class LoginViewModel : ObservableObject
    {
        private readonly FirebaseAuthService _auth;

        public LoginViewModel(FirebaseAuthService auth) => _auth = auth;

        [ObservableProperty]
        private string email = "";

        [ObservableProperty]
        private string password = "";

        [ObservableProperty]
        private bool isBusy;

        [ObservableProperty]
        private string errorText = "";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Title))]
        [NotifyPropertyChangedFor(nameof(ActionText))]
        [NotifyPropertyChangedFor(nameof(ToggleText))]
        private bool isRegisterMode;

        public string Title => IsRegisterMode ? "Criar conta" : "Entrar";
        public string ActionText => IsRegisterMode ? "Criar conta e entrar" : "Entrar";
        public string ToggleText => IsRegisterMode ? "Já tenho conta — entrar" : "Não tenho conta — criar";

        public bool NotConfigured => !FirebaseConfig.IsConfigured;

        [RelayCommand]
        private void ToggleMode()
        {
            IsRegisterMode = !IsRegisterMode;
            ErrorText = "";
        }

        [RelayCommand]
        private async Task SubmitAsync()
        {
            ErrorText = "";
            if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
            {
                ErrorText = "Preencha e-mail e senha.";
                return;
            }

            IsBusy = true;
            try
            {
                var (ok, error) = IsRegisterMode
                    ? await _auth.SignUpAsync(Email.Trim(), Password)
                    : await _auth.SignInAsync(Email.Trim(), Password);

                if (!ok)
                {
                    ErrorText = error ?? "Não foi possível autenticar.";
                    return;
                }

                Password = "";
                await Microsoft.Maui.Controls.Shell.Current.GoToAsync("//dashboard");
                await Microsoft.Maui.Controls.Shell.Current.GoToAsync("chat");
            }
            finally { IsBusy = false; }
        }
    }
}
