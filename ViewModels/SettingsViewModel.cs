using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BabyTrackerMobile.Data;
using BabyTrackerMobile.Models;
using BabyTrackerMobile.Services;

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

        // ---- Vitamina ----
        [ObservableProperty]
        private ObservableCollection<VitaminSchedule> vitamins = new();

        [ObservableProperty]
        private string newVitaminName = "Vitamina D";

        [ObservableProperty]
        private TimeSpan newVitaminTime = new(9, 0, 0);

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
                var baby = await DatabaseService.GetOrCreateBabyAsync();
                HasBaby = true;
                BabyId = baby.Id;
                BabyName = baby.Name;
                BirthDate = baby.BirthDate;
                BirthWeightGrams = baby.BirthWeightGrams;
                SelectedFeedingType = baby.FeedingType;
                SelectedFeedingTypeIndex = (int)baby.FeedingType;

                await LoadVitaminsAsync();
            }
            finally { IsLoading = false; }
        }

        public async Task LoadVitaminsAsync()
        {
            if (BabyId <= 0) return;
            var list = await DatabaseService.GetVitaminsAsync(BabyId);
            Vitamins = new ObservableCollection<VitaminSchedule>(list);
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
            await LoadAsync();
        }

        [RelayCommand]
        public async Task AddVitaminAsync()
        {
            if (string.IsNullOrWhiteSpace(NewVitaminName))
            {
                await Microsoft.Maui.Controls.Shell.Current.DisplayAlert("Aviso", "Informe o nome da vitamina.", "OK");
                return;
            }
            if (BabyId <= 0)
            {
                var b = await DatabaseService.GetOrCreateBabyAsync();
                BabyId = b.Id;
            }

            var vitamin = new VitaminSchedule
            {
                BabyId = BabyId,
                Name = NewVitaminName.Trim(),
                TimeOfDay = NewVitaminTime,
                IsActive = true
            };

            await DatabaseService.SaveVitaminAsync(vitamin);
            await NotificationService.ScheduleAsync(vitamin);
            await LoadVitaminsAsync();

            NewVitaminName = "Vitamina D";
            NewVitaminTime = new TimeSpan(9, 0, 0);

            await Microsoft.Maui.Controls.Shell.Current.DisplayAlert(
                "⏰ Lembrete criado",
                $"Você receberá uma notificação todos os dias às {vitamin.TimeDisplay}.",
                "OK");
        }

        [RelayCommand]
        public async Task DeleteVitaminAsync(VitaminSchedule vitamin)
        {
            NotificationService.Cancel(vitamin);
            await DatabaseService.DeleteVitaminAsync(vitamin.Id);
            await LoadVitaminsAsync();
        }

        [RelayCommand]
        public async Task ToggleVitaminAsync(VitaminSchedule vitamin)
        {
            vitamin.IsActive = !vitamin.IsActive;
            await DatabaseService.SaveVitaminAsync(vitamin);
            if (vitamin.IsActive)
                await NotificationService.ScheduleAsync(vitamin);
            else
                NotificationService.Cancel(vitamin);
            await LoadVitaminsAsync();
        }

        [RelayCommand]
        public async Task ExportDayAsync()
        {
            try
            {
                var baby = await DatabaseService.GetOrCreateBabyAsync();
                var today = DateTime.Today;
                var feedings = await DatabaseService.GetFeedingsForDateAsync(baby.Id, today);
                var diapers = await DatabaseService.GetDiapersForDateAsync(baby.Id, today);
                var waters = await DatabaseService.GetMotherWatersForDateAsync(baby.Id, today);
                await ExportService.ShareDayAsync(baby, today, feedings, diapers, waters);
            }
            catch (Exception ex)
            {
                await Microsoft.Maui.Controls.Shell.Current.DisplayAlert("Erro", $"Não foi possível exportar: {ex.Message}", "OK");
            }
        }
    }
}
