using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BabyTrackerMobile.Data;
using BabyTrackerMobile.Models;

namespace BabyTrackerMobile.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        [ObservableProperty]
        private int babyId = 0;

        [ObservableProperty]
        private string babyName = "";

        [ObservableProperty]
        private DateTime birthDate = DateTime.Today;

        [ObservableProperty]
        private double birthWeightGrams = 3000;

        [ObservableProperty]
        private FeedingType selectedFeedingType = FeedingType.Breast;

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        private bool hasBaby;

        public List<string> FeedingTypeOptions { get; } = new List<string> { "Amamentação", "Fórmula", "Misto" };

        private int selectedFeedingTypeIndex = 0;
        public int SelectedFeedingTypeIndex
        {
            get => selectedFeedingTypeIndex;
            set
            {
                if (SetProperty(ref selectedFeedingTypeIndex, value))
                {
                    SelectedFeedingType = (FeedingType)value;
                }
            }
        }

        [RelayCommand]
        public async Task LoadAsync()
        {
            IsLoading = true;
            try
            {
                var baby = await DatabaseService.GetFirstBabyAsync();
                if (baby != null)
                {
                    HasBaby = true;
                    BabyId = baby.Id;
                    BabyName = baby.Name;
                    BirthDate = baby.BirthDate;
                    BirthWeightGrams = baby.BirthWeightGrams;
                    SelectedFeedingType = baby.FeedingType;
                    SelectedFeedingTypeIndex = (int)baby.FeedingType;
                }
                else
                {
                    HasBaby = false;
                }
            }
            finally { IsLoading = false; }
        }

        [RelayCommand]
        public async Task SaveBabyAsync()
        {
            if (string.IsNullOrWhiteSpace(BabyName))
            {
                await Microsoft.Maui.Controls.Shell.Current.DisplayAlert("Erro", "O nome do bebê é obrigatório.", "OK");
                return;
            }

            var baby = new Baby
            {
                Id = BabyId,
                Name = BabyName,
                BirthDate = BirthDate,
                BirthWeightGrams = BirthWeightGrams,
                FeedingType = SelectedFeedingType
            };

            await DatabaseService.SaveBabyAsync(baby);

            HasBaby = true;
            await Microsoft.Maui.Controls.Shell.Current.DisplayAlert("Sucesso", "❤️ Dados do bebê salvos!", "OK");
            
            // Reload to ensure ID is set if it was newly created
            await LoadAsync();
        }
    }
}
