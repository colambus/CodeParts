namespace Bars.UZ.Utils.Services.Administration
{
    using Bars.B4.DataModels;
    using Bars.B4.Services;
    using Bars.B4.Utils;
    using Bars.UZ.Entities.Administration;
    using Bars.UZ.Utils.DTO.Administration;
    using System;

    [Display("Настройки задач с критическим сроком")]
    [Description("Бизнес-логика работы с настройками задач с критическим сроком")]
    public class UzTaskWithCriticalDeadlineSettingsServiceManifest : IServiceManifest
    {
        public void Register(IServiceRegistrationContainer registrationContainer)
        {
            registrationContainer.Service<UzTaskWithCriticalDeadlineSettingsService>(d =>
            {
                d.ListOperation(s => s.Query())
                    .AltExtJSCompatibleController()
                    .WithRequiredPermission("Administration.Notification.TaskWithCriticalDeadline.View");

                d.Operation(s => s.Update(default(UzTaskWithCriticaldeadlineSettingsListDTO[])))
                    .WithRequiredPermission("Administration.Notification.TaskWithCriticalDeadline.Edit");
            });
        }
    }
}
