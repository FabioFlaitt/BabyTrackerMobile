using System;
using System.Threading.Tasks;
using Plugin.LocalNotification;
using BabyTrackerMobile.Models;

namespace BabyTrackerMobile.Services
{
    /// <summary>
    /// Notificações locais: lembretes de vitamina e avisos de mensagem do chat.
    /// </summary>
    public static class NotificationService
    {
        private const int BaseNotificationId = 1000;

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
            LocalNotificationCenter.Current.Cancel(BaseNotificationId + vitamin.Id);

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

        /// <summary>Mostra na hora uma notificação (usada pelo chat). badge = número no ícone.</summary>
        public static async Task ShowMessageAsync(int id, string title, string description, int badge = 1)
        {
            await RequestPermissionAsync();
            var request = new NotificationRequest
            {
                NotificationId = id,
                Title = title,
                Description = description,
                BadgeNumber = badge
            };
            await LocalNotificationCenter.Current.Show(request);
        }

        /// <summary>Zera o badge do ícone e limpa notificações entregues (ao abrir o app).</summary>
        public static void ClearBadge()
        {
            try { LocalNotificationCenter.Current.ClearAll(); } catch { }
#if IOS
            try
            {
                UIKit.UIApplication.SharedApplication.InvokeOnMainThread(() =>
                {
                    UIKit.UIApplication.SharedApplication.ApplicationIconBadgeNumber = 0;
                });
            }
            catch { }
#endif
        }
    }
}
