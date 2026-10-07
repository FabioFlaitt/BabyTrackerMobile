using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BabyTrackerMobile.Data;
using BabyTrackerMobile.Models;

namespace BabyTrackerMobile.ViewModels
{
    public partial class SleepViewModel : ObservableObject
    {
        private SleepRecord? _editing;
        private DateTime _date = DateTime.Today;

        [ObservableProperty]
        private Baby? currentBaby;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DurationText))]
        private TimeSpan startTime = DateTime.Now.AddHours(-1).TimeOfDay;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DurationText))]
        private TimeSpan endTime = DateTime.Now.TimeOfDay;

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ActionButtonText))]
        [NotifyPropertyChangedFor(nameof(FormTitle))]
        private bool isEditing;

        public string ActionButtonText => IsEditing ? "Salvar alteração 💾" : "Registrar Sono 😴";
        public string FormTitle => IsEditing ? "✏️ Editar Sono" : "😴 Registrar Sono";

        // Duração mostrada ao vivo (fim que cruza a meia-noite conta no dia seguinte).
        public string DurationText
        {
            get
            {
                var dur = EndTime - StartTime;
                if (dur <= TimeSpan.Zero) dur += TimeSpan.FromDays(1);
                return $"Duração: {(int)dur.TotalHours}h{dur.Minutes:00}";
            }
        }

        [RelayCommand]
        public async Task LoadAsync()
        {
            IsLoading = true;
            try
            {
                CurrentBaby = await DatabaseService.GetOrCreateBabyAsync();
                if (!IsEditing)
                {
                    StartTime = DateTime.Now.AddHours(-1).TimeOfDay;
                    EndTime = DateTime.Now.TimeOfDay;
                }
            }
            finally { IsLoading = false; }
        }

        public void BeginEdit(SleepRecord record)
        {
            _editing = record;
            _date = record.Start.Date;
            StartTime = record.Start.TimeOfDay;
            EndTime = record.End.TimeOfDay;
            IsEditing = true;
        }

        private void ResetForm()
        {
            _editing = null;
            _date = DateTime.Today;
            IsEditing = false;
            StartTime = DateTime.Now.AddHours(-1).TimeOfDay;
            EndTime = DateTime.Now.TimeOfDay;
        }

        [RelayCommand]
        public async Task CancelEditAsync()
        {
            ResetForm();
            await Microsoft.Maui.Controls.Shell.Current.GoToAsync("//dashboard");
        }

        [RelayCommand]
        public async Task RegisterSleepAsync()
        {
            try
            {
                CurrentBaby ??= await DatabaseService.GetOrCreateBabyAsync();

                var start = _date.Add(new TimeSpan(StartTime.Hours, StartTime.Minutes, 0));
                var end = _date.Add(new TimeSpan(EndTime.Hours, EndTime.Minutes, 0));
                if (end <= start) end = end.AddDays(1); // cruzou a meia-noite

                if (_editing != null)
                {
                    _editing.Start = start;
                    _editing.End = end;
                    await DatabaseService.UpdateSleepAsync(_editing);
                    ResetForm();
                    await Microsoft.Maui.Controls.Shell.Current.DisplayAlert("✅ Alterado", "😴 Sono atualizado!", "OK");
                    await Microsoft.Maui.Controls.Shell.Current.GoToAsync("//dashboard");
                    return;
                }

                var record = new SleepRecord
                {
                    BabyId = CurrentBaby.Id,
                    Start = start,
                    End = end
                };
                await DatabaseService.AddSleepAsync(record);
                ResetForm();
                await Microsoft.Maui.Controls.Shell.Current.DisplayAlert("✅ Registrado", "😴 Sono registrado com sucesso!", "OK");
            }
            catch (Exception ex)
            {
                await Microsoft.Maui.Controls.Shell.Current.DisplayAlert("Erro", $"Não foi possível registrar: {ex.Message}", "OK");
            }
        }
    }
}
