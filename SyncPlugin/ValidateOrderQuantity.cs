using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace SyncPlugin
{
    public class ValidateOrderQuantity : IPlugin
    {
        private ITracingService tracingService;

        public void Execute(IServiceProvider serviceProvider)
        {
            tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));

            if (context.InputParameters.Contains("Target") && context.InputParameters["Target"] is Entity)
            {
                Entity order = (Entity)context.InputParameters["Target"];

                IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
                IOrganizationService service = serviceFactory.CreateOrganizationService(context.UserId);

                try
                {
                    //  Start of business logic


                    //  Get salesorderdetails
                    Trace("Getting the sales order details");
                    var salesorderdetails = service.RetrieveMultiple(new QueryExpression
                    {
                        EntityName = "salesorderdetail",
                        ColumnSet = new ColumnSet("quantity"),
                        Criteria = new FilterExpression
                        {
                            Conditions =
                            {
                                new ConditionExpression
                                {
                                    AttributeName = "salesorderid",
                                    Operator = ConditionOperator.Equal,
                                    Values = { order.Id }
                                }
                            }
                        }
                    }).Entities;

                    //  Loop through each item in the order, getting the quantity
                    Trace("Checking each sales order detail");
                    foreach (var salesorderdetail in salesorderdetails)
                    {
                        //  Get the quantity of the order
                        int orderQuantity = (int)salesorderdetail["quantity"];

                        //  Get the product info
                        Entity product = service.Retrieve("product", context.InitiatingUserId, new ColumnSet("name","quantityonhand"));
                        int productQuantity = (int)product["quantityonhand"];

                        //  Check if there is enough inventory to make the order
                        if (orderQuantity > productQuantity)
                        {
                            throw new InvalidPluginExecutionException($"Quantity on this order exceeds the quantity on hand by {productQuantity - orderQuantity} units for product:{product["name"]}." +
                                $"Please, lower the quantity of the order before creating the order.");
                        }
                    }


                    //  End of business logic
                }
                catch (FaultException<OrganizationServiceFault> ex)
                {
                    throw new InvalidPluginExecutionException("An error occurred in FollowupPlugin.", ex);
                }
                catch (Exception ex)
                {
                    tracingService.Trace($"{ex.ToString()}");
                    throw;
                }
            }
        }

        private void Trace(string msg)
        {
            tracingService.Trace($"{GetType().Name} ({DateTime.Now}): {msg}");
        }
    }
}
