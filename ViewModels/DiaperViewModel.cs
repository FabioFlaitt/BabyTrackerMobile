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
        private DiaperRecord? _editing;
        private DateTime _date = DateTime.Today;

        [ObservableProperty]
        private Baby? currentBaby;

        // TimePicker.Time é TimeSpan (antes era DateTime e o horário informado era ignorado).
        [ObservableProperty]
        private TimeSpan diaperTime = DateTime.Now.TimeOfDay;

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

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ActionButtonText))]
        [NotifyPropertyChangedFor(nameof(FormTitle))]
        private bool isEditing;

        public string ActionButtonText => IsEditing ? "Salvar alteração 💾" : "Registrar Fralda 🧷";
        public string FormTitle => IsEditing ? "✏️ Editar Fralda" : "🧷 Registrar Fralda";

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
                if (!IsEditing) DiaperTime = DateTime.Now.TimeOfDay;
            }
            finally { IsLoading = false; }
        }

        /// <summary>Preenche o formulário com um registro existente (chamado pelo Painel).</summary>
        public void BeginEdit(DiaperRecord record)
        {
            _editing = record;
            _date = record.Time.Date;
            DiaperTime = record.Time.TimeOfDay;
            IsWet = record.IsWet;
            IsDirty = record.IsDirty;
            SelectedColorIndex = (int)(record.Color ?? StoolColor.Yellow);
            SelectedConsistencyIndex = (int)(record.Consistency ?? StoolConsistency.Soft);
            Notes = record.Notes ?? "";
            IsEditing = true;
        }

        private void ResetForm()
        {
            _editing = null;
            _date = DateTime.Today;
            IsEditing = false;
            DiaperTime = DateTime.Now.TimeOfDay;
            IsWet = true;
            IsDirty = false;
            SelectedColorIndex = 2;
            SelectedConsistencyIndex = 2;
            Notes = "";
        }

        [RelayCommand]
        public async Task CancelEditAsync()
        {
            ResetForm();
            await Microsoft.Maui.Controls.Shell.Current.GoToAsync("//dashboard");
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

                var when = _date.Add(new TimeSpan(DiaperTime.Hours, DiaperTime.Minutes, 0));

                if (_editing != null)
                {
                    _editing.Time = when;
                    _editing.IsWet = IsWet;
                    _editing.IsDirty = IsDirty;
                    _editing.Color = IsDirty ? SelectedColor : null;
                    _editing.Consistency = IsDirty ? SelectedConsistency : null;
                    _editing.Notes = Notes;
                    await DatabaseService.UpdateDiaperAsync(_editing);

                    ResetForm();
                    await Microsoft.Maui.Controls.Shell.Current.DisplayAlert("✅ Alterado", "🧷 Fralda atualizada!", "OK");
                    await Microsoft.Maui.Controls.Shell.Current.GoToAsync("//dashboard");
                    return;
                }

                var record = new DiaperRecord
                {
                    BabyId = CurrentBaby.Id,
                    Time = when,
                    IsWet = IsWet,
                    IsDirty = IsDirty,
                    Color = IsDirty ? SelectedColor : null,
                    Consistency = IsDirty ? SelectedConsistency : null,
                    Notes = Notes
                };

                await DatabaseService.AddDiaperAsync(record);
                ResetForm();

                await Microsoft.Maui.Controls.Shell.Current.DisplayAlert("✅ Registrado", "🧷 Fralda registrada com sucesso!", "OK");
            }
            catch (Exception ex)
            {
                await Microsoft.Maui.Controls.Shell.Current.DisplayAlert("Erro", $"Não foi possível registrar: {ex.Message}", "OK");
            }
        }
    }
}
