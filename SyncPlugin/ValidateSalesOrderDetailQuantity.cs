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
    public class ValidateSalesOrderDetailQuantity : IPlugin
    {
        private ITracingService tracingService;

        public void Execute(IServiceProvider serviceProvider)
        {
            tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));

            if (context.InputParameters.Contains("Target") && context.InputParameters["Target"] is Entity)
            {
                Entity salesorderdetail = (Entity)context.InputParameters["Target"];

                IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
                IOrganizationService service = serviceFactory.CreateOrganizationService(context.UserId);

                try
                {
                    //  Start of business logic


                    //  Get order line quantity
                    Trace("Getting salesorderdetail quantity.");
                    var salesorderdetailQuantity = (decimal)salesorderdetail["quantity"];

                    //  Get product from product reference
                    var productRef = (EntityReference)salesorderdetail["productid"];
                    Trace($"{productRef.LogicalName} retrieved from {salesorderdetail.LogicalName}.");

                    Entity product = service.Retrieve("product", productRef.Id, new ColumnSet("name", "quantityonhand"));
                    Trace($"{product.LogicalName} retrieved from {productRef.LogicalName}.");

                    //  Get product quantity
                    Trace("Getting the product quantity.");
                    if (!product.Attributes.ContainsKey("quantityonhand"))
                        throw new InvalidPluginExecutionException("Cannot find quanityonhand for this product.");
                    var productQuantity = (decimal)product["quantityonhand"];

                    //  Check if there is enough inventory to make the order
                    bool enoughQuantity = productQuantity > salesorderdetailQuantity; 
                    Trace($"Enough Quantity: {enoughQuantity}");
                    if (!enoughQuantity)
                    {
                        throw new InvalidPluginExecutionException($"The quantity on this order line " +
                            $"exceeds the product ({product["name"]}) quantity by " +
                            $"{Decimal.Round(salesorderdetailQuantity - productQuantity,2)} units. " +
                            $"Please, lower the quantity of this order line.");
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

        private void Test(Entity entity, string attributeName)
        {
            Trace($"    -   -   -   TEST START  ({entity.LogicalName}['{attributeName}'])   -   -   -   ");
            if (entity.Attributes.ContainsKey(attributeName))
            {
                Trace($"ATTRIBUTE {attributeName} FOUND IN {entity.LogicalName}.");

                var attribute = entity[attributeName];
                Trace($"ATTRIBUTE {attributeName} VALUE({attribute})");
            }
            else
                Trace($"ATTRIBUTE {attributeName} NOT-FOUND IN {entity.LogicalName}.");
            Trace($"    -   -   -   TEST END    ({entity.LogicalName}['{attributeName}'])   -   -   -   ");
        }

        private void Trace(string msg)
        {
            tracingService.Trace($"{GetType().Name} ({DateTime.Now}): {msg}");
        }
    }
}
