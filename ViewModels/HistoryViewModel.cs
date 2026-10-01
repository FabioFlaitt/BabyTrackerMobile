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
    public partial class HistoryViewModel : ObservableObject
    {
        [ObservableProperty]
        private Baby? currentBaby;

        [ObservableProperty]
        private DateTime selectedDate = DateTime.Today;

        [ObservableProperty]
        private DailyAnalysis? historyAnalysis;

        [ObservableProperty]
        private ObservableCollection<FeedingRecord> feedings = new();

        [ObservableProperty]
        private ObservableCollection<DiaperRecord> diapers = new();

        [ObservableProperty]
        private bool isLoading;

        public string SelectedDateText => SelectedDate.ToString("dd/MM/yyyy");

        [RelayCommand]
        public async Task LoadAsync()
        {
            IsLoading = true;
            try
            {
                CurrentBaby = await DatabaseService.GetFirstBabyAsync();
                await LoadHistoryAsync();
            }
            finally { IsLoading = false; }
        }

        [RelayCommand]
        public async Task LoadHistoryAsync()
        {
            if (CurrentBaby == null) return;

            var f = await DatabaseService.GetFeedingsForDateAsync(CurrentBaby.Id, SelectedDate);
            var d = await DatabaseService.GetDiapersForDateAsync(CurrentBaby.Id, SelectedDate);
            
            Feedings = new ObservableCollection<FeedingRecord>(f);
            Diapers = new ObservableCollection<DiaperRecord>(d);
            HistoryAnalysis = MedicalGuidelinesService.AnalyzeDay(CurrentBaby, SelectedDate, f, d);
            
            OnPropertyChanged(nameof(SelectedDateText));
        }

        [RelayCommand]
        public async Task PreviousDayAsync()
        {
            SelectedDate = SelectedDate.AddDays(-1);
            await LoadHistoryAsync();
        }

        [RelayCommand]
        public async Task NextDayAsync()
        {
            if (SelectedDate < DateTime.Today)
            {
                SelectedDate = SelectedDate.AddDays(1);
                await LoadHistoryAsync();
            }
        }

        [RelayCommand]
        public async Task DeleteFeedingAsync(FeedingRecord record)
        {
            await DatabaseService.DeleteFeedingAsync(record.Id);
            await LoadHistoryAsync();
        }

        [RelayCommand]
        public async Task DeleteDiaperAsync(DiaperRecord record)
        {
            await DatabaseService.DeleteDiaperAsync(record.Id);
            await LoadHistoryAsync();
        }
    }
}
