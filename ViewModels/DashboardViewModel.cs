using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BabyTrackerMobile.Data;
using BabyTrackerMobile.Models;
using BabyTrackerMobile.Services;

namespace BabyTrackerMobile.ViewModels
{
    /// <summary>Linha do checklist de vitaminas do dia no Painel.</summary>
    public partial class VitaminCheckItem : ObservableObject
    {
        private readonly int _vitaminId;
        private readonly DateTime _date;
        private bool _loading = true;

        public VitaminCheckItem(VitaminSchedule vitamin, DateTime date, bool taken)
        {
            _vitaminId = vitamin.Id;
            _date = date;
            Name = vitamin.Name;
            TimeDisplay = vitamin.TimeDisplay;
            isTaken = taken;
            _loading = false;
        }

        public string Name { get; }
        public string TimeDisplay { get; }

        [ObservableProperty]
        private bool isTaken;

        partial void OnIsTakenChanged(bool value)
        {
            if (_loading) return;
            _ = DatabaseService.SetVitaminTakenAsync(_vitaminId, _date, value);
        }
    }

    public partial class DashboardViewModel : ObservableObject
    {
        [ObservableProperty]
        private ObservableCollection<VitaminCheckItem> todayVitamins = new();

        [ObservableProperty]
        private bool hasVitamins;

        [ObservableProperty]
        private ObservableCollection<VitaminCheckItem> todayMotherVitamins = new();

        [ObservableProperty]
        private bool hasMotherVitamins;

        [ObservableProperty]
        private ObservableCollection<SleepRecord> todaySleeps = new();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SleepTotalText))]
        private int totalSleepMinutes;

        [ObservableProperty]
        private Baby? currentBaby;

        [ObservableProperty]
        private DailyAnalysis? todayAnalysis;

        [ObservableProperty]
        private ObservableCollection<FeedingRecord> todayFeedings = new();

        [ObservableProperty]
        private ObservableCollection<DiaperRecord> todayDiapers = new();

        [ObservableProperty]
        private ObservableCollection<MotherWaterRecord> todayMotherWaters = new();

        [ObservableProperty]
        private int totalMotherWaterMl;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(MilestonesText))]
        private int milestonesAchieved;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(MilestonesText))]
        private int milestonesTotal;

        [ObservableProperty]
        private bool isLoading;

        public string DayOfLifeText => CurrentBaby != null
            ? MedicalGuidelinesService.GetDayOfLifeDescription((DateTime.Today - CurrentBaby.BirthDate.Date).Days + 1)
            : "";

        public string BabyDisplayName => CurrentBaby?.Name ?? "Nenhum bebê cadastrado";

        public string MotherWaterText => $"{TotalMotherWaterMl} ml hoje";

        public string SleepTotalText => $"{TotalSleepMinutes / 60}h{TotalSleepMinutes % 60:00} hoje";

        public string MilestonesText => $"{MilestonesAchieved}/{MilestonesTotal} conquistadas";

        partial void OnTotalMotherWaterMlChanged(int value)
        {
            OnPropertyChanged(nameof(MotherWaterText));
        }

        // Dia exibido no Painel; usado para detectar a virada de dia.
        private DateTime _loadedDate = DateTime.Today;

        /// <summary>Recarrega o Painel se o dia mudou desde a última carga.</summary>
        public void CheckDayRollover()
        {
            if (!IsLoading && DateTime.Today != _loadedDate)
                LoadCommand.Execute(null);
        }

        [RelayCommand]
        public async Task LoadAsync()
        {
            IsLoading = true;
            try
            {
                CurrentBaby = await DatabaseService.GetOrCreateBabyAsync();
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
            _loadedDate = date;
            var feedings = await DatabaseService.GetFeedingsForDateAsync(CurrentBaby.Id, date);
            var diapers = await DatabaseService.GetDiapersForDateAsync(CurrentBaby.Id, date);
            var waters = await DatabaseService.GetMotherWatersForDateAsync(CurrentBaby.Id, date);
            var sleeps = await DatabaseService.GetSleepsForDateAsync(CurrentBaby.Id, date);
            TodayFeedings = new ObservableCollection<FeedingRecord>(feedings);
            TodayDiapers = new ObservableCollection<DiaperRecord>(diapers);
            TodayMotherWaters = new ObservableCollection<MotherWaterRecord>(waters);
            TodaySleeps = new ObservableCollection<SleepRecord>(sleeps);

            var total = 0;
            foreach (var w in waters) total += w.VolumeMl;
            TotalMotherWaterMl = total;

            var sleepMin = 0;
            foreach (var s in sleeps) sleepMin += (int)s.Duration.TotalMinutes;
            TotalSleepMinutes = sleepMin;

            var vitamins = await DatabaseService.GetVitaminsAsync(CurrentBaby.Id);
            var taken = await DatabaseService.GetTakenVitaminIdsAsync(date);
            var babyItems = new ObservableCollection<VitaminCheckItem>();
            var motherItems = new ObservableCollection<VitaminCheckItem>();
            foreach (var v in vitamins)
            {
                if (!v.IsActive) continue;
                var item = new VitaminCheckItem(v, date, taken.Contains(v.Id));
                if (v.IsMother) motherItems.Add(item); else babyItems.Add(item);
            }
            TodayVitamins = babyItems;
            HasVitamins = babyItems.Count > 0;
            TodayMotherVitamins = motherItems;
            HasMotherVitamins = motherItems.Count > 0;

            await DatabaseService.EnsureMilestonesSeededAsync(CurrentBaby.Id);
            MilestonesAchieved = await DatabaseService.CountAchievedMilestonesAsync(CurrentBaby.Id);
            MilestonesTotal = await DatabaseService.CountMilestonesAsync(CurrentBaby.Id);

            TodayAnalysis = MedicalGuidelinesService.AnalyzeDay(CurrentBaby, date, feedings, diapers);
            OnPropertyChanged(nameof(DayOfLifeText));
        }

        [RelayCommand]
        public async Task QuickFeedingAsync()
        {
            CurrentBaby ??= await DatabaseService.GetOrCreateBabyAsync();
            var record = new FeedingRecord { BabyId = CurrentBaby.Id, StartTime = DateTime.Now, DurationMinutes = 15, Side = BreastSide.Left };
            await DatabaseService.AddFeedingAsync(record);
            await RefreshAsync();
            await Microsoft.Maui.Controls.Shell.Current.DisplayAlert("✅ Registrado", "Mamada registrada!", "OK");
        }

        [RelayCommand]
        public async Task QuickWetDiaperAsync()
        {
            CurrentBaby ??= await DatabaseService.GetOrCreateBabyAsync();
            var record = new DiaperRecord { BabyId = CurrentBaby.Id, Time = DateTime.Now, IsWet = true };
            await DatabaseService.AddDiaperAsync(record);
            await RefreshAsync();
            await Microsoft.Maui.Controls.Shell.Current.DisplayAlert("✅ Registrado", "Fralda molhada registrada!", "OK");
        }

        [RelayCommand]
        public async Task QuickDirtyDiaperAsync()
        {
            CurrentBaby ??= await DatabaseService.GetOrCreateBabyAsync();
            var record = new DiaperRecord { BabyId = CurrentBaby.Id, Time = DateTime.Now, IsDirty = true, Color = StoolColor.Yellow, Consistency = StoolConsistency.Soft };
            await DatabaseService.AddDiaperAsync(record);
            await RefreshAsync();
            await Microsoft.Maui.Controls.Shell.Current.DisplayAlert("✅ Registrado", "Fralda suja registrada!", "OK");
        }

        [RelayCommand]
        public async Task QuickWater300Async() => await AddMotherWaterAsync(300);

        [RelayCommand]
        public async Task QuickWater500Async() => await AddMotherWaterAsync(500);

        private async Task AddMotherWaterAsync(int volumeMl)
        {
            CurrentBaby ??= await DatabaseService.GetOrCreateBabyAsync();
            var record = new MotherWaterRecord
            {
                BabyId = CurrentBaby.Id,
                Time = DateTime.Now,
                VolumeMl = volumeMl
            };
            await DatabaseService.AddMotherWaterAsync(record);
            await RefreshAsync();
        }

        [RelayCommand]
        public async Task GoToSleepAsync()
            => await Microsoft.Maui.Controls.Shell.Current.GoToAsync("//sleep");

        [RelayCommand]
        public async Task GoToMilestonesAsync()
            => await Microsoft.Maui.Controls.Shell.Current.GoToAsync("milestones");

        [RelayCommand]
        public async Task ExportDayAsync()
        {
            try
            {
                CurrentBaby ??= await DatabaseService.GetOrCreateBabyAsync();
                var today = DateTime.Today;
                var feedings = await DatabaseService.GetFeedingsForDateAsync(CurrentBaby.Id, today);
                var diapers = await DatabaseService.GetDiapersForDateAsync(CurrentBaby.Id, today);
                var waters = await DatabaseService.GetMotherWatersForDateAsync(CurrentBaby.Id, today);
                await ExportService.ShareDayAsync(CurrentBaby, today, feedings, diapers, waters);
            }
            catch (Exception ex)
            {
                await Microsoft.Maui.Controls.Shell.Current.DisplayAlert("Erro", $"Não foi possível exportar: {ex.Message}", "OK");
            }
        }

        private static T? Resolve<T>() where T : class
            => Microsoft.Maui.Controls.Application.Current?.Handler?.MauiContext?.Services.GetService<T>();

        [RelayCommand]
        public async Task EditFeedingAsync(FeedingRecord record)
        {
            var vm = Resolve<FeedingViewModel>();
            if (vm == null) return;
            vm.BeginEdit(record);
            await Microsoft.Maui.Controls.Shell.Current.GoToAsync("//feeding");
        }

        [RelayCommand]
        public async Task EditDiaperAsync(DiaperRecord record)
        {
            var vm = Resolve<DiaperViewModel>();
            if (vm == null) return;
            vm.BeginEdit(record);
            await Microsoft.Maui.Controls.Shell.Current.GoToAsync("//diaper");
        }

        [RelayCommand]
        public async Task EditSleepAsync(SleepRecord record)
        {
            var vm = Resolve<SleepViewModel>();
            if (vm == null) return;
            vm.BeginEdit(record);
            await Microsoft.Maui.Controls.Shell.Current.GoToAsync("//sleep");
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

        [RelayCommand]
        public async Task DeleteSleepAsync(SleepRecord record)
        {
            await DatabaseService.DeleteSleepAsync(record.Id);
            await RefreshAsync();
        }

        [RelayCommand]
        public async Task DeleteMotherWaterAsync(MotherWaterRecord record)
        {
            await DatabaseService.DeleteMotherWaterAsync(record.Id);
            await RefreshAsync();
        }
    }
}
