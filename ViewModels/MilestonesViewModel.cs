using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BabyTrackerMobile.Data;
using BabyTrackerMobile.Models;

namespace BabyTrackerMobile.ViewModels
{
    /// <summary>Linha de conquista: marcar/desmarcar e ajustar a data salvam na hora.</summary>
    public partial class MilestoneItem : ObservableObject
    {
        private bool _loading = true;
        public Milestone Model { get; }

        public MilestoneItem(Milestone model)
        {
            Model = model;
            achieved = model.AchievedDate.HasValue;
            date = model.AchievedDate ?? DateTime.Today;
            _loading = false;
        }

        public string Icon => Model.Icon;
        public string Title => Model.Title;
        public bool IsCustom => Model.IsCustom;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(StatusText))]
        private bool achieved;

        [ObservableProperty]
        private DateTime date;

        public string StatusText => Achieved ? "Conquistado!" : "Ainda não";

        partial void OnAchievedChanged(bool value)
        {
            if (_loading) return;
            Model.AchievedDate = value ? Date.Date : (DateTime?)null;
            _ = DatabaseService.SaveMilestoneAsync(Model);
        }

        partial void OnDateChanged(DateTime value)
        {
            if (_loading) return;
            if (Achieved)
            {
                Model.AchievedDate = value.Date;
                _ = DatabaseService.SaveMilestoneAsync(Model);
            }
        }
    }

    public partial class MilestonesViewModel : ObservableObject
    {
        [ObservableProperty]
        private Baby? currentBaby;

        [ObservableProperty]
        private ObservableCollection<MilestoneItem> milestones = new();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ProgressText))]
        private int achievedCount;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ProgressText))]
        private int totalCount;

        [ObservableProperty]
        private bool isLoading;

        public string ProgressText => $"🏆 {AchievedCount} de {TotalCount} conquistas";

        [RelayCommand]
        public async Task LoadAsync()
        {
            IsLoading = true;
            try
            {
                CurrentBaby = await DatabaseService.GetOrCreateBabyAsync();
                await DatabaseService.EnsureMilestonesSeededAsync(CurrentBaby.Id);
                var list = await DatabaseService.GetMilestonesAsync(CurrentBaby.Id);
                var items = new ObservableCollection<MilestoneItem>();
                var achieved = 0;
                foreach (var m in list)
                {
                    items.Add(new MilestoneItem(m));
                    if (m.AchievedDate.HasValue) achieved++;
                }
                Milestones = items;
                TotalCount = list.Count;
                AchievedCount = achieved;
            }
            finally { IsLoading = false; }
        }

        [RelayCommand]
        public async Task AddCustomAsync()
        {
            var name = await Microsoft.Maui.Controls.Shell.Current.DisplayPromptAsync(
                "Nova conquista", "Qual foi a conquista?", "Adicionar", "Cancelar", "Ex: Primeiro banho de piscina");
            if (string.IsNullOrWhiteSpace(name)) return;

            CurrentBaby ??= await DatabaseService.GetOrCreateBabyAsync();
            await DatabaseService.SaveMilestoneAsync(new Milestone
            {
                BabyId = CurrentBaby.Id,
                Icon = "⭐",
                Title = name.Trim(),
                SortOrder = 1000 + TotalCount,
                IsCustom = true
            });
            await LoadAsync();
        }

        [RelayCommand]
        public async Task DeleteAsync(MilestoneItem item)
        {
            await DatabaseService.DeleteMilestoneAsync(item.Model.Id);
            await LoadAsync();
        }
    }
}
