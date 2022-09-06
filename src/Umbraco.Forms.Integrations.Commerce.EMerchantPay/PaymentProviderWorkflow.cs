﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Umbraco.Forms.Core;
using Umbraco.Forms.Core.Enums;
using Umbraco.Forms.Core.Models;
using Umbraco.Forms.Integrations.Commerce.EMerchantPay.Builders;
using Umbraco.Forms.Integrations.Commerce.EMerchantPay.Configuration;
using Umbraco.Forms.Integrations.Commerce.EMerchantPay.ExtensionMethods;
using Umbraco.Forms.Integrations.Commerce.EMerchantPay.Helpers;
using Umbraco.Forms.Integrations.Commerce.EMerchantPay.Models.Dtos;
using Umbraco.Forms.Integrations.Commerce.EMerchantPay.Services;

#if NETCOREAPP
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

using Umbraco.Cms.Core.Web;
#else
using System.Configuration;

using Umbraco.Web;
using Umbraco.Forms.Core.Persistence.Dtos;
#endif

namespace Umbraco.Forms.Integrations.Commerce.EMerchantPay
{
    public class PaymentProviderWorkflow : WorkflowType
    {
        private readonly PaymentProviderSettings _paymentProviderSettings;

        private readonly IHttpContextAccessor _httpContextAccessor;

        private readonly ConsumerService _consumerService;

        private readonly PaymentService _paymentService;

        private readonly UrlHelper _urlHelper;

        private readonly IMappingService<Mapping> _mappingService;

        #region WorkflowSettings

        [Core.Attributes.Setting("Amount",
            Description = "Payment amount (without decimals)",
            View = "TextField")]
        public string Amount { get; set; }

        [Core.Attributes.Setting("Currency",
            Description = "Payment currency",
            View = "~/App_Plugins/UmbracoForms.Integrations/Commerce/eMerchantPay/currency.html")]
        public string Currency { get; set; }

        [Core.Attributes.Setting("Number of Items",
           Description = "Map number of items with form field. If selected, final amount will be Amount x NumberOfItems.",
           View = "~/App_Plugins/UmbracoForms.Integrations/Commerce/eMerchantPay/field-picker.html")]
        public string NumberOfItems { get; set; }

        [Core.Attributes.Setting("Record Status",
           Description = "Map payment record status with form field",
           View = "~/App_Plugins/UmbracoForms.Integrations/Commerce/eMerchantPay/field-picker.html")]
        public string RecordStatus { get; set; }

        [Core.Attributes.Setting("Record Payment Unique ID",
           Description = "Map payment unique ID with form field",
           View = "~/App_Plugins/UmbracoForms.Integrations/Commerce/eMerchantPay/field-picker.html")]
        public string UniqueId { get; set; }

        [Core.Attributes.Setting("Customer Details",
            Description = "Map customer details with form fields",
            View = "~/App_Plugins/UmbracoForms.Integrations/Commerce/eMerchantPay/customer-details-mapper.html")]
        public string CustomerDetailsMappings { get; set; }

        [Core.Attributes.Setting("Success URL",
            View = "Pickers.Content")]
        public string SuccessUrl { get; set; }

        [Core.Attributes.Setting("Failure URL",
            View = "Pickers.Content")]
        public string FailureUrl { get; set; }

        [Core.Attributes.Setting("Cancel URL",
            View = "Pickers.Content")]
        public string CancelUrl { get; set; }

        #endregion

#if NETCOREAPP
        public PaymentProviderWorkflow(IOptions<PaymentProviderSettings> paymentProviderSettings, IHttpContextAccessor httpContextAccessor,
            ConsumerService consumerService, PaymentService paymentService, UrlHelper urlHelper, IMappingService<Mapping> mappingService)
#else
        public PaymentProviderWorkflow(IHttpContextAccessor httpContextAccessor,
                ConsumerService consumerService, PaymentService paymentService, UrlHelper urlHelper, IMappingService<Mapping> mappingService)
#endif
        {
            Id = new Guid(Constants.WorkflowId);
            Name = "eMerchantPay Gateway";
            Description = "eMerchantPay provider handling form-based payments.";
            Icon = "icon-multiple-credit-cards";

            _consumerService = consumerService;

            _paymentService = paymentService;

            _httpContextAccessor = httpContextAccessor;

            _urlHelper = urlHelper;
            
            _mappingService = mappingService;

#if NETCOREAPP
            _paymentProviderSettings = paymentProviderSettings.Value;
#else
            _paymentProviderSettings = new PaymentProviderSettings(ConfigurationManager.AppSettings);
#endif
        }

#if NETCOREAPP
        public override WorkflowExecutionStatus Execute(WorkflowExecutionContext context)
#else
        public override WorkflowExecutionStatus Execute(Record record, RecordEventArgs e)
#endif
        {
            if (!_mappingService.TryParse(CustomerDetailsMappings, out var mappings)) return WorkflowExecutionStatus.Failed;

            var mappingBuilder = new MappingBuilder()
#if NETCOREAPP
                .SetValues(context.Record, mappings)
#else
                .SetValues(record, mappings)
#endif
                .Build();

            // step 1. Create or Retrieve Consumer
            var consumer = new ConsumerDto { Email = mappingBuilder.Email };

            // step 1. Create Consumer
            var createConsumerTask = Task.Run(async () => await _consumerService.Create(consumer));

            var result = createConsumerTask.Result;
            if (result.Code == Constants.ErrorCode.ConsumerExists)
            {
                // step 1.1. Get Consumer
                var retrieveConsumerTask = Task.Run(async () => await _consumerService.Retrieve(consumer));
                consumer = retrieveConsumerTask.Result;
            }

            // step 2. Create Payment
            var random = new Random();
            var transactionId = $"uc-{random.Next(1000000, 999999999)}";

#if NETCOREAPP
            var formId = context.Record.Form;
            var recordUniqueId = context.Record.UniqueId;
#else
            var formId = record.Form;
            var recordUniqueId = record.UniqueId;
#endif
            var uniqueIdKey = UniqueId;  
            var statusKey = RecordStatus;


            var numberOfItems = string.IsNullOrEmpty(NumberOfItems)
                ? 0
#if NETCOREAPP
                : int.Parse(context.Record.RecordFields[Guid.Parse(NumberOfItems)].ValuesAsString());
#else
                : int.Parse(record.RecordFields[Guid.Parse(NumberOfItems)].ValuesAsString());
#endif
            var payment = new PaymentDto
            {
                TransactionId = transactionId.ToString(),
                Usage = _paymentProviderSettings.Usage,
                NotificationUrl = $"{_paymentProviderSettings.UmbracoBaseUrl}umbraco/api/paymentprovider/notifypayment" +
                    $"?formId={formId}&recordUniqueId={recordUniqueId}&statusFieldId={statusKey}",
                ReturnSuccessUrl = _urlHelper.GetPageUrl(int.Parse(SuccessUrl)),
                ReturnFailureUrl = _urlHelper.GetPageUrl(int.Parse(FailureUrl)),
                ReturnCancelUrl = _urlHelper.GetPageUrl(int.Parse(CancelUrl)),
                Amount = numberOfItems != 0
                    ? numberOfItems * int.Parse(Amount)
                    : int.Parse(Amount),
                Currency = Currency,
                ConsumerId = consumer.Id,
                CustomerEmail = consumer.Email,
                CustomerPhone = mappingBuilder.Phone,
                BillingAddress = new AddressDto
                {
                    FirstName = mappingBuilder.FirstName,
                    LastName = mappingBuilder.LastName,
                    Address1 = mappingBuilder.Address,
                    Address2 = string.Empty,
                    ZipCode = mappingBuilder.ZipCode,
                    City = mappingBuilder.City,
                    State = mappingBuilder.State,
                    Country = mappingBuilder.Country
                },
                BusinessAttribute = new BusinessAttribute { NameOfTheSupplier = _paymentProviderSettings.Supplier },
                TransactionTypes = new TransactionTypeDto
                {
                    TransactionTypes = _paymentProviderSettings.TransactionTypes.Split(';')
                                        .Select(p => new TransactionTypeRecordDto { TransactionType = p })
                                        .ToList()
                }
            };

            var createPaymentTask = Task.Run(async () => await _paymentService.Create(payment));

            var createPaymentResult = createPaymentTask.Result;

            if (createPaymentResult.Status != "error")
            {
                // add unique ID and status to record
#if NETCOREAPP
                context.Record.RecordFields[Guid.Parse(uniqueIdKey)].Values.Add(createPaymentResult.UniqueId);
                context.Record.RecordFields[Guid.Parse(statusKey)].Values.Add(createPaymentResult.Status);
#else
                record.RecordFields[Guid.Parse(uniqueIdKey)].Values.Add(createPaymentResult.UniqueId);
                record.RecordFields[Guid.Parse(statusKey)].Values.Add(createPaymentResult.Status);
#endif

                _httpContextAccessor.HttpContext.Items[Core.Constants.ItemKeys.RedirectAfterFormSubmitUrl] = createPaymentResult.RedirectUrl;

                return WorkflowExecutionStatus.Completed;
            }

#if NETCOREAPP
            context.Record.RecordFields[Guid.Parse(statusKey)].Values.Add("error");
#else
            record.RecordFields[Guid.Parse(statusKey)].Values.Add("error");
#endif

            return WorkflowExecutionStatus.Failed;
        }

        public override List<Exception> ValidateSettings()
        {
            var list = new List<Exception>();

            if (string.IsNullOrEmpty(Amount) || !int.TryParse(Amount, out _))
                list.Add(new Exception("Amount value is not valid."));

            if (string.IsNullOrEmpty(Currency)) list.Add(new Exception("Currency field is required."));

            if (string.IsNullOrEmpty(RecordStatus)) list.Add(new Exception("Record Status field is required."));

            if (string.IsNullOrEmpty(UniqueId)) list.Add(new Exception("Payment Unique ID field is required."));

            if (!_mappingService.TryParse(CustomerDetailsMappings, out _))
                list.Add(new Exception("Invalid mappings. Email at least is required."));

            if (!SuccessUrl.IsContentValid(nameof(SuccessUrl), out var successError))
                list.Add(new Exception(successError));

            if (!FailureUrl.IsContentValid(nameof(FailureUrl), out var failureError))
                list.Add(new Exception(failureError));

            if (!CancelUrl.IsContentValid(nameof(CancelUrl), out var cancelError)) 
                list.Add(new Exception(cancelError));

            return list;
        }
    }
}
