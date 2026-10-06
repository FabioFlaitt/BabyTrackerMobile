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
        private FeedingRecord? _editing;
        private DateTime _date = DateTime.Today;

        [ObservableProperty]
        private Baby? currentBaby;

        // TimePicker.Time é TimeSpan. Antes isto era DateTime e o binding falhava
        // em silêncio, então o horário escolhido nunca chegava ao registro.
        [ObservableProperty]
        private TimeSpan feedingTime = DateTime.Now.TimeOfDay;

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

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ActionButtonText))]
        [NotifyPropertyChangedFor(nameof(FormTitle))]
        private bool isEditing;

        public string ActionButtonText => IsEditing ? "Salvar alteração 💾" : "Registrar Mamada 🍼";
        public string FormTitle => IsEditing ? "✏️ Editar Mamada" : "🍼 Registrar Mamada";

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
                CurrentBaby = await DatabaseService.GetOrCreateBabyAsync();
                // Formulário novo começa com a hora atual; edição mantém a hora do registro.
                if (!IsEditing) FeedingTime = DateTime.Now.TimeOfDay;
            }
            finally { IsLoading = false; }
        }

        /// <summary>Preenche o formulário com um registro existente (chamado pelo Painel).</summary>
        public void BeginEdit(FeedingRecord record)
        {
            _editing = record;
            _date = record.StartTime.Date;
            FeedingTime = record.StartTime.TimeOfDay;
            DurationMinutes = record.DurationMinutes;
            SelectedSideIndex = (int)record.Side;
            BottleAmountMl = record.BottleAmountMl ?? 30;
            Notes = record.Notes ?? "";
            IsEditing = true;
        }

        private void ResetForm()
        {
            _editing = null;
            _date = DateTime.Today;
            IsEditing = false;
            FeedingTime = DateTime.Now.TimeOfDay;
            DurationMinutes = 15;
            SelectedSideIndex = 0;
            BottleAmountMl = 30;
            Notes = "";
        }

        [RelayCommand]
        public async Task CancelEditAsync()
        {
            ResetForm();
            await Microsoft.Maui.Controls.Shell.Current.GoToAsync("//dashboard");
        }

        [RelayCommand]
        public async Task RegisterFeedingAsync()
        {
            try
            {
                CurrentBaby ??= await DatabaseService.GetOrCreateBabyAsync();

                var when = _date.Add(new TimeSpan(FeedingTime.Hours, FeedingTime.Minutes, 0));

                if (_editing != null)
                {
                    _editing.StartTime = when;
                    _editing.DurationMinutes = DurationMinutes;
                    _editing.Side = SelectedSide;
                    _editing.BottleAmountMl = IsBottleFeeding ? BottleAmountMl : null;
                    _editing.Notes = Notes;
                    await DatabaseService.UpdateFeedingAsync(_editing);

                    ResetForm();
                    await Microsoft.Maui.Controls.Shell.Current.DisplayAlert("✅ Alterado", "🍼 Mamada atualizada!", "OK");
                    await Microsoft.Maui.Controls.Shell.Current.GoToAsync("//dashboard");
                    return;
                }

                var record = new FeedingRecord
                {
                    BabyId = CurrentBaby.Id,
                    StartTime = when,
                    DurationMinutes = DurationMinutes,
                    Side = SelectedSide,
                    BottleAmountMl = IsBottleFeeding ? BottleAmountMl : null,
                    Notes = Notes
                };

                await DatabaseService.AddFeedingAsync(record);
                ResetForm();

                await Microsoft.Maui.Controls.Shell.Current.DisplayAlert("✅ Registrado", "🍼 Mamada registrada com sucesso!", "OK");
            }
            catch (Exception ex)
            {
                await Microsoft.Maui.Controls.Shell.Current.DisplayAlert("Erro", $"Não foi possível registrar: {ex.Message}", "OK");
            }
        }
    }
}
