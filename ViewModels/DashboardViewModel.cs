using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BabyTrackerMobile.Data;
using BabyTrackerMobile.Models;
using BabyTrackerMobile.Services;

namespace BabyTrackerMobile.ViewModels
{
    public partial class DashboardViewModel : ObservableObject
    {
        [ObservableProperty]
        private Baby? currentBaby;

        [ObservableProperty]
        private DailyAnalysis? todayAnalysis;

        [ObservableProperty]
        private ObservableCollection<FeedingRecord> todayFeedings = new();

        [ObservableProperty]
        private ObservableCollection<DiaperRecord> todayDiapers = new();

        [ObservableProperty]
        private bool isLoading;

        public string DayOfLifeText => CurrentBaby != null
            ? MedicalGuidelinesService.GetDayOfLifeDescription((DateTime.Today - CurrentBaby.BirthDate.Date).Days + 1)
            : "";

        public string BabyDisplayName => CurrentBaby?.Name ?? "Nenhum bebê cadastrado";

        [RelayCommand]
        public async Task LoadAsync()
        {
            IsLoading = true;
            try
            {
                CurrentBaby = await DatabaseService.GetFirstBabyAsync();
                OnPropertyChanged(nameof(DayOfLifeText));
                OnPropertyChanged(nameof(BabyDisplayName));
                await RefreshAsync();
            }
            finally { IsLoading = false; }
        }

        [RelayCommand]
        public async Task RefreshAsync()
        {
            if (CurrentBaby == null) return;
            var date = DateTime.Today;
            var feedings = await DatabaseService.GetFeedingsForDateAsync(CurrentBaby.Id, date);
            var diapers = await DatabaseService.GetDiapersForDateAsync(CurrentBaby.Id, date);
            TodayFeedings = new ObservableCollection<FeedingRecord>(feedings);
            TodayDiapers = new ObservableCollection<DiaperRecord>(diapers);
            TodayAnalysis = MedicalGuidelinesService.AnalyzeDay(CurrentBaby, date, feedings, diapers);
            OnPropertyChanged(nameof(DayOfLifeText));
        }

        [RelayCommand]
        public async Task QuickFeedingAsync()
        {
            if (CurrentBaby == null) return;
            var record = new FeedingRecord { BabyId = CurrentBaby.Id, StartTime = DateTime.Now, DurationMinutes = 15, Side = BreastSide.Left };
            await DatabaseService.AddFeedingAsync(record);
            await RefreshAsync();
            await Microsoft.Maui.Controls.Shell.Current.DisplayAlert("✅ Registrado", "Mamada registrada!", "OK");
        }

        [RelayCommand]
        public async Task QuickWetDiaperAsync()
        {
            if (CurrentBaby == null) return;
            var record = new DiaperRecord { BabyId = CurrentBaby.Id, Time = DateTime.Now, IsWet = true };
            await DatabaseService.AddDiaperAsync(record);
            await RefreshAsync();
            await Microsoft.Maui.Controls.Shell.Current.DisplayAlert("✅ Registrado", "Fralda molhada registrada!", "OK");
        }

        [RelayCommand]
        public async Task QuickDirtyDiaperAsync()
        {
            if (CurrentBaby == null) return;
            var record = new DiaperRecord { BabyId = CurrentBaby.Id, Time = DateTime.Now, IsDirty = true, Color = StoolColor.Yellow, Consistency = StoolConsistency.Soft };
            await DatabaseService.AddDiaperAsync(record);
            await RefreshAsync();
            await Microsoft.Maui.Controls.Shell.Current.DisplayAlert("✅ Registrado", "Fralda suja registrada!", "OK");
        }

        [RelayCommand]
        public async Task DeleteFeedingAsync(FeedingRecord record)
        {
            await DatabaseService.DeleteFeedingAsync(record.Id);
            await RefreshAsync();
        }

        [RelayCommand]
        public async Task DeleteDiaperAsync(DiaperRecord record)
        {
            await DatabaseService.DeleteDiaperAsync(record.Id);
            await RefreshAsync();
        }
    }
}
