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
    public class StateLimitation : IPlugin
    {
        private ITracingService tracingService;

        public void Execute(IServiceProvider serviceProvider)
        {
            tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));

            if (context.InputParameters.Contains("Target") && context.InputParameters["Target"] is Entity)
            {
                Entity entity = (Entity)context.InputParameters["Target"];

                IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
                IOrganizationService service = serviceFactory.CreateOrganizationService(context.UserId);

                try
                {
                    //  Start of business logic

                    //  Get user
                    Entity user = service.Retrieve("systemuser", context.InitiatingUserId, new ColumnSet("fullname"));
                    var username = user["fullname"];

                    //  Trace checking for state
                    Trace("Checking for state data");
                    string state;
                    if (!entity.TryGetAttributeValue("address1_stateorprovince", out state))
                    {
                        Trace($"{username} has attempted to create an account without a state value.");
                        throw new InvalidPluginExecutionException("Fool! You must provide a state first.");
                    }

                    //  Now validate the state
                    Trace("Validating state");
                    var st = state.ToLower();
                    if (st != "tx" & st != "texas")
                    {
                        Trace($"{username} has attempted to create an account in the state of {state}.");
                        throw new InvalidPluginExecutionException("You fool... I have many eyes to see and hands to catch little insects who try to STING ME!!!");
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
