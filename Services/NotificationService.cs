using System;
using System.Threading.Tasks;
using Plugin.LocalNotification;
using BabyTrackerMobile.Models;

namespace BabyTrackerMobile.Services
{
    /// <summary>
    /// Agenda notificações locais diárias para lembretes de vitaminas.
    /// </summary>
    public static class NotificationService
    {
        private const int BaseNotificationId = 1000;

        /// <summary>Pede permissão de notificação ao usuário (iOS 10+).</summary>
        public static async Task<bool> RequestPermissionAsync()
        {
            if (await LocalNotificationCenter.Current.AreNotificationsEnabled())
                return true;
            return await LocalNotificationCenter.Current.RequestNotificationPermission();
        }

        /// <summary>Agenda a notificação diária para a vitamina.</summary>
        public static async Task ScheduleAsync(VitaminSchedule vitamin)
        {
            if (!vitamin.IsActive) return;

            await RequestPermissionAsync();

            // Cancela se já estava agendada
            LocalNotificationCenter.Current.Cancel(BaseNotificationId + vitamin.Id);

            // Monta o próximo horário de disparo (hoje se ainda não passou; senão, amanhã)
            var now = DateTime.Now;
            var next = DateTime.Today.Add(vitamin.TimeOfDay);
            if (next <= now) next = next.AddDays(1);

            var request = new NotificationRequest
            {
                NotificationId = BaseNotificationId + vitamin.Id,
                Title = "💊 Hora da vitamina",
                Subtitle = vitamin.Name,
                Description = $"Está na hora de dar {vitamin.Name} para o bebê.",
                BadgeNumber = 1,
                Schedule = new NotificationRequestSchedule
                {
                    NotifyTime = next,
                    NotifyRepeatInterval = TimeSpan.FromDays(1)
                }
            };

            await LocalNotificationCenter.Current.Show(request);
        }

        public static void Cancel(VitaminSchedule vitamin)
        {
            LocalNotificationCenter.Current.Cancel(BaseNotificationId + vitamin.Id);
        }
    }
}
