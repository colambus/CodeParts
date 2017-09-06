using Bars.B4.Application;
using Bars.B4.DataAccess;
using Bars.B4.DataAccess.ByCode;
using Bars.B4.Utils;
using Bars.BudgetPlaning.Dictionary.Entities.Simple;
using Bars.BudgetPlaning.Dictionary.Entities.UZ.Enums;
using Bars.BudgetPlaning.Dictionary.Enums;
using Bars.Minfin.Modules.Calendar.Entities;
using Bars.UZ.Contracts.Entities;
using Bars.UZ.Enums;
using Bars.UZ.Mapping;
using Bars.UZ.Utils.Entities.Administration;
using Castle.Windsor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bars.UZ.Utils.Services.Administration
{
    /// <summary>
    /// Класс для передачи результата действия метода    
    /// </summary>
    public class TaskWithCriticalDeadlineResult
    {
        public long Id { get; set; }

        public DateTime CriticalDate { get; set; }
    }

    /// <summary>
    /// Методы для задач с критическим сроком
    /// </summary>
    public static class TaskWithCriticalDeadlineMethods
    {
        public static IWindsorContainer container;
        public static IDataStore datastore;

        /// <summary>
        /// Выбор метода для задачи с критическим сроком
        /// </summary>
        public static List<TaskWithCriticalDeadlineResult> MethodChoise(TaskWithCriticalDeadlineSettings setting)
        {
            IWindsorContainer container = ApplicationContext.Current.Container;
            IRepository<Notice> repo = container.Resolve<IRepository<Notice>>();

            List<TaskWithCriticalDeadlineResult> result = new List<TaskWithCriticalDeadlineResult>();
            switch (setting.TaskType)
            {
                case TaskType.BlockAbility: result = MethodForBlockAbility(setting, repo); break;
                case TaskType.RequiredAction: result = MethodForRequiredAction(setting, repo); break;
            }
            return result;
        }

        /// <summary>
        /// Метод "Необходимое действие" 
        /// </summary>
        public static List<TaskWithCriticalDeadlineResult> MethodForRequiredAction(TaskWithCriticalDeadlineSettings setting, IRepository<Notice> repo)
        {
            var result = new List<TaskWithCriticalDeadlineResult>();
            var notices = repo.GetAll().Where(x=> !x.Deleted && x.VersionIsCurrent && 
                x.StatusInRelationToOos != StatusInRelationToOos.IsPublished && x.StatusInRelationToOos != StatusInRelationToOos.Editing && x.PlacementPlanDate!=null);

            foreach(var notice in notices)
            {
                DateTime time = (DateTime)notice.PlacementPlanDate;
                time = time.AddDays(-setting.DaysForImplementation);
                result.Add(new TaskWithCriticalDeadlineResult { Id = notice.Id, CriticalDate = time });
            }
            return result;
        }

        /// <summary>
        /// Метод "Заблокировать возможность" 
        /// </summary>
        public static List<TaskWithCriticalDeadlineResult> MethodForBlockAbility(TaskWithCriticalDeadlineSettings setting, IRepository<Notice> repo)
        {
            var result = new List<TaskWithCriticalDeadlineResult>();
            var notices = repo.GetAll().Where(x => !x.Deleted && x.VersionIsCurrent &&
                (x.StatusInRelationToOos == StatusInRelationToOos.IsPublished || x.StatusInRelationToOos == StatusInRelationToOos.Editing) 
                && x.PlacingWay.PlacingType == setting.PlacingType);

            foreach(var notice in notices)
            {
                DateTime time = (DateTime)notice.PlacementPlanDate;
                if (!setting.WorkingDays)
                {
                    time = time.AddDays(-setting.DaysForImplementation);
                    result.Add(new TaskWithCriticalDeadlineResult { Id = notice.Id, CriticalDate = time });
                }
                else
                {
                    time = time.AddDays(-CountDaysWithoutWeekendsToDeadline(time, setting.DaysForImplementation));
                    result.Add(new TaskWithCriticalDeadlineResult { Id = notice.Id, CriticalDate = time });
                }
            }
            return result;
        }

        /// <summary>
        /// Метод для определения количества дней с учетом не рабочих дней
        /// </summary>
        private static int CountDaysWithoutWeekendsToDeadline(DateTime docPublishDate, int days)
        {
            IWindsorContainer container = ApplicationContext.Current.Container;
            int result = 0;
            int addDays = 0;
            var range = container.Resolve<IRepository<DayRecord>>().GetAll();
            var currentPoint = docPublishDate.Date;
            while (result < days)
            {
                if (range.Where(x => x.Date == currentPoint && (x.DayOff || x.Holyday)).FirstOrDefault() != null)
                {
                    currentPoint = currentPoint.AddDays(-1);
                    addDays++;
                }
                else
                {
                    currentPoint = currentPoint.AddDays(-1);
                    result++;
                }
            }
            return result + addDays;
        }
            
    }
}
