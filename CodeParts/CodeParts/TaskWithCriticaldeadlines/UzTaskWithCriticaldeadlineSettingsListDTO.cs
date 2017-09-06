using Bars.B4.DataModels;
using Bars.B4.Utils;
using Bars.BudgetPlaning.Dictionary.Enums;
using Bars.UZ.Enums;
using Bars.UZ.Utils.Entities.Administration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bars.UZ.Utils.Services.Administration
{
    [Display("Данные метода настроек задач с критическими сроками")]
    public class UzTaskWithCriticaldeadlineSettingsListDTO : IHaveId
    {
        [Display("Идентификатор")]
        public long Id { get; set; }

        [Display("Активна")]
        public bool IsActive { get; set; }

        [Display("Тип задачи")]
        public TaskType TaskType { get; set; }

        [Display("Наименование задачи")]
        public string Name { get; set; }

        [Display("Способ определения поставщика")]
        public TenderPlanPlacingType? PlacingType { get; set; }

        [Display("Количество дней на выполнение задачи")]
        public int DaysForImplementation { get; set; }

        [Display("Рабочие дни")]
        public bool WorkingDays { get; set; }

        [Display("Контролировать за (часов)")]
        public int ControlBeforeHours { get; set; }

        [Display("Описание")]
        public string Description { get; set; }
    }
}
