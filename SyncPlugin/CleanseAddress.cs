using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.Serialization.Json;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Newtonsoft.Json.Linq;

namespace SyncPlugin
{
    public class CleanseAddress : IPlugin
    {
        private ITracingService tracingService;
        private readonly HttpClient client = new HttpClient();
        private const string USERID = "151CRM468065";

        private const string STREET1 = "address1_line1";
        private const string STREET2 = "address1_line2";
        private const string CITY = "address1_city";
        private const string STATE = "address1_stateorprovince";
        private const string ZIP = "address1_postalcode";

        public void Execute(IServiceProvider serviceProvider)
        {
            tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));

            ////  Check to prevent infinite loops
            //if (context.Depth > 4 || context.ParentContext != null)
            //    return;

            if (context.InputParameters.Contains("Target") && context.InputParameters["Target"] is Entity)
            {
                Entity account = (Entity)context.InputParameters["Target"];

                IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
                IOrganizationService service = serviceFactory.CreateOrganizationService(context.UserId);

                try
                {
                    //  Start of business logic
                    

                    //  Get address info
                    Trace("Getting Address info.");
                    Address address = new Address();

                    address.Street1 = IfGet(account, STREET1);
                    address.Street2 = IfGet(account, STREET2);
                    address.City = IfGet(account, CITY);
                    address.State = IfGet(account, STATE);
                    address.Zip5 = IfGet(account, ZIP);

                    Trace("Assemble URL.");
                    //  convert into url
                    var url = "https://usps-api-jw2021.azurewebsites.net/api/AddressValidation";
                    Trace($"Verify URL: {url}");
                    
                    //  Make api call
                    Trace($"Call API");
                    var result = Call<Address>(url,address).Result;
                    Trace($"API Response: {result}");

                    //  Update
                    var cleanAddress = result;

                    Trace("Updating the account entity.");
                    account[$"{STREET1}"] = cleanAddress.Street1;
                    account[$"{STREET2}"] = cleanAddress.Street2;
                    account[$"{CITY}"] = cleanAddress.City;
                    account[$"{STATE}"] = cleanAddress.State;
                    account[$"{ZIP}"] = $"{cleanAddress.Zip5}{(String.IsNullOrEmpty(cleanAddress.Zip4) ? "" : "-" + cleanAddress.Zip4)}";


                    //  End of business logic
                }
                catch (FaultException<OrganizationServiceFault> ex)
                {
                    throw new InvalidPluginExecutionException("An error occurred:", ex);
                }
                catch (Exception ex)
                {
                    tracingService.Trace($"{ex.ToString()}");
                    throw;
                }
            }
        }

        private string IfGet(Entity entity, string attributeName)
        {
            Trace($"{attributeName}: {entity.Contains($"{attributeName}")}");

            if (entity.Contains($"{attributeName}"))
                return entity[$"{attributeName}"].ToString();
            else
                return "";
        }

        private async Task<T> Call<T>(string url, Address address)
        {
            // Call asynchronous network methods in a try/catch block to handle exceptions.
            try
            {
                //  Serialize
                Trace("Serializing json");
                using (var request = new HttpRequestMessage(HttpMethod.Post, url))
                {
                    string json;
                    using (MemoryStream SerializememoryStream = new MemoryStream())
                    {
                        //write newly created object(address) into memory stream
                        DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(T));
                        serializer.WriteObject(SerializememoryStream, address);
                        SerializememoryStream.Position = 0;

                        //use stream reader to read serialized data from memory stream
                        StreamReader sr = new StreamReader(SerializememoryStream);

                        //get JSON data serialized in string format in string variable 
                        json = sr.ReadToEnd();
                    }

                    request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                    Trace($"Send async Request: {json}");
                    HttpResponseMessage response = await client.SendAsync(request);

                    var status = response.EnsureSuccessStatusCode();
                    Trace($"StatusCode: {status.StatusCode}");

                    //  Extract data
                    var body = response.Content.ReadAsStringAsync().Result;
                    Trace($"body: {body}");

                    T result;
                    using (MemoryStream DeSerializememoryStream = new MemoryStream())
                    {
                        //user stream writer to write JSON string data to memory stream
                        StreamWriter writer = new StreamWriter(DeSerializememoryStream);
                        writer.Write(body);
                        writer.Flush();
                        DeSerializememoryStream.Position = 0;

                        //get the Desrialized data in object of type address
                        DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(T));
                        result = (T)serializer.ReadObject(DeSerializememoryStream);
                    }

                    return result;
                }
            }
            catch (HttpRequestException e)
            {
                Trace("\nException Caught!");
                Trace($"Message :{e.Message}");
                return default(T);
            }
        }

        private async Task<T> Call<T>(string url)
        {
            // Call asynchronous network methods in a try/catch block to handle exceptions.
            try
            {
                client.DefaultRequestHeaders.Accept.Add(
                    new MediaTypeWithQualityHeaderValue("text/xml"));

                Trace("Send Get Request.");
                HttpResponseMessage response = await client.GetAsync(url);
                
                var status = response.EnsureSuccessStatusCode();
                Trace($"StatusCode: {status}");

                //  Extract data
                XmlSerializer serializer = new XmlSerializer(typeof(T));
                Stream stream = await response.Content.ReadAsStreamAsync();
                T result = (T)serializer.Deserialize(stream);

                return result;
            }
            catch (HttpRequestException e)
            {
                Trace("\nException Caught!");
                Trace($"Message :{e.Message}");
                return default(T);
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
