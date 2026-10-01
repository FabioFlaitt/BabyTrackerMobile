using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BabyTrackerMobile.Data;
using BabyTrackerMobile.Models;

namespace BabyTrackerMobile.ViewModels
{
    public partial class FeedingViewModel : ObservableObject
    {
        [ObservableProperty]
        private Baby? currentBaby;

        [ObservableProperty]
        private DateTime feedingTime = DateTime.Now;

        [ObservableProperty]
        private int durationMinutes = 15;

        [ObservableProperty]
        private BreastSide selectedSide = BreastSide.Left;

        [ObservableProperty]
        private double bottleAmountMl = 30;

        [ObservableProperty]
        private string notes = "";

        [ObservableProperty]
        private bool isLoading;

        public bool IsBottleFeeding => SelectedSide == BreastSide.Bottle;

        public List<string> SideOptions { get; } = new List<string> { "Esquerdo", "Direito", "Ambos", "Mamadeira" };

        private int selectedSideIndex = 0;
        public int SelectedSideIndex
        {
            get => selectedSideIndex;
            set
            {
                if (SetProperty(ref selectedSideIndex, value))
                {
                    SelectedSide = (BreastSide)value;
                    OnPropertyChanged(nameof(IsBottleFeeding));
                }
            }
        }

        [RelayCommand]
        public async Task LoadAsync()
        {
            IsLoading = true;
            try
            {
                CurrentBaby = await DatabaseService.GetFirstBabyAsync();
            }
            finally { IsLoading = false; }
        }

        [RelayCommand]
        public async Task RegisterFeedingAsync()
        {
            if (CurrentBaby == null) return;

            var record = new FeedingRecord
            {
                BabyId = CurrentBaby.Id,
                StartTime = FeedingTime,
                DurationMinutes = DurationMinutes,
                Side = SelectedSide,
                BottleAmountMl = IsBottleFeeding ? BottleAmountMl : null,
                Notes = Notes
            };

            await DatabaseService.AddFeedingAsync(record);

            // Reset form
            FeedingTime = DateTime.Now;
            DurationMinutes = 15;
            SelectedSideIndex = 0;
            BottleAmountMl = 30;
            Notes = "";

            await Microsoft.Maui.Controls.Shell.Current.DisplayAlert("✅ Registrado", "🍼 Mamada registrada com sucesso!", "OK");
        }
    }
}
