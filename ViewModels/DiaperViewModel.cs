using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BabyTrackerMobile.Data;
using BabyTrackerMobile.Models;

namespace BabyTrackerMobile.ViewModels
{
    public partial class DiaperViewModel : ObservableObject
    {
        [ObservableProperty]
        private Baby? currentBaby;

        [ObservableProperty]
        private DateTime diaperTime = DateTime.Now;

        [ObservableProperty]
        private bool isWet = true;

        [ObservableProperty]
        private bool isDirty = false;

        [ObservableProperty]
        private StoolColor selectedColor = StoolColor.Yellow;

        [ObservableProperty]
        private StoolConsistency selectedConsistency = StoolConsistency.Soft;

        [ObservableProperty]
        private string notes = "";

        [ObservableProperty]
        private bool isLoading;

        public List<string> ColorOptions { get; } = new List<string>
        {
            "Mecônio (escuro)", "Verde escuro", "Amarelo", "Marrom", "Branco ⚠️", "Vermelho ⚠️"
        };

        public List<string> ConsistencyOptions { get; } = new List<string>
        {
            "Líquido", "Grumoso", "Pastoso", "Duro"
        };

        private int selectedColorIndex = 2; // Yellow
        public int SelectedColorIndex
        {
            get => selectedColorIndex;
            set
            {
                if (SetProperty(ref selectedColorIndex, value))
                {
                    SelectedColor = (StoolColor)value;
                }
            }
        }

        private int selectedConsistencyIndex = 2; // Soft
        public int SelectedConsistencyIndex
        {
            get => selectedConsistencyIndex;
            set
            {
                if (SetProperty(ref selectedConsistencyIndex, value))
                {
                    SelectedConsistency = (StoolConsistency)value;
                }
            }
        }

        [RelayCommand]
        public async Task LoadAsync()
        {
            IsLoading = true;
            try
            {
                CurrentBaby = await DatabaseService.GetOrCreateBabyAsync();
            }
            finally { IsLoading = false; }
        }

        [RelayCommand]
        public async Task RegisterDiaperAsync()
        {
            try
            {
                CurrentBaby ??= await DatabaseService.GetOrCreateBabyAsync();

                if (!IsWet && !IsDirty)
                {
                    await Microsoft.Maui.Controls.Shell.Current.DisplayAlert("Aviso", "A fralda deve estar molhada, suja ou ambos.", "OK");
                    return;
                }

                var record = new DiaperRecord
                {
                    BabyId = CurrentBaby.Id,
                    Time = DiaperTime,
                    IsWet = IsWet,
                    IsDirty = IsDirty,
                    Color = IsDirty ? SelectedColor : null,
                    Consistency = IsDirty ? SelectedConsistency : null,
                    Notes = Notes
                };

                await DatabaseService.AddDiaperAsync(record);

                // Reset form
                DiaperTime = DateTime.Now;
                IsWet = true;
                IsDirty = false;
                SelectedColorIndex = 2;
                SelectedConsistencyIndex = 2;
                Notes = "";

                await Microsoft.Maui.Controls.Shell.Current.DisplayAlert("✅ Registrado", "🧷 Fralda registrada com sucesso!", "OK");
            }
            catch (Exception ex)
            {
                await Microsoft.Maui.Controls.Shell.Current.DisplayAlert("Erro", $"Não foi possível registrar: {ex.Message}", "OK");
            }
        }
    }
}
