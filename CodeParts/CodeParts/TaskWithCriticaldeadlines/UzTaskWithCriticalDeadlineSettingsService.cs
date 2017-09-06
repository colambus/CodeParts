using Bars.B4.DataAccess;
using Bars.B4.Utils;
using Bars.BudgetPlaning.Dictionary.Enums;
using Bars.UZ.Utils.Entities.Administration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bars.UZ.Utils.Services.Administration
{
    [Display("Настройка контроля задач с критическими сроками")]
    [Description("Бизнес-логика контроля задач с критическими сроками")]
    public class UzTaskWithCriticalDeadlineSettingsService
    {
        public IDataStore DataStore { get; set; }

        [Display("Получить все настройки")]
        public IQueryable<UzTaskWithCriticaldeadlineSettingsListDTO> Query()
        {            
            var result = DataStore
                .GetAll<TaskWithCriticalDeadlineSettings>().Select(x=>
                    new UzTaskWithCriticaldeadlineSettingsListDTO
                    {
                        Id = x.Id,
                        ControlBeforeHours = x.ControlBefore,
                        IsActive = x.IsActive,
                        Name = x.Name,
                        TaskType = x.TaskType,
                        PlacingType = x.PlacingType,
                        DaysForImplementation = x.DaysForImplementation,
                        WorkingDays = x.WorkingDays,
                        Description = x.Description
                    });

            return result;
        }

        [Display("Сохранение/обновление настроек")]
        public void Update(
            [Display("Список DTO настроек задач с критическим сроком")] UzTaskWithCriticaldeadlineSettingsListDTO[] records)
        {
            if (records != null)
            {
                foreach (var el in records)
                {
                    var entity = DataStore.GetAll<TaskWithCriticalDeadlineSettings>().Where(x => x.Id == el.Id).FirstOrDefault();
                    entity.WorkingDays = el.WorkingDays;
                    entity.ControlBefore = el.ControlBeforeHours;
                    entity.DaysForImplementation = el.DaysForImplementation;
                    entity.IsActive = el.IsActive;
                    DataStore.Update(entity);
                }
                DataStore.Flush();
            }
        }
    }
}
