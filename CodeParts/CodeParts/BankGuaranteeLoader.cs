using Bars.B4.DataAccess;
using Bars.B4.Modules.Nsi.Interfaces.Domain;
using Bars.B4.Utils;
using Bars.BudgetPlaning.Dictionary.Entities.Base;
using Bars.UZ.Contracts.Entities;
using Bars.UZ.Contracts.Entities._44fz.Uz4Contract.Enums;
using Bars.UZ.Loaders.NsiEntities;
using Bars.UZ.Utils;
using Bars.UZ.Utils.Loaders;
using Bars.UZ.Utils.Loaders.DictLoader;
using Bars.UZ.Utils.Loaders.NsiEntities;
using Castle.Windsor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using NLog;
using Bars.BudgetPlaning.Dictionary;
using Bars.B4.DataModels;
using Bars.B4.Modules.FileStorage;
using Bars.BudgetPlaning.Common;
using System.IO;
using NHibernate.Cfg;
using NHibernate;
using NHibernate.Cfg.ConfigurationSchema;

namespace Bars.UZ.Loaders.DictionaryLoaders
{
    /// <summary>
    /// Загрузчик данных банковских гарантий из ЕИС.
    /// </summary>
    public class BankGuaranteeLoader : IBankGuaranteeLoader
    {
        public IWindsorContainer Container { get; set; }

        public Logger Logger { get; set; }

        public string Name => "Реестр банковских гарантий";

        public ISession session;

        Operator CurrentOperator { get; set; }

        /// <summary>
        /// Путь для загрузки файлов спраовчника
        /// </summary>
        public string Path => Container.Resolve<ISettingsIntegration>().GetDictionaryPath(SettingsDictionaryType.BankGuaranteeLoaderPath);

        protected BankGuaranteeLoader()
        {
            LoadedEntitiesId = new List<long>();
            FilterActualItems = true;
        }

        public BankGuaranteeLoader(IDataStore dataStore)
        {
            LoadedEntitiesId = new List<long>();
            FilterActualItems = true;
            this.dataStore = dataStore;
        }

        /// <summary>
        /// Были ли изменены значения каких-либо полей у записи
        /// </summary>
        /// <param name="record">Существующая в системе запись</param>
        /// <param name="item">Загружаемая запись</param>
        protected bool IsEntityNotChanged(Uz4BankGuarantee record, NsiExportBankGuarantee exportItem)
        {
            var item = exportItem.bankGuarantee;
            Uz4BankGaranteeTypeProcuring typeProcuring = Uz4BankGaranteeTypeProcuring.ApplicationInPurchase;

            if (item.Guarantee?.PurchaseRequestEnsure == null)
            {
                typeProcuring = Uz4BankGaranteeTypeProcuring.ContractExecution;
            }

            return record.OosId == item.OosId &&
                record.Status == GetStatusBG(exportItem.statusBG) &&
                IsPropertyNotChanged(record.RegNumBankGuarantee, item.RegNumBankGuarantee) &&
                IsPropertyNotChanged(record.CreditOrgNum, item.CreditOrgNum) &&
                IsPropertyNotChanged(record.DocNum, item.DocNum) &&
                IsPropertyNotChanged(record.ExtendedDocNum, item.ExtendedDocNum) &&
                record.ObjectVersion == item.ObjectVersion &&
                CompareDates(record.DocPublishDate, item.DocPublishDate) &&
                record.TypeProcuring == typeProcuring &&
                CheckPuchaseCodes(record, item) &&
                CompareDates(record.Date, item.Guarantee?.Date) &&
                IsPropertyNotChanged(record.Amount, item.Guarantee?.GuaranteeAmount) &&
                CompareDates(record.ExpireDate, item.Guarantee?.ExpireDate) &&
                CompareDates(record.EntryForceDate, item.Guarantee?.EntryForceDate) &&
                IsPropertyNotChanged(record.Procedure, item.Guarantee?.Procedure) &&
                IsPropertyNotChanged(record.CurrencyName, item.Guarantee?.Currency.Name) &&
                IsPropertyNotChanged(record.CurrencyCode, item.Guarantee?.Currency?.Code) &&
                IsPropertyNotChanged(record.AmountRur, item.Guarantee?.GuaranteeAmountRUR) &&
                record.CurrencyRate == item.Guarantee?.CurrencyRate &&
                IsPropertyNotChanged(record.PurchaceNum, (item.Guarantee != null ? (item.Guarantee.PurchaseRequestEnsure != null ? item.Guarantee.PurchaseRequestEnsure.PurchaseNumber :
                                                                            (item.Guarantee.ContractExecutionEnsure?.Purchase?.PurchaseNumber)) : null)) &&
                record.LotNum == (item.Guarantee != null ? (item.Guarantee.PurchaseRequestEnsure != null ? item.Guarantee.PurchaseRequestEnsure.LotNumber :
                                                                            (item.Guarantee.ContractExecutionEnsure?.Purchase?.LotNumber)) : 0) &&
                IsPropertyNotChanged(record.RegNum, item.Guarantee?.ContractExecutionEnsure?.RegNum) &&
                IsPropertyNotChanged(record.RegNumBank, item.Bank?.RegNum) &&
                IsPropertyNotChanged(record.NameBank, item.Bank?.FullName) &&
                IsPropertyNotChanged(record.ShortNameBank, item.Bank?.ShortName) &&
                IsPropertyNotChanged(record.PostAddressBank, item.Bank?.PostAddress) &&
                IsPropertyNotChanged(record.Inn, item.Bank?.INN) &&
                IsPropertyNotChanged(record.Kpp, item.Bank?.KPP) &&
                IsPropertyNotChanged(record.LocationAddressBank, item.Bank?.Location) &&
                CompareDates(record.DateRegistrationBank, item.Bank?.RegistrationDate) &&
                IsPropertyNotChanged(record.IkuBank, item.Bank?.IKU) &&
                IsPropertyNotChanged(record.LegalFormCodeBank, item.Bank?.LegalForm?.Code) &&
                IsPropertyNotChanged(record.LegalFormCodeName, item.Bank?.LegalForm?.SingularName) &&
                IsPropertyNotChanged(record.SubjectRfCodeBank, item.Bank?.SubjectRF?.Code) &&
                IsPropertyNotChanged(record.SubjectRfNameBank, item.Bank?.SubjectRF?.Name) &&
                IsPropertyNotChanged(record.LocationCodeBank, item.Bank?.OKTMO?.Code) &&
                IsPropertyNotChanged(record.LocationNameBank, item.Bank?.OKTMO?.Name) &&
                record.TypePrincipal == (item.SupplierInfo != null ? GetTypePrincipalBG(item.SupplierInfo.principalTypeItems) : Uz4BankGaranteeTypePrincipal.LegalRf) &&
                IsPropertyNotChanged(record.NamePrincipal, item.SupplierInfo?.principalType?.FullName) &&
                IsPropertyNotChanged(record.ShortNamePrincipal, item.SupplierInfo?.principalType?.ShortName) &&
                IsPropertyNotChanged(record.InnPrincipal, item.SupplierInfo?.principalType?.INN) &&
                IsPropertyNotChanged(record.KppPrincipal, item.SupplierInfo?.principalType?.KPP) &&
                IsPropertyNotChanged(record.Ogrn, item.SupplierInfo?.principalType?.OGRN) &&
                CompareDates(record.DateRegistrationPrincipal, item.SupplierInfo?.principalType?.RegistrationDate) &&
                IsPropertyNotChanged(record.LocationAddressPrincipal, item.SupplierInfo?.principalType?.Address) &&
                IsPropertyNotChanged(record.NamePrincipalLat, item.SupplierInfo?.principalType?.FullNameLat) &&
                IsPropertyNotChanged(record.TaxPayerCodePrincipal, item.SupplierInfo?.principalType?.TaxPayerCode) &&
                IsPropertyNotChanged(record.LastNamePrincipal, item.SupplierInfo?.principalType?.LastName) &&
                IsPropertyNotChanged(record.FirstNamePrincipal, item.SupplierInfo?.principalType?.FirstName) &&
                IsPropertyNotChanged(record.MiddleNamePrincipal, item.SupplierInfo?.principalType?.MiddleName) &&
                record.IsIpPrincipal == item.SupplierInfo?.principalType?.IsIP &&
                IsPropertyNotChanged(record.LastNamePrincipalLat, item.SupplierInfo?.principalType?.LastNameLat) &&
                IsPropertyNotChanged(record.FirstNamePrincipalLat, item.SupplierInfo?.principalType?.FirstNameLat) &&
                IsPropertyNotChanged(record.MiddleNamePrincipalLat, item.SupplierInfo?.principalType?.MiddleNameLat) &&
                IsPropertyNotChanged(record.LegalFormCodePrincipal, item.SupplierInfo?.principalType?.LegalForm?.Code) &&
                IsPropertyNotChanged(record.LegalFormNamePrincipal, item.SupplierInfo?.principalType?.LegalForm?.SingularName) &&
                IsPropertyNotChanged(record.SubjectRfCodePrincipal, item.SupplierInfo?.principalType?.SubjectRF?.Code) &&
                IsPropertyNotChanged(record.SubjectRfNamePrincipal, item.SupplierInfo?.principalType?.SubjectRF?.Name) &&
                IsPropertyNotChanged(record.OkatoCodePrincipal, item.SupplierInfo?.principalType?.OKATO?.Code) &&
                IsPropertyNotChanged(record.OkatoNamePrincipal, item.SupplierInfo?.principalType?.OKATO?.Name) &&
                IsPropertyNotChanged(record.LocationCodePrincipal, item.SupplierInfo?.principalType?.OKTMO?.Code) &&
                IsPropertyNotChanged(record.LocationNamePrincipal, item.SupplierInfo?.principalType?.OKTMO?.Name) &&
                IsPropertyNotChanged(record.CountryCodePrincipal, item.SupplierInfo?.principalType?.Country?.CountryCode) &&
                IsPropertyNotChanged(record.CountryNamePrincipal, item.SupplierInfo?.principalType?.Country?.CountryFullName) &&
                IsPropertyNotChanged(record.RegNumBeneficiar, item.Guarantee?.Customer?.RegNum) &&
                IsPropertyNotChanged(record.FullNameBeneficiar, item.Guarantee?.Customer?.FullName) &&
                IsPropertyNotChanged(record.ShortNameBeneficiar, item.Guarantee?.Customer?.ShortName) &&
                IsPropertyNotChanged(record.PostAddressBeneficiar, item.Guarantee?.Customer?.PostAddress) &&
                IsPropertyNotChanged(record.InnBeneficiar, item.Guarantee?.Customer?.INN) &&
                IsPropertyNotChanged(record.KppBeneficiar, item.Guarantee?.Customer?.KPP) &&
                IsPropertyNotChanged(record.OgrnBeneficiar, item.Guarantee?.Customer?.OGRN) &&
                IsPropertyNotChanged(record.LocationBeneficiar, item.Guarantee?.Customer?.Location) &&
                CompareDates(record.RegDateBeneficiar, item.Guarantee?.Customer?.RegistrationDate) &&
                IsPropertyNotChanged(record.IkuBeneficiar, item.Guarantee?.Customer?.IKU) &&
                IsPropertyNotChanged(record.LegalFormCodeBeneficiar, item.Guarantee?.Customer?.LegalForm?.Code) &&
                IsPropertyNotChanged(record.LegalFormNameBeneficiar, item.Guarantee?.Customer?.LegalForm?.SingularName) &&
                IsPropertyNotChanged(record.SubjectRfCodeBeneficiar, item.Guarantee?.Customer?.SubjectRF?.Code) &&
                IsPropertyNotChanged(record.SubjectRfNameBeneficiar, item.Guarantee?.Customer?.SubjectRF?.Name) &&
                IsPropertyNotChanged(record.LocationCodeBeneficiar, item.Guarantee?.Customer?.OKTMO?.Code) &&
                IsPropertyNotChanged(record.LocationNameBeneficiar, item.Guarantee?.Customer?.OKTMO?.Name) &&
                CompareDates(record.ModificationDateOos, item.Modification?.ModificationDate) &&
                IsPropertyNotChanged(record.ModificationInfo, item.Modification?.Info) &&
                IsPropertyNotChanged(record.InvalidReason, item.BankGuaranteeInvalid?.Reason) &&
                CheckRefusalReasons(record, item) &&
                IsPropertyNotChanged(record.RefusalInvalidNum, item.BankGuaranteeRefusalInvalid?.RefusalDocNumber) &&
                IsPropertyNotChanged(record.RefusalInvalidReason, item.BankGuaranteeRefusalInvalid?.Reason) &&
                CompareDates(record.TerminationDate, item.BankGuaranteeTermination?.BankGuaranteeTerminationregNumber?.TerminationDate) &&
                IsPropertyNotChanged(record.TerminationReason, item.BankGuaranteeTermination?.BankGuaranteeTerminationregNumber?.TerminationReason) &&
                IsPropertyNotChanged(record.TerminationInvalidNum, item.BankGuaranteeTerminationInvalid?.TerminationDocNumber) &&
                IsPropertyNotChanged(record.TerminationInvalidReason, item.BankGuaranteeTerminationInvalid?.Reason) &&
                IsPropertyNotChanged(record.ReturnNum, item.BankGuaranteeReturn?.ReturnDocNumber) &&
                IsPropertyNotChanged(record.ReturnInvalidNum, item.BankGuaranteeReturnInvalid?.ReturnDocNumber) &&
                IsPropertyNotChanged(record.ReturnInvalidReason, item.BankGuaranteeReturnInvalid?.Reason);
        }


        /// <summary>
        /// Получить экземпляр новой записи на основе загруженных данных
        /// </summary>
        /// <param name="source">Загружаемая запись</param>
        protected Uz4BankGuarantee GetNewEntity(NsiExportBankGuarantee exportItem)
        {
            var item = exportItem.bankGuarantee;

            Uz4BankGaranteeTypeProcuring typeProcuring = Uz4BankGaranteeTypeProcuring.ApplicationInPurchase;
            string purchaceNum; int lotNum;
            if (item.Guarantee?.PurchaseRequestEnsure == null)
            {
                typeProcuring = Uz4BankGaranteeTypeProcuring.ContractExecution;
                purchaceNum = item.Guarantee != null ? (item.Guarantee.ContractExecutionEnsure != null ? (item.Guarantee.ContractExecutionEnsure.Purchase != null ? item.Guarantee.ContractExecutionEnsure.Purchase.PurchaseNumber : null) : null) : null;
                lotNum = item.Guarantee != null ? (item.Guarantee.ContractExecutionEnsure != null ? (item.Guarantee.ContractExecutionEnsure.Purchase != null ? item.Guarantee.ContractExecutionEnsure.Purchase.LotNumber : 0) : 0) : 0;
            }
            else
            {
                purchaceNum = item.Guarantee != null ? (item.Guarantee.PurchaseRequestEnsure != null ? item.Guarantee.PurchaseRequestEnsure.PurchaseNumber : null) : null;
                lotNum = item.Guarantee != null ? (item.Guarantee.PurchaseRequestEnsure != null ? item.Guarantee.PurchaseRequestEnsure.LotNumber : 0) : 0;
            }

            return new Uz4BankGuarantee
            {
                Department = CurrentOperator.Organization,
                StateCode = "",
                VersionIsCurrent = true,
                OosId = item.OosId,
                Status = GetStatusBG(exportItem.statusBG),
                RegNumBankGuarantee = item.RegNumBankGuarantee != null ? item.RegNumBankGuarantee : string.Empty,
                CreditOrgNum = item.CreditOrgNum != null ? item.CreditOrgNum : string.Empty,
                DocNum = item.DocNum != null ? Truncate(item.DocNum, 22) : string.Empty,
                ExtendedDocNum = item.ExtendedDocNum != null ? Truncate(item.ExtendedDocNum, 22) : string.Empty,
                ObjectVersion = item.ObjectVersion,
                DocPublishDate = item.DocPublishDate,
                TypeProcuring = typeProcuring,
                Date = item.Guarantee != null ? item.Guarantee.Date : DateTime.MinValue,
                Amount = item.Guarantee != null ? item.Guarantee.GuaranteeAmount : string.Empty,
                ExpireDate = item.Guarantee != null ? item.Guarantee.ExpireDate : DateTime.MinValue,
                EntryForceDate = item.Guarantee != null ? item.Guarantee.EntryForceDate : DateTime.MinValue,
                Procedure = item.Guarantee != null ? item.Guarantee.Procedure : null,
                CurrencyName = item.Guarantee != null ? (item.Guarantee.Currency != null ? item.Guarantee.Currency.Name : null) : null,
                CurrencyCode = item.Guarantee != null ? (item.Guarantee.Currency != null ? item.Guarantee.Currency.Code : null) : null,
                AmountRur = item.Guarantee != null ? item.Guarantee.GuaranteeAmountRUR : null,
                CurrencyRate = item.Guarantee != null ? item.Guarantee.CurrencyRate : 0,
                PurchaceNum = purchaceNum,
                LotNum = lotNum,
                RegNum = item.Guarantee != null ? (item.Guarantee.ContractExecutionEnsure != null ? item.Guarantee.ContractExecutionEnsure.RegNum : null) : null,
                RegNumBank = item.Bank != null ? item.Bank.RegNum : null,
                NameBank = item.Bank != null ? (item.Bank.FullName != null ? item.Bank.FullName : string.Empty) : string.Empty,
                ShortNameBank = item.Bank != null ? item.Bank.ShortName : null,
                PostAddressBank = item.Bank != null ? item.Bank.PostAddress : null,
                Inn = item.Bank != null ? item.Bank.INN : null,
                Kpp = item.Bank != null ? item.Bank.KPP : null,
                LocationAddressBank = item.Bank != null ? item.Bank.Location : null,
                DateRegistrationBank = item.Bank != null ? item.Bank.RegistrationDate : DateTime.MinValue,
                IkuBank = item.Bank != null ? item.Bank.IKU : null,
                LegalFormCodeBank = item.Bank != null ? (item.Bank.LegalForm != null ? item.Bank.LegalForm.Code : null) : null,
                LegalFormCodeName = item.Bank != null ? (item.Bank.LegalForm != null ? item.Bank.LegalForm.SingularName : null) : null,
                SubjectRfCodeBank = item.Bank != null ? (item.Bank.SubjectRF != null ? item.Bank.SubjectRF.Code : null) : null,
                SubjectRfNameBank = item.Bank != null ? (item.Bank.SubjectRF != null ? item.Bank.SubjectRF.Name : null) : null,
                LocationCodeBank = item.Bank != null ? (item.Bank.OKTMO != null ? item.Bank.OKTMO.Code : null) : null,
                LocationNameBank = item.Bank != null ? (item.Bank.OKTMO != null ? item.Bank.OKTMO.Name : null) : null,
                TypePrincipal = item.SupplierInfo != null ? GetTypePrincipalBG(item.SupplierInfo.principalTypeItems) : Uz4BankGaranteeTypePrincipal.IndividualForeignState,
                NamePrincipal = item.SupplierInfo != null ? (item.SupplierInfo.principalType != null ? (item.SupplierInfo.principalType.FullName != null ? item.SupplierInfo.principalType.FullName : string.Empty) : string.Empty) : string.Empty,
                ShortNamePrincipal = item.SupplierInfo != null ? (item.SupplierInfo.principalType != null ? item.SupplierInfo.principalType.ShortName : null) : null,
                InnPrincipal = item.SupplierInfo != null ? (item.SupplierInfo.principalType != null ? item.SupplierInfo.principalType.INN : null) : null,
                KppPrincipal = item.SupplierInfo != null ? (item.SupplierInfo.principalType != null ? item.SupplierInfo.principalType.KPP : null) : null,
                Ogrn = item.SupplierInfo != null ? (item.SupplierInfo.principalType != null ? item.SupplierInfo.principalType.OGRN : null) : null,
                DateRegistrationPrincipal = item.SupplierInfo != null ? (item.SupplierInfo.principalType != null ? item.SupplierInfo.principalType.RegistrationDate : DateTime.MinValue) : DateTime.MinValue,
                LocationAddressPrincipal = item.SupplierInfo != null ? (item.SupplierInfo.principalType != null ? item.SupplierInfo.principalType.Address : null) : null,
                NamePrincipalLat = item.SupplierInfo != null ? (item.SupplierInfo.principalType != null ? item.SupplierInfo.principalType.FullNameLat : null) : null,
                TaxPayerCodePrincipal = item.SupplierInfo != null ? (item.SupplierInfo.principalType != null ? item.SupplierInfo.principalType.TaxPayerCode : null) : null,
                LastNamePrincipal = item.SupplierInfo != null ? (item.SupplierInfo.principalType != null ? item.SupplierInfo.principalType.LastName : null) : null,
                FirstNamePrincipal = item.SupplierInfo != null ? (item.SupplierInfo.principalType != null ? item.SupplierInfo.principalType.FirstName : null) : null,
                MiddleNamePrincipal = item.SupplierInfo != null ? (item.SupplierInfo.principalType != null ? item.SupplierInfo.principalType.MiddleName : null) : null,
                IsIpPrincipal = item.SupplierInfo != null ? (item.SupplierInfo.principalType != null ? item.SupplierInfo.principalType.IsIP : false) : false,
                LastNamePrincipalLat = item.SupplierInfo != null ? (item.SupplierInfo.principalType != null ? item.SupplierInfo.principalType.LastNameLat : null) : null,
                FirstNamePrincipalLat = item.SupplierInfo != null ? (item.SupplierInfo.principalType != null ? item.SupplierInfo.principalType.FirstNameLat : null) : null,
                MiddleNamePrincipalLat = item.SupplierInfo != null ? (item.SupplierInfo.principalType != null ? item.SupplierInfo.principalType.MiddleNameLat : null) : null,
                LegalFormCodePrincipal = item.SupplierInfo != null ? (item.SupplierInfo.principalType != null ? (item.SupplierInfo.principalType.LegalForm != null ? item.SupplierInfo.principalType.LegalForm.Code : null) : null) : null,
                LegalFormNamePrincipal = item.SupplierInfo != null ? (item.SupplierInfo.principalType != null ? (item.SupplierInfo.principalType.LegalForm != null ? item.SupplierInfo.principalType.LegalForm.SingularName : null) : null) : null,
                SubjectRfCodePrincipal = item.SupplierInfo != null ? (item.SupplierInfo.principalType != null ? (item.SupplierInfo.principalType.SubjectRF != null ? item.SupplierInfo.principalType.SubjectRF.Code : null) : null) : null,
                SubjectRfNamePrincipal = item.SupplierInfo != null ? (item.SupplierInfo.principalType != null ? (item.SupplierInfo.principalType.SubjectRF != null ? item.SupplierInfo.principalType.SubjectRF.Name : null) : null) : null,
                OkatoCodePrincipal = item.SupplierInfo != null ? (item.SupplierInfo.principalType != null ? (item.SupplierInfo.principalType.OKATO != null ? item.SupplierInfo.principalType.OKATO.Code : null) : null) : null,
                OkatoNamePrincipal = item.SupplierInfo != null ? (item.SupplierInfo.principalType != null ? (item.SupplierInfo.principalType.OKATO != null ? item.SupplierInfo.principalType.OKATO.Name : null) : null) : null,
                LocationCodePrincipal = item.SupplierInfo != null ? (item.SupplierInfo.principalType != null ? (item.SupplierInfo.principalType.OKTMO != null ? item.SupplierInfo.principalType.OKTMO.Code : null) : null) : null,
                LocationNamePrincipal = item.SupplierInfo != null ? (item.SupplierInfo.principalType != null ? (item.SupplierInfo.principalType.OKTMO != null ? item.SupplierInfo.principalType.OKTMO.Name : null) : null) : null,
                CountryCodePrincipal = item.SupplierInfo != null ? (item.SupplierInfo.principalType != null ? (item.SupplierInfo.principalType.Country != null ? item.SupplierInfo.principalType.Country.CountryCode : null) : null) : null,
                CountryNamePrincipal = item.SupplierInfo != null ? (item.SupplierInfo.principalType != null ? (item.SupplierInfo.principalType.Country != null ? item.SupplierInfo.principalType.Country.CountryFullName : null) : null) : null,
                RegNumBeneficiar = item.Guarantee != null ? (item.Guarantee.Customer != null ? item.Guarantee.Customer.RegNum : null) : null,
                FullNameBeneficiar = item.Guarantee != null ? (item.Guarantee.Customer != null ? item.Guarantee.Customer.FullName : string.Empty) : string.Empty,
                ShortNameBeneficiar = item.Guarantee != null ? (item.Guarantee.Customer != null ? item.Guarantee.Customer.ShortName : null) : null,
                PostAddressBeneficiar = item.Guarantee != null ? (item.Guarantee.Customer != null ? item.Guarantee.Customer.PostAddress : null) : null,
                InnBeneficiar = item.Guarantee != null ? (item.Guarantee.Customer != null ? item.Guarantee.Customer.INN : null) : null,
                KppBeneficiar = item.Guarantee != null ? (item.Guarantee.Customer != null ? item.Guarantee.Customer.KPP : null) : null,
                OgrnBeneficiar = item.Guarantee != null ? (item.Guarantee.Customer != null ? item.Guarantee.Customer.OGRN : null) : null,
                LocationBeneficiar = item.Guarantee != null ? (item.Guarantee.Customer != null ? item.Guarantee.Customer.Location : null) : null,
                RegDateBeneficiar = item.Guarantee != null ? (item.Guarantee.Customer != null ? item.Guarantee.Customer.RegistrationDate : DateTime.MinValue) : DateTime.MinValue,
                IkuBeneficiar = item.Guarantee != null ? (item.Guarantee.Customer != null ? item.Guarantee.Customer.IKU : null) : null,
                LegalFormCodeBeneficiar = item.Guarantee != null ? (item.Guarantee.Customer != null ? (item.Guarantee.Customer.LegalForm != null ? item.Guarantee.Customer.LegalForm.Code : null) : null) : null,
                LegalFormNameBeneficiar = item.Guarantee != null ? (item.Guarantee.Customer != null ? (item.Guarantee.Customer.LegalForm != null ? item.Guarantee.Customer.LegalForm.SingularName : null) : null) : null,
                SubjectRfCodeBeneficiar = item.Guarantee != null ? (item.Guarantee.Customer != null ? (item.Guarantee.Customer.SubjectRF != null ? item.Guarantee.Customer.SubjectRF.Code : null) : null) : null,
                SubjectRfNameBeneficiar = item.Guarantee != null ? (item.Guarantee.Customer != null ? (item.Guarantee.Customer.SubjectRF != null ? item.Guarantee.Customer.SubjectRF.Name : null) : null) : null,
                LocationCodeBeneficiar = item.Guarantee != null ? (item.Guarantee.Customer != null ? (item.Guarantee.Customer.OKTMO != null ? item.Guarantee.Customer.OKTMO.Code : null) : null) : null,
                LocationNameBeneficiar = item.Guarantee != null ? (item.Guarantee.Customer != null ? (item.Guarantee.Customer.OKTMO != null ? item.Guarantee.Customer.OKTMO.Name : null) : null) : null,
                ModificationDateOos = item.Modification != null ? item.Modification.ModificationDate : DateTime.MinValue,
                ModificationInfo = item.Modification != null ? item.Modification.Info : null,
                InvalidReason = item.BankGuaranteeInvalid != null ? item.BankGuaranteeInvalid.Reason : null,
                RefusalInvalidNum = item.BankGuaranteeRefusalInvalid != null ? Truncate(item.BankGuaranteeRefusalInvalid.RefusalDocNumber, 22) : null,
                RefusalInvalidReason = item.BankGuaranteeRefusalInvalid != null ? item.BankGuaranteeRefusalInvalid.Reason : null,
                TerminationDate = item.BankGuaranteeTermination != null ? (item.BankGuaranteeTermination.BankGuaranteeTerminationregNumber != null ? item.BankGuaranteeTermination.BankGuaranteeTerminationregNumber.TerminationDate : DateTime.MinValue) : DateTime.MinValue,
                TerminationReason = item.BankGuaranteeTermination != null ? (item.BankGuaranteeTermination.BankGuaranteeTerminationregNumber != null ? item.BankGuaranteeTermination.BankGuaranteeTerminationregNumber.TerminationReason : null) : null,
                TerminationInvalidNum = item.BankGuaranteeTerminationInvalid != null ? Truncate(item.BankGuaranteeTerminationInvalid.TerminationDocNumber, 22) : null,
                TerminationInvalidReason = item.BankGuaranteeTerminationInvalid != null ? item.BankGuaranteeTerminationInvalid.Reason : null,
                ReturnNum = item.BankGuaranteeReturn != null ? Truncate(item.BankGuaranteeReturn.ReturnDocNumber, 22) : null,              
                ReturnInvalidNum = item.BankGuaranteeReturnInvalid != null ? Truncate(item.BankGuaranteeReturnInvalid.ReturnDocNumber, 22) : null,
                ReturnInvalidReason = item.BankGuaranteeReturnInvalid?.Reason
            };
        }

        /// <summary>
        /// Получить экземпляр новой версии записи на основе существующей в системе записи
        /// </summary>
        /// <param name="source">Существующая в системе запись</param>
        protected Uz4BankGuarantee GetNewEntity(Uz4BankGuarantee item)
        {
            return new Uz4BankGuarantee
            {
                OosId = item.OosId,
                StatusBp = BpStatusType.Черновик,
                Name = item.Name,
                Status = item.Status,
                RegNumBankGuarantee = item.RegNumBankGuarantee,
                CreditOrgNum = item.CreditOrgNum,
                DocNum = item.DocNum,
                ExtendedDocNum = item.ExtendedDocNum,
                ObjectVersion = item.ObjectVersion,
                DocPublishDate = item.DocPublishDate,
                TypeProcuring = item.TypeProcuring,
                Date = item.Date,
                Amount = item.Amount,
                ExpireDate = item.ExpireDate,
                EntryForceDate = item.EntryForceDate,
                Procedure = item.Procedure,
                CurrencyName = item.CurrencyName,
                CurrencyCode = item.CurrencyCode,
                AmountRur = item.AmountRur,
                CurrencyRate = item.CurrencyRate,
                PurchaceNum = item.PurchaceNum,
                LotNum = item.LotNum,
                RegNum = item.RegNum,
                RegNumBank = item.RegNumBank,
                NameBank = item.NameBank,
                ShortNameBank = item.ShortNameBank,
                PostAddressBank = item.PostAddressBank,
                Inn = item.Inn,
                Kpp = item.Kpp,
                LocationAddressBank = item.LocationAddressBank,
                DateRegistrationBank = item.DateRegistrationBank,
                IkuBank = item.IkuBank,
                LegalFormCodeBank = item.LegalFormCodeBank,
                LegalFormCodeName = item.LegalFormCodeName,
                SubjectRfCodeBank = item.SubjectRfCodeBank,
                SubjectRfNameBank = item.SubjectRfNameBank,
                LocationCodeBank = item.LocationCodeBank,
                LocationNameBank = item.LocationNameBank,
                TypePrincipal = item.TypePrincipal,
                NamePrincipal = item.NamePrincipal,
                ShortNamePrincipal = item.ShortNamePrincipal,
                InnPrincipal = item.InnPrincipal,
                KppPrincipal = item.KppPrincipal,
                Ogrn = item.Ogrn,
                DateRegistrationPrincipal = item.DateRegistrationPrincipal,
                LocationAddressPrincipal = item.LocationAddressPrincipal,
                NamePrincipalLat = item.NamePrincipalLat,
                TaxPayerCodePrincipal = item.TaxPayerCodePrincipal,
                LastNamePrincipal = item.LastNamePrincipal,
                FirstNamePrincipal = item.FirstNamePrincipal,
                MiddleNamePrincipal = item.MiddleNamePrincipal,
                IsIpPrincipal = item.IsIpPrincipal,
                LastNamePrincipalLat = item.LastNamePrincipalLat,
                FirstNamePrincipalLat = item.FirstNamePrincipalLat,
                MiddleNamePrincipalLat = item.MiddleNamePrincipalLat,
                LegalFormCodePrincipal = item.LegalFormCodePrincipal,
                LegalFormNamePrincipal = item.LegalFormNamePrincipal,
                SubjectRfCodePrincipal = item.SubjectRfCodePrincipal,
                SubjectRfNamePrincipal = item.SubjectRfNamePrincipal,
                OkatoCodePrincipal = item.OkatoCodePrincipal,
                OkatoNamePrincipal = item.OkatoNamePrincipal,
                LocationCodePrincipal = item.LocationCodePrincipal,
                LocationNamePrincipal = item.LocationNamePrincipal,
                CountryCodePrincipal = item.CountryCodePrincipal,
                CountryNamePrincipal = item.CountryNamePrincipal,
                RegNumBeneficiar = item.RegNumBeneficiar,
                FullNameBeneficiar = item.FullNameBeneficiar,
                ShortNameBeneficiar = item.ShortNameBeneficiar,
                PostAddressBeneficiar = item.PostAddressBeneficiar,
                InnBeneficiar = item.InnBeneficiar,
                KppBeneficiar = item.KppBeneficiar,
                OgrnBeneficiar = item.OgrnBeneficiar,
                LocationBeneficiar = item.LocationBeneficiar,
                RegDateBeneficiar = item.RegDateBeneficiar,
                IkuBeneficiar = item.IkuBeneficiar,
                LegalFormCodeBeneficiar = item.LegalFormCodeBeneficiar,
                LegalFormNameBeneficiar = item.LegalFormNameBeneficiar,
                SubjectRfCodeBeneficiar = item.SubjectRfCodeBeneficiar,
                SubjectRfNameBeneficiar = item.SubjectRfNameBeneficiar,
                LocationCodeBeneficiar = item.LocationCodeBeneficiar,
                LocationNameBeneficiar = item.LocationNameBeneficiar,
                ModificationDateOos = item.ModificationDateOos,
                ModificationInfo = item.ModificationInfo,
                InvalidReason = item.InvalidReason,
                RefusalInvalidNum = item.RefusalInvalidNum,
                RefusalInvalidReason = item.RefusalInvalidReason,
                TerminationDate = item.TerminationDate,
                TerminationReason = item.TerminationReason,
                TerminationInvalidNum = item.TerminationInvalidNum,
                TerminationInvalidReason = item.TerminationInvalidReason,
                ReturnNum = item.ReturnNum,
                ReturnDate = item.ReturnDate,
                ReturnReason = item.ReturnReason,
                ReturnInvalidNum = item.ReturnInvalidNum,
                ReturnInvalidReason = item.ReturnInvalidReason
            };
        }

        /// <summary>
        /// Получить выражение для фильтрации по идентификатору сущности
        /// </summary>
        /// <param name="id">Значение фильтра идентификатора</param>
        protected Expression<Func<Uz4BankGuarantee, bool>> GetIdFilterExpression(string id)
        {
            return o => o.RegNumBankGuarantee == id;
        }

        /// <summary>
        /// Получить значение идентификатора загружаемой записи
        /// </summary>
        /// <param name="item">Загружаемая запись</param>
        protected string GetId(NsiBankGuarantee item)
        {
            return item.RegNumBankGuarantee;
        }

        protected IDataStore dataStore;

        /// <summary>
        /// ID загруженных и прошедших фильтр записей
        /// </summary>
        public List<long> LoadedEntitiesId { get; set; }

        /// <summary>
        /// Фильтровать ли записи с установленным флагом actual к сохранению в системе
        /// </summary>
        protected bool FilterActualItems { get; set; }

        /// <summary>
        /// Пропускать ли процедуру сброса существующей записи
        /// </summary>
        protected bool SkipEntityReseting { get; set; }

        /// <summary>
        /// Полная ли загрузка
        /// </summary>
        public bool NeedFullLoad { get; set; }

        public int Count => GetCount();

        /// <summary>
        /// Не было ли изменений в коллекции записей и их полях
        /// </summary>
        /// <param name="entities">Существующие записи</param>
        /// <param name="items">Загружаемые записи</param>
        public bool IsEntityCollectionNotChanged(IEnumerable<Uz4BankGuarantee> entities, IEnumerable<NsiExportBankGuarantee> items)
        {
            var currentEntities = entities.ToArray();
            var entityItems = currentEntities.ToDictionary(o => o, o => (NsiExportBankGuarantee)null);

            foreach (var nsiItem in items)
                if (!SkipLoading(nsiItem))
                {
                    if (nsiItem.bankGuarantee != null)
                    {
                        var entity = currentEntities.AsQueryable().FirstOrDefault(GetIdFilterExpression(GetId(nsiItem.bankGuarantee)));
                        if (entity != null)
                            entityItems[entity] = nsiItem;
                    }
                }

            return entityItems.Values.All(o => o != null) && entityItems.All(o => IsEntityNotChanged(o.Key, o.Value));
        }

        /// <summary>
        /// Загрузить составные справочники
        /// </summary>
        /// <param name="record">Созданная в системе запись</param>
        /// <param name="item">Загружаемая запись</param>
        /// <param name="count">Счетчик созданных записей</param>
        protected virtual void LoadSubItems(Uz4BankGuarantee record, NsiBankGuarantee item, out int count)
        {
            count = 0;
        }

        /// <summary>
        /// Пропустить ли загрузку записи
        /// </summary>
        /// <param name="item">Загружаемая запись</param>
        protected virtual bool SkipLoading(NsiExportBankGuarantee item)
        {

            var oosItem = item;
            return oosItem == null;
        }

        /// <summary>
        /// Загрузить запись
        /// </summary>
        /// <param name="item">Загружаемая запись</param>
        /// <param name="count">Счетчик созданных записей</param>
        /// <returns>Созданные записи</returns>
        public virtual Uz4BankGuarantee LoadEntity(NsiExportBankGuarantee item, out int count)
        {
            count = 0;

            if (SkipLoading(item)) return null;
            if (item.bankGuarantee == null) return null;

            var resultRecords = new List<Uz4BankGuarantee>();

            var oosId = GetId(item.bankGuarantee);
            var records = dataStore.GetAll<Uz4BankGuarantee>().Where(GetIdFilterExpression(oosId)).ToList();

            LoadedEntitiesId.AddRange(records.Select(o => o.Id));

            if (records.Count <= 0)
                records.Add(new Uz4BankGuarantee { MetaId = Guid.NewGuid() });

            var record = records.First();

            if (records.Count > 1)  // если записи разможились по какой-то причине, делаем их все неактуальными на уровне домена
            {
                foreach (var badRecord in records)
                {
                    using (IStatelessSession statelessSession = Container.Resolve<ISessionProvider>().OpenStatelessSession())
                    using (ITransaction tx = statelessSession.BeginTransaction())
                    {
                        statelessSession.Update(badRecord);
                        tx.Commit();
                    }
                }
            }
            else if (record.Id > 0) // если запись не новая, смотрим надо ли делать версию
            {
                // если изменений нет, то оставляем как есть
                if (IsEntityNotChanged(record, item))
                    return record;

                // иначе, ставим как неактуальную на уровне домена
                using (IStatelessSession statelessSession = Container.Resolve<ISessionProvider>().OpenStatelessSession())
                using (ITransaction tx = statelessSession.BeginTransaction())
                {
                    statelessSession.InsertOrUpdate(record);
                    tx.Commit();
                }

                if (item.bankGuarantee.PurchaseCodes != null)
                    AddPurchaseCodes(item.bankGuarantee, record);
                

                if (item.bankGuarantee.RefusalInfo != null)
                    AddRefusalReasons(item.bankGuarantee, record);

                if (item.bankGuarantee.GuaranteeReturns != null)
                    AddGuaranteeReturns(item.bankGuarantee, record);

                if (item.bankGuarantee.agreementDocuments != null)
                {
                    var attachment = dataStore.GetAll<Uz4BankGuaranteeDoc>().Where(x => x.BankGuarantee.Id == record.Id).ToList();
                    if (attachment.IsEmpty())
                        AddAttachmentsToRecord(item.bankGuarantee, record);
                }

                return record;
            }

            // если дошли до этой точки, то либо пришла новая запись, либо появились изменения для записи, либо имело место размножение записей
            var newRecord = GetNewEntity(item);
            newRecord.MetaId = record.MetaId;

            if (newRecord is ISimpleDictionary)
            {
                (newRecord as ISimpleDictionary).Author = "import";
            }
            using (IStatelessSession statelessSession = Container.Resolve<ISessionProvider>().OpenStatelessSession())
            using (ITransaction txn = statelessSession.BeginTransaction())
            {
                statelessSession.InsertOrUpdate(newRecord);
                txn.Commit();
            }

            if (item.bankGuarantee.PurchaseCodes != null)
                AddPurchaseCodes(item.bankGuarantee, newRecord);

            if (item.bankGuarantee.RefusalInfo != null)
                AddRefusalReasons(item.bankGuarantee, newRecord);

            if (item.bankGuarantee.GuaranteeReturns != null)
                AddGuaranteeReturns(item.bankGuarantee, newRecord);

            if (item.bankGuarantee.agreementDocuments != null)
                AddAttachmentsToRecord(item.bankGuarantee, newRecord);

            resultRecords.Add(newRecord);
            count++;

            int subCount;
            LoadSubItems(newRecord, item.bankGuarantee, out subCount);
            count += subCount;

            LoadedEntitiesId.Add(newRecord.Id);

            return newRecord;
        }

        /// <summary>
        /// Добавить идентификационны коды закупки
        /// </summary>
        /// <param name="item">Загружаемая запись банковской гарантии</param>
        /// /// <param name="record">Запись созданная на домене </param>
        protected void AddPurchaseCodes(NsiBankGuarantee item, Uz4BankGuarantee record)
        {
            foreach (var code in item.PurchaseCodes)
            {
                var newCode = new RefusalPurchase
                {
                    PurchaseCode = code,
                    BankGuarantee = record
                };
                using (IStatelessSession statelessSession = Container.Resolve<ISessionProvider>().OpenStatelessSession())
                using (ITransaction txn = statelessSession.BeginTransaction())
                {
                    statelessSession.InsertOrUpdate(newCode);
                    txn.Commit();
                }
            }
        }

        /// <summary>
        /// Добавить идентификационны коды закупки
        /// </summary>
        /// <param name="item">Загружаемая запись банковской гарантии</param>
        /// /// <param name="record">Запись созданная на домене </param>
        protected void AddGuaranteeReturns(NsiBankGuarantee item, Uz4BankGuarantee record)
        {
            var guaranteeReturns = dataStore.GetAll<GuaranteeReturn>().Where(x => x.BankGuarantee == record); 

            foreach (var el in item.GuaranteeReturns.WaiverNotice)
            {
                if (guaranteeReturns.Where(x => x.GuaranteeReturnDate == el.NoticeDate && x.GuaranteeReturnNoticeNumber == el.NoticeNumber
                 && x.GuaranteeReturnReason == el.NoticeReason && x.GuaranteeReturnType == BankGuaranteeReturnType.ReturnToGarantInfo).Count() > 0)
                    continue;

                var newGuaranteeReturn = new GuaranteeReturn
                {
                    GuaranteeReturnDate = el.NoticeDate,
                    GuaranteeReturnNoticeNumber = el.NoticeNumber,
                    GuaranteeReturnReason = el.NoticeReason,
                    BankGuarantee = record
                };
                using (IStatelessSession statelessSession = Container.Resolve<ISessionProvider>().OpenStatelessSession())
                using (ITransaction txn = statelessSession.BeginTransaction())
                {
                    statelessSession.InsertOrUpdate(newGuaranteeReturn);
                    txn.Commit();
                }
            }

            foreach (var el in item.GuaranteeReturns.bankGuaranteeReturn)
            {
                if (guaranteeReturns.Where(x => x.GuaranteeReturnDate == el.returnDate && x.GuaranteeReturnNoticeNumber == el.DocNumber
                 && x.GuaranteeReturnReason == el.ReturnReason && x.GuaranteeReturnType == BankGuaranteeReturnType.ReturnToGarant).Count() > 0)
                    continue;

                var newGuaranteeReturn = new GuaranteeReturn
                {
                    GuaranteeReturnDate = el.returnDate,
                    GuaranteeReturnNoticeNumber = el.DocNumber,
                    GuaranteeReturnReason = el.ReturnReason,
                    BankGuarantee = record
                };
                using (IStatelessSession statelessSession = Container.Resolve<ISessionProvider>().OpenStatelessSession())
                using (ITransaction txn = statelessSession.BeginTransaction())
                {
                    statelessSession.InsertOrUpdate(newGuaranteeReturn);
                    txn.Commit();
                }
            }
        }

        /// <summary>
        /// Добавить причины отказа
        /// </summary>
        /// <param name="item">Загружаемая запись банковской гарантии</param>
        /// /// <param name="record">Запись созданная на домене </param>
        protected void AddRefusalReasons(NsiBankGuarantee item, Uz4BankGuarantee record)
        {
            var refusalReasons = dataStore.GetAll<RefusalReasons>().Where(x => x.BankGuarantee == record);

            if (item.RefusalInfo.RefusalReasons != null)
            {
                foreach (var reason in item.RefusalInfo.RefusalReasons)
                {
                    if (refusalReasons != null)
                    {
                        var existRecords = refusalReasons.Where(x => x.GuaranteeRefusalCode == reason.Code && x.GuaranteeRefusalName == reason.Name);
                        if (existRecords.Count() > 0) continue;
                    }

                    var newRecord = new RefusalReasons
                    {
                        GuaranteeRefusalCode = reason.Code,
                        GuaranteeRefusalName = reason.Name,
                        BankGuarantee = record
                    };
                    using (IStatelessSession statelessSession = Container.Resolve<ISessionProvider>().OpenStatelessSession())
                    using (ITransaction txn = statelessSession.BeginTransaction())
                    {
                        statelessSession.InsertOrUpdate(newRecord);
                        txn.Commit();
                    }
                }
            }
        }

        /// <summary>
        /// Добавить вложенные файлы к записанной банковской гарнтии
        /// </summary>
        /// <param name="item">Загружаемая запись банковской гарантии</param>
        /// /// <param name="record">Запись созданная на домене </param>
        protected void AddAttachmentsToRecord(NsiBankGuarantee item, Uz4BankGuarantee record)
        {
            var tempDirPath = System.IO.Path.GetTempPath() + Guid.NewGuid();
            foreach (var attachment in item.agreementDocuments)
            {
                var newAttachment = new Uz4BankGuaranteeDoc
                {
                    MetaId = Guid.NewGuid(),
                    BankGuarantee = record,
                    RegNum = attachment?.RegDocNumber,
                    Description = attachment?.DocDescription,
                    FileName = attachment?.FileName,
                    Document = downloadAttachmentByUrl(attachment, tempDirPath)
                };
                using (IStatelessSession statelessSession = Container.Resolve<ISessionProvider>().OpenStatelessSession())
                using (ITransaction txn = statelessSession.BeginTransaction())
                {
                    statelessSession.InsertOrUpdate(newAttachment); //.Save(newAttachment);
                    txn.Commit();
                }
            }
            if (Directory.Exists(tempDirPath)) Directory.Delete(tempDirPath, true);
        }

        /// <summary>
        /// Сбросить (установить как неактуальную на уровне домена) существующую запись
        /// </summary>
        /// <param name="entityId">Идентификатор записи</param>
        /// <returns>Количество сброшенных записей</returns>
        protected B4.Modules.FileStorage.FileInfo downloadAttachmentByUrl(NsiAttachmentBankGuarantee item, string tempDirPath)
        {
            var fileManager = Container.Resolve<IFileManager>();

            B4.Modules.FileStorage.FileInfo file = new B4.Modules.FileStorage.FileInfo();
            using (var webClient = new WebClient())
            {
                webClient.Headers.Add("User-Agent: Mozilla/5.0 (Windows NT 6.1; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/55.0.2883.87 Safari/537.36");

                Directory.CreateDirectory(tempDirPath);
                var fileName = System.IO.Path.Combine(tempDirPath, item.FileName);

                try
                {
                    webClient.DownloadFile(item.Url, fileName);
                }
                catch (Exception ex)
                {
                }

                using (IStatelessSession statelessSession = Container.Resolve<ISessionProvider>().OpenStatelessSession())
                using (ITransaction txn = statelessSession.BeginTransaction())
                {

                    System.IO.FileInfo f = new System.IO.FileInfo(fileName);
                    file = new B4.Modules.FileStorage.FileInfo()
                    {
                        Extention = f.Extension,
                        Name = f.Name,
                        Size = f.Length,
                        CheckSum = f.GetHashCode().ToString()
                    };

                    statelessSession.InsertOrUpdate(file);
                    txn.Commit();
                }
            }

            return file;
        }

        /// <summary>
        /// Сбросить (установить как неактуальную на уровне домена) существующую запись
        /// </summary>
        /// <param name="entityId">Идентификатор записи</param>
        /// <returns>Количество сброшенных записей</returns>
        public virtual bool ResetEntity(long entityId)
        {
            var record = dataStore.GetAll<Uz4BankGuarantee>()
                .FirstOrDefault(o => o.Id == entityId);

            if (record == null)
                return false;

            using (IStatelessSession statelessSession = Container.Resolve<ISessionProvider>().OpenStatelessSession())
            using (ITransaction tx = statelessSession.BeginTransaction())
            {
                statelessSession.InsertOrUpdate(record);
                tx.Commit();
            }

            var newRecord = GetNewEntity(record);
            newRecord.MetaId = record.MetaId;
            newRecord.OosId = record.OosId;
            newRecord.Name = record.Name;

            if (newRecord is ISimpleDictionary)
            {
                (newRecord as ISimpleDictionary).Author = "import";
            }

            using (IStatelessSession statelessSession = Container.Resolve<ISessionProvider>().OpenStatelessSession())
            using (ITransaction txn = statelessSession.BeginTransaction())
            {
                statelessSession.InsertOrUpdate(newRecord);
                txn.Commit();
            }

            return true;
        }

        /// <summary>
        /// Получить идентификаторы записей для сброса
        /// </summary>
        /// <param name="includedEntitiesId">Идентификаторы загруженных и прошедших фильтр загрузчика записей</param>
        public virtual long[] ListExcludedEntitiesId(long[] includedEntitiesId)
        {
            return dataStore.GetAll<Uz4BankGuarantee>().WhereNotContains(x => x.Id, includedEntitiesId)
                .Select(x => x.Id).ToArray();
        }

        /// <summary>
        /// Загрузить запись
        /// </summary>
        /// <param name="item">Загружаемая запись</param>
        /// <returns>Количество созданных записей</returns>
        public int LoadEntity(NsiExportBankGuarantee item)
        {
            int count;
            using (IStatelessSession statelessSession = Container.Resolve<ISessionProvider>().OpenStatelessSession())
            using (ITransaction txn = statelessSession.BeginTransaction())
            {
                LoadEntity(item, out count);
                txn.Commit();
            }

            return count;
        }

        /// <summary>
        /// Получить количество актуальных (на уровне домена) записей
        /// </summary>
        /// <returns></returns>
        public int GetCount()
        {
            return dataStore.GetAll<Uz4BankGuarantee>().Count();
        }

        /// <summary>
        /// Выполнение операций перед загрузкой
        /// </summary>
        protected void OnBeforeLoad()
        {
        }

        /// <summary>
        /// Сбросить неиспользуемые записи
        /// </summary>
        protected void OnAfterLoad()
        {
            if (SkipEntityReseting)
                return;

            var excluded = ListExcludedEntitiesId(LoadedEntitiesId.ToArray());

            foreach (var id in excluded)
                ResetEntity(id);
        }

        /// <summary>
        /// Сравнить строковые значения
        /// </summary>
        /// <param name="entityProperty">Значение поля существующей записи</param>
        /// <param name="itemProperty">Значение поля загружаемой записи</param>
        protected bool IsPropertyNotChanged(string entityProperty, string itemProperty)
        {
            return (entityProperty ?? string.Empty) == (itemProperty ?? string.Empty);
        }

        /// <summary>
        /// Загрузить банковскую гарнтию с оператором выполняющим загрузку 
        /// </summary>
        /// <param name="lastSync">Дата последней загрузки</param>
        /// <param name="currentOperator">Оператор загрузки</param>
        public void LoadBankGuarantee(DateTime lastSync, Operator currentOperator)
        {
            this.CurrentOperator = currentOperator;
            Load(lastSync);
        }

        /// <summary>
        /// Загрузить банковскую гарнтию
        /// </summary>
        /// <param name="lastSync">Дата последней загрузки</param>
        public void Load(DateTime lastSync)
        {
            var settingIntegration = Container.Resolve<ISettingsIntegration>();
            // Доступ со специальными правами, отличными от стандартных
            var customNsiUser = settingIntegration.GetValue<string>(SettingsType.NsiUser2);
            var customNsiPassword = settingIntegration.GetValue<string>(SettingsType.NsiPassword2);
            session = this.Container.Resolve<ISessionProvider>().GetCurrentSession();

            using (var downloadHepler = Container.Resolve<IOosEntityDownloadHelper<NsiExportBankGuarantee>>(new { Logger, customNsiUser, customNsiPassword, lastSync}))
            {
                var fileNames = downloadHepler.DownloadXmlFiles(Path, NeedFullLoad);

                //var fileNames = System.IO.Directory.GetFiles(@"C:\TestBG2");

                OnBeforeLoad();

                long counterFiles = 0;

                foreach (var fileName in fileNames)
                {
                    counterFiles++;
                    var fileData = downloadHepler.GetListFileData(fileName);

                    if (fileData == null)
                        continue;
                    var counterItems = 0;
                    foreach (var root in fileData)
                    {
                        Logger.Info(string.Format("Десериализировано файлов: {0}, количество записей: {1}", counterFiles, counterItems));
                        counterItems += LoadEntity(root);
                    }
                }

                OnAfterLoad();

                Logger.Info("Все записи загружены или обновлены");
            }
        }

        [Obsolete("Теперь используем соответствующие параметры *Path из UZ.Settings")]
        public string GetKey()
        {
            throw new NotImplementedException();
        }

        [Obsolete("Теперь используем свойство Name")]
        public string GetName()
        {
            return Name;
        }

        /// <summary>
        /// Получить статус банковской гарантии полученного из файла загрузки
        /// </summary>
        protected Uz4BankGaranteeStatus GetStatusBG(StatusBG code)
        {
            Uz4BankGaranteeStatus status = Uz4BankGaranteeStatus.Issued;
            switch (code)
            {
                case StatusBG.bankGuarantee: status = Uz4BankGaranteeStatus.Issued; break;
                case StatusBG.bankGuaranteeInvalid: status = Uz4BankGaranteeStatus.InvalidityInfo; break;
                case StatusBG.bankGuaranteeRefusal: status = Uz4BankGaranteeStatus.RefusalCustomer; break;
                case StatusBG.bankGuaranteeRefusalInvalid: status = Uz4BankGaranteeStatus.InvalidityRefusalCustomer; break;
                case StatusBG.bankGuaranteeTermination: status = Uz4BankGaranteeStatus.TerminationObligationSupplier; break;
                case StatusBG.bankGuaranteeTerminationInvalid: status = Uz4BankGaranteeStatus.InvalidityInfoTerminationObligationSupplier; break;
                case StatusBG.bankGuaranteeReturn: status = Uz4BankGaranteeStatus.Return; break;
                case StatusBG.bankGuaranteeReturnInvalid: status = Uz4BankGaranteeStatus.InvalidateInfoReturn; break;
            };

            return status;
        }

        /// <summary>
        /// Получить Вид принципала загруженного из файла
        /// </summary>
        protected Uz4BankGaranteeTypePrincipal GetTypePrincipalBG(PrincipalType type)
        {
            Uz4BankGaranteeTypePrincipal typePrincipal = Uz4BankGaranteeTypePrincipal.IndividualForeignState;
            switch (type)
            {
                case PrincipalType.legalEntityRF: typePrincipal = Uz4BankGaranteeTypePrincipal.LegalRf; break;
                case PrincipalType.legalEntityForeignState: typePrincipal = Uz4BankGaranteeTypePrincipal.LegalForeignState; break;
                case PrincipalType.individualPersonRF: typePrincipal = Uz4BankGaranteeTypePrincipal.IndividualRf; break;
                case PrincipalType.individualPersonForeignState: typePrincipal = Uz4BankGaranteeTypePrincipal.IndividualForeignState; break;
            };
            return typePrincipal;
        }

        /// <summary>
        /// Сравнить даты по времени и дате без локации
        /// </summary>
        protected bool CompareDates(DateTime? recordDate, DateTime? itemDate)
        {
            if (!itemDate.HasValue)
            {
                if (recordDate.HasValue)
                    return (recordDate.Value.Year == DateTime.MinValue.Year && recordDate.Value.Month == DateTime.MinValue.Month && recordDate.Value.Day == DateTime.MinValue.Day &&
                    recordDate.Value.Hour == DateTime.MinValue.Hour && recordDate.Value.Minute == DateTime.MinValue.Minute && recordDate.Value.Second == DateTime.MinValue.Second);
            }

            if (itemDate.HasValue && recordDate.HasValue)
                return (recordDate.Value.Year == itemDate.Value.Year && recordDate.Value.Month == itemDate.Value.Month && recordDate.Value.Day == itemDate.Value.Day &&
                    recordDate.Value.Hour == itemDate.Value.Hour && recordDate.Value.Minute == itemDate.Value.Minute && recordDate.Value.Second == itemDate.Value.Second);
            return false;
        }

        /// <summary>
        /// Сократить строку полученную из файла для записи в базу 
        /// </summary>
        protected string Truncate(string value, int maxChars)
        {
            return value.Length <= maxChars ? value : value.Substring(0, maxChars);
        }

        /// <summary>
        /// Проверяем существую ли причины возврата для данной банковской гарантии
        /// </summary>
        protected bool CheckRefusalReasons(Uz4BankGuarantee record, NsiBankGuarantee item)
        {
            var refusalResons = dataStore.GetAll<RefusalReasons>().Where(x => x.Id == record.Id);

            var reasons = item.RefusalInfo?.RefusalReasons;
            if (reasons != null)
                foreach (var el in reasons)
                {
                    var existRecord = refusalResons.Where(x => x.GuaranteeRefusalCode == el.Code && x.GuaranteeRefusalName == el.Name);
                    if (existRecord.Count() == 0)
                        return false;
                }

            return true;
        }

        /// <summary>
        /// Проверяем существую ли Идентификационный код закупки для данной банковской гарантии
        /// </summary>
        protected bool CheckPuchaseCodes(Uz4BankGuarantee record, NsiBankGuarantee item)
        {
            var codesInBase = dataStore.GetAll<RefusalPurchase>().Where(x => x.BankGuarantee.Id == record.Id);

            var codes = item.PurchaseCodes;

            if (codes != null)
            {
                foreach (var code in codes)
                    if (codesInBase.Where(x => x.PurchaseCode == code).Count() == 0)
                        return false;
            }

            return true;
        }
    }
}
