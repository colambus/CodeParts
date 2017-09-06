using Bars.B4.DataAccess;
using Bars.B4.DataModels;
using Bars.B4.Modules.Reports;
using Bars.B4.Modules.StimulReportGenerator;
using Bars.B4.Utils;
using Bars.UZ.Contracts.Entities;
using Bars.UZ.Contracts.Enums;
using Bars.UZ.ServiceLayer.Purchasing;
using Castle.Windsor;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace Bars.UZ.Reports
{
    /// <summary>
	/// Генератор отчётов для Документации о закупке 44-ФЗ со способом определения поставщика "Открытый конкурс", "Электронный аукцион", "Запрос котировок", "Закрытый конкурс"
	/// на основе Стимул технологий
	/// </summary>
    public class DocumantationStimulReport : BaseStimulReport<DocumentationStimulReportParams>
    {
        public IWindsorContainer Container { get; set; }
        public IDataStore DataStore { get; set; }

        private Documentation documentation;
        private List<LotInfo> purchasingLots;
        // преимущества участников
        private List<ParticipantBenefitsData> LotParticipantBenefitsData { get; set; } = new List<ParticipantBenefitsData>();
        // требования к участникам
        private List<ParticipantRequirementsData> LotParticipantRequirementsData { get; set; } = new List<ParticipantRequirementsData>();
        private  Contracts.Entities.PurchasingRequest PurchasingRequest { get; set; }
        // ТРУ
        private List<LotSpec> LotSpecData { get; set; } = new List<LotSpec>();
        #region properties

        public PurchasingLotSpecCrudService PurchasingLotSpecCrudService { get; set; }
        public PurchasingLotRequirementsCrudService PurchasingLotRequirementsCrudService { get; set; }
        public PurchasingLotPlacementFeatureCrudService PurchasingLotPlacementFeatureCrudService { get; set; }
        public Minfin.Core.AutoMapper.IModelMapper ModelMapper { get; set; }
        #endregion

        #region data source & business objects definitions 
        // переменные для сокрытия/показа различных частей в общем репорте
        private class ReportConditions
        {
            // Показывать основную доп. информацию для "Электронного аукциона"
            public bool ShowMainInfoEA { get; set; }
            // Дополнительная инфо о контракте
            public bool ShowMainContractAdditioanlInfaBand { get; set; }
            // Доп. данные о процедуре закупки для "Электронного аукциона"
            public bool ShowPurchaseProcedureAddEA { get; set; }
            // Доп. данные о процедуре закупки для "Открытого конкурса"
            public bool PurchaseProedureAddOK { get; set; }
            // Доп. данные о процедуре закупки для "Закрытый конкурс"
            public bool ShowPurchaseRequestZP { get; set; }
            // оп. данные о процедуре закупки для "Запрос котировок"
            public bool ShowPurchaseRequestZK { get; set; }
            // Отображать наименование для лота
            public bool ShowLotInfoDataBandName { get; set; }
            //Блок обоснования финансирования
            public bool ShowLotInfoBandFinanceExplanation { get; set; }
            // Блок планирования финансирования
            public bool LotInfoFinancePlan { get; set; }
            // Информация о возможности одностороннего отказа
            public bool ShowLotInfoFinancePlanRejection { get; set; }
            // Условия, запреты и ограничения допуска товаров
            public bool PurchasingSubjectRequirements { get; set; }
            // Конкурсная документация
            public bool ShowTenderDocumantation { get; set; }
            // Документация о проведении запроса предложений
            public bool ShowTenderRequestDocumantation { get; set; }
            // Показывать положение о законе
            public bool ShowLowExplanation { get; set; }
        };
        #endregion

        public override Stream GetDefaultTemplate()
        {
            var directory = HttpContext.Current.Server.MapPath("~/reportForms");
            if (Directory.Exists(directory))
            {
                var form = Path.Combine(directory, "Documentation.mrt");
                if(File.Exists(form))
                    return new ReportTemplateBinary(File.ReadAllBytes(form)).GetTemplate();
            }
                
            return new MemoryStream(Properties.Resources.Documentation);
        }

        /// <summary> Построение отчёта </summary>
		protected override StimulTemplateData ComputeTemplateData(DocumentationStimulReportParams parameters)
        {
            var reportData = GetReportData(parameters);

            var stimulTemplateData = new StimulTemplateData();


            foreach (ReportData data in reportData)
            {
                if (data.IsCollection)
                {
                    stimulTemplateData.RegisterData(data.Name, (IList)data.Obj);
                }
                else
                {
                    stimulTemplateData.RegisterBusinessObject(data.Name, data.Obj);
                }
            }

            return stimulTemplateData;
        }

        /// <summary>
		/// Получить данные отчета
		/// </summary>
        private List<ReportData> GetReportData(DocumentationStimulReportParams parameters)
        {
            documentation = DataStore.Get<Documentation>(parameters.DocumentationId);
            PurchasingRequest = documentation.PurchasingRequestCopy;
            BuildDataSources();

            var list = new List<ReportData>
            {
                // условные переменные, скрывающие некоторые части отчёта
				GetShowVariablesData(),
                // Основной объект
                GetMainReportBusinessObject(),
                // Информация о лотах
				GetLotInfo(),
                // ТРУ
				GetLotSpecData(),
				// преимущества участников
				GetParticipantBenefitsData(),
				// требования к участникам
				GetParticipantRequirementsData(),
            };

            return list;
        }

        /// <summary>
		/// генерация необходимых источников данных(списки, если нужно связанные)
		/// </summary>
		private void BuildDataSources()
        {
            // лоты
            purchasingLots = DataStore.GetAll<PurchasingLot>()
                .Where(x => x.PurchasingRequest.Id == PurchasingRequest.Id)
                .Select(Lot2InfoProjectionExpression).ToList();

            // дополняем преимущества участников
            purchasingLots.ForEach(x => BuildFeatures(x.Id));

            // дополняем требования к участникам
            purchasingLots.ForEach(x => BuildRequirements(x.Id));

            // ТРУ
            purchasingLots.ForEach(x => BuildLotTru(x.Id));
        }

        /// <summary>
		/// Получаем информацию о лотах
		/// </summary>
        private ReportData GetLotInfo()
        {
            return new ReportData()
            {
                Name = "LotInfo",
                Obj = purchasingLots.ToList(),
                IsCollection = true
            };
        }

        /// <summary>
		/// ReportData по ТРУ 
		/// </summary>
		/// <returns></returns>
		private ReportData GetLotSpecData()
        {
            return new ReportData()
            {
                Name = "LotSpec",
                Obj = LotSpecData,
                IsCollection = true
            };
        }

        /// <summary>
		/// Получаем основную информацию о документации
		/// </summary>
        private ReportData GetMainReportBusinessObject()
        {
            var purchasingRequestCrudService = Container.Resolve<PurchasingRequestCrudService>();
            var purchasingRequestFormDTO = purchasingRequestCrudService.Get(new EntityId<Contracts.Entities.PurchasingRequest>() { Id = PurchasingRequest.Id });
            Container.Release(purchasingRequestCrudService);

            var custContact = DataStore.GetAll<PurchasingContact>()
                .Where(x => x.PurchasingRequest.Id == PurchasingRequest.Id && x.ContactTypeEn == ContactTypeEn.Customer)
                .Select(x => new
                {
                    FirstName = x.Operator != null ? x.Operator.FirstName : x.FirstName,
                    SurName = x.Operator != null ? x.Operator.LastName : x.SurName,
                    Patronymic = x.Operator != null ? x.Operator.MiddleName : x.Patronymic,
                    x.Email,
                    x.Phone
                })
                .FirstOrDefault(); // для докментации берем только первую запись

            var docs = DataStore.GetAll<DocumentationDoc>()
               .Where(x => x.Documentation.Id == documentation.Id)
               .OrderByDescending(x => x.Attachment.ObjectEditDate)
               .Select(x => new
               {
                   DocName = x.Attachment.Name + (x.Attachment.Extention != null && x.Attachment.Extention != "" ? "." + x.Attachment.Extention : ""),
               })
               .ToList();

            object buisnessObject = new
            {
                // Номер 
                Num = purchasingRequestFormDTO.Num,

                // Наименование объекта закупки
                Name = purchasingRequestFormDTO.Name,

                // Способ определения поставщика (подрядчика, исполнителя)
                PlacingWay = purchasingRequestFormDTO.PlacingWay?.Name,

                // Организация, осуществляющая закупку: Уровень
                CustomerDepartmentFullName = purchasingRequestFormDTO.CustomerDepartment?.FullName,

                // Организация, осуществляющая закупку: Почтовый адрес
                CustomerPostalAddress = purchasingRequestFormDTO.CustomerDepartment?.PostAddress,

                // Организация, осуществляющая закупку: Адрес местонахождения
                CustomerAddress = purchasingRequestFormDTO.CustomerDepartment?.Address,

                //Ответственное должностное лицо
                CustomerContactFio = custContact != null ? $"{custContact.SurName} {custContact.FirstName} {custContact.Patronymic}" : null,
                
                //Адрес электронной почты
                CustomerContactEmail = custContact != null ? custContact.Email : null,

                //Номер контактного телефона
                CustomerContactPhone = custContact != null ? custContact.Phone : null,

                //Перечень прикрепленных документов
                DocsAllNumberedList = string.Join("\n", docs.Select((x, index) => $"{index + 1}. {x.DocName}")),

                //Дата и время размещения извещения
                PlacementPlanDate = purchasingRequestFormDTO.PlacementPlanDate
            };

            return new ReportData()
            {
                Name = "PurchasingRequest",
                Obj = buisnessObject,
                IsCollection = false
            };
        }

        /// <summary>
        /// условные переменные, скрывающие некоторые части отчёта
        /// <summary>
        private ReportData GetShowVariablesData()
        {
            var conditions = new ReportConditions();
            if(PurchasingRequest.PlacingWay != null)
            {
                switch (PurchasingRequest.PlacingWay.PlacingType)
                {
                    case BudgetPlaning.Dictionary.Enums.TenderPlanPlacingType.OK:
                        {
                            conditions.PurchaseProedureAddOK = true;
                            conditions.ShowLotInfoDataBandName = true;
                            conditions.LotInfoFinancePlan = true;
                            conditions.PurchasingSubjectRequirements = true;
                            conditions.ShowTenderDocumantation = true;
                            conditions.ShowLowExplanation = true;
                            break;
                        }
                    case BudgetPlaning.Dictionary.Enums.TenderPlanPlacingType.EF:
                        {
                            conditions.ShowMainInfoEA = true;
                            conditions.ShowPurchaseProcedureAddEA = true;
                            conditions.LotInfoFinancePlan = true;
                            conditions.PurchasingSubjectRequirements = true;
                            conditions.ShowLowExplanation = true;
                            break;
                        }
                    case BudgetPlaning.Dictionary.Enums.TenderPlanPlacingType.ZK:
                        {
                            conditions.ShowPurchaseRequestZK = true;
                            conditions.ShowMainContractAdditioanlInfaBand = true;
                            conditions.ShowLotInfoBandFinanceExplanation = true;
                            conditions.ShowLotInfoFinancePlanRejection = true;
                            break;
                        }
                    case BudgetPlaning.Dictionary.Enums.TenderPlanPlacingType.ZP:
                        {
                            conditions.ShowPurchaseRequestZP = true;
                            conditions.ShowTenderRequestDocumantation = true;
                            break;
                        }
                }
            }

            return new ReportData()
            {
                Name = "ReportConditions",
                Obj = conditions,
                IsCollection = false
            };
        }

        /// <summary>
		/// ReportData по преимуществам для участников 
		/// </summary>
		/// <returns></returns>
		private ReportData GetParticipantBenefitsData()
        {
            return new ReportData()
            {
                Name = "Benefits",
                Obj = LotParticipantBenefitsData,
                IsCollection = true
            };
        }

        /// <summary>
        /// ReportData по требованиям к участникам
        /// </summary>
        /// <returns></returns>
        private ReportData GetParticipantRequirementsData()
        {
            return new ReportData()
            {
                Name = "Requirements",
                Obj = LotParticipantRequirementsData,
                IsCollection = true
            };
        }

        /// <summary>
		/// генерация связанных с лотами ТРУ
		/// </summary>
		/// <param name="lotId"></param>
		private void BuildLotTru(long lotId)
        {
            PurchasingLotSpecCrudService.Query(new PurchasingLotSpecFilter { PurchasingLotId = lotId })
                .ToList()
                .Select(x => ModelMapper.GetModelMapExpression<PurchasingLotSpecListDto, LotSpec>().Compile()(x))
                .ForEach(x => { x.LotId = lotId; LotSpecData.Add(x); });
        }

        /// <summary>
		/// генерация связанных с лотами преимуществ участников
		/// </summary>
		/// <param name="lotId"></param>
		private void BuildFeatures(long lotId)
        {
            PurchasingLotPlacementFeatureCrudService.QueryByLot(new PurchasingLotPlacementFeatureFilter { PurchasingLotId = lotId }, new B4.ListParam())
                .Data
                .Where(x => x.Added)
                .Select(x => ModelMapper.GetModelMapExpression<PurchasingLotBenefitsListDto, ParticipantBenefitsData>().Compile()(x))
                .ForEach(x => { x.LotId = lotId; LotParticipantBenefitsData.Add(x); });
        }

        /// <summary>
        /// генерация связанных с лотами требований к участникам
        /// </summary>
        /// <param name="lotId"></param>
        private void BuildRequirements(long lotId)
        {
            PurchasingLotRequirementsCrudService.QueryByLot(new PurchasingLotPlacementFeatureFilter { PurchasingLotId = lotId }, new B4.ListParam())
                .Data
                .Where(x => x.Added)
                .Select(x => ModelMapper.GetModelMapExpression<PurchasingLotRequirementsListDto, ParticipantRequirementsData>().Compile()(x))
                .ForEach(x => { x.LotId = lotId; LotParticipantRequirementsData.Add(x); });
        }

        // ТРУ для репорта
        private class LotSpec : PurchasingLotSpecListDto
        {
            public long LotId { get; set; }
        }

        // преимущества участников
        private class ParticipantBenefitsData : PurchasingLotBenefitsListDto
        {
            public long LotId { get; set; }
        }
        // требования к участникам
        private class ParticipantRequirementsData : PurchasingLotRequirementsListDto
        {
            public long LotId { get; set; }
        }

        // информация о лоте
        private class LotInfo
        {
            public long Id { get; set; }

            #region additional data 
            // Наименование объекта закупки для лота
            public string LotName { get; set; }
            // Идентификационный код закупки
            public string PurchaseId { get; set; }
            // валюта
            public string LotCurrency { get; set; }
            // Начальная (максимальная) цена контракта
            public decimal? Imp { get; set; }
            //Обоснование начальной (максимальной) цены контракта
            public string RImp { get; set; }
            //Источник финансирования
            public string FinancingInfo { get; set; }
            //Место доставки товара
            public string DeliveryPoint { get; set; }
            //Сроки поставки товара
            public string DeliveryDate { get; set; }
            //Информация о возможности одностороннего отказа от исполнения контракта
            public string UnilaterialRefusalInfo { get; set; }
            //Ограничение участия в определении поставщика
            public string LimitParticipation { get; set; }
            //Условия, запреты и ограничения допуска товаров
            public string LimitAdmission { get; set; }
            //Обеспечение исполнения контракта
            public bool ProvidingContract { get; set; }
            //Размер обеспечения исполнения контракта
            public decimal? ProvidingContractImpSum { get; set; }
            //Порядок предоставления обеспечения исполнения контракта
            public string ProvidingContractMakingMoney { get; set; }
            // расчётный счёт в обеспечении исполнения контракта
            public string ProvidingContractBankAccountNum { get; set; }
            // БИК в обеспечении исполнения контракта
            public string ProvidingContractBankBik { get; set; }
            // требуется обеспечение заявки
            public bool ProvidingTender { get; set; }
            // Сумма от НМЦ
            public decimal? ProvidingTenderImpSum { get; set; }
            // Порядок предоставления обеспечения заявки
            public string ProvidingTenderMakingMoney { get; set; }
            // расчётный счёт в обеспечении исполнения контракта
            public string ProvidingTenderBankAccountNum { get; set; }
            // БИК в обеспечении исполнения контракта
            public string ProvidingTenderBankBik { get; set; }

            #endregion
        };

        /// <summary>
		/// проекция сущность -> наше DTO
		/// </summary>
		private static readonly Expression<Func<PurchasingLot, LotInfo>> Lot2InfoProjectionExpression = lot => new LotInfo
        {
            Id = lot.Id,
            LotName = lot.PurchaseObjectName,
            PurchaseId = lot.Purchase.PurchaseId,
            Imp = lot.PurchasePrice,
            RImp = lot.RImp,
            LotCurrency = lot.Okv.Name,
            FinancingInfo = lot.FinancingSource,
            DeliveryPoint = lot.DeliveryPoint,
            DeliveryDate = lot.DeliveryDate,
            UnilaterialRefusalInfo = lot.UnilaterialRefusalInfo,
            LimitParticipation = lot.LimitParticipation,
            LimitAdmission = lot.LimitAdmission,
            ProvidingContract = lot.ProvidingContract,
            ProvidingContractImpSum = lot.PurchasePrice / 100 * lot.ContractPercentNmck,
            ProvidingContractMakingMoney = lot.ContractMakingMoney,
            ProvidingContractBankAccountNum = lot.ContractBankAccounts.BankAccountNum,
            ProvidingContractBankBik = lot.ContractBankAccounts.BankBik,
            ProvidingTender = lot.ProvidingTender,
            ProvidingTenderImpSum = lot.PurchasePrice / 100 * lot.TenderPercentNmck,
            ProvidingTenderMakingMoney = lot.TenderMakingMoney,
            ProvidingTenderBankAccountNum = lot.TenderBankAccounts.BankAccountNum,
            ProvidingTenderBankBik = lot.TenderBankAccounts.BankBik,
        };

    }
}
