using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;

namespace InvToMarketDurable
{
    public static class Function1
    {
        static HttpClient client = new HttpClient();
        // API Key provided by SAP from the portal
        static string S4HCAPIKey = "";
        const decimal MELIStockLimit = 99999;

        [FunctionName("Function1")]
        public static async Task<bool> RunOrchestrator(
            [OrchestrationTrigger] DurableOrchestrationContext context)
        {
            string material = "TG11";
            string supplyingPlant1710 = "1710";
            string item1710MELI = "MLA723041885";
            string item1710WISH = "TG11";

            decimal inv1710 = decimal.Parse(await context.CallActivityAsync<string>("GetInventory", (material, supplyingPlant1710)));

            var parallelTasks = new List<Task<bool>>();

            parallelTasks.Add(context.CallActivityAsync<bool>("UpdateMELI", (item1710MELI, inv1710)));
            parallelTasks.Add(context.CallActivityAsync<bool>("UpdateWISH", (item1710WISH, inv1710)));

            await Task.WhenAll(parallelTasks);

            bool allUpdated = parallelTasks.TrueForAll(t => t.Result);

            return allUpdated;
        }

        [FunctionName("GetInventory")]
        public static string GetInventory([ActivityTrigger] DurableActivityContext inputs, TraceWriter log)
        {
            (string material, string supplyingPlant) materialInfo = inputs.GetInput<(string, string)>();

            string baseURL = "https://sandbox.api.sap.com/s4hanacloud/sap/opu/odata/sap/API_PRODUCT_AVAILY_INFO_BASIC/CalculateAvailabilityTimeseries?";
            string URL = baseURL + "ATPCheckingRule='A'&Material='" + materialInfo.material + "'&SupplyingPlant='" + materialInfo.supplyingPlant + "'";

            var request = new HttpRequestMessage()
            {
                RequestUri = new Uri(URL),
                Method = HttpMethod.Get
            };
            request.Headers.TryAddWithoutValidation("APIKey", S4HCAPIKey);
            request.Headers.TryAddWithoutValidation("Accept", "application/json");

            var result = client.SendAsync(request).Result;

            string jsonContent = result.Content.ReadAsStringAsync().Result;
            var odata = JsonConvert.DeserializeObject<OData>(jsonContent);

            log.Info($"Inventory level for {materialInfo.material} in {materialInfo.supplyingPlant} is {odata.d.Results[0].AvailableQuantityInBaseUnit}.");

            return odata.d.Results[0].AvailableQuantityInBaseUnit;
        }

        [FunctionName("UpdateMELI")]
        public static bool MELIUpdateItemStock([ActivityTrigger] DurableActivityContext inputs, TraceWriter log)
        {
            string refreshToken = "";
            string clientId = "";
            string clientSecret = "";
            string access_token = Helpers.GetTokenMELI(clientId, clientSecret, refreshToken);

            (string item, decimal inventory) materialInfo = inputs.GetInput<(string, decimal)>();
            materialInfo.inventory = materialInfo.inventory > MELIStockLimit ? MELIStockLimit : materialInfo.inventory;

            string URL = "https://api.mercadolibre.com/items/" + materialInfo.item + "?access_token=" + access_token;
            string body = "{\"available_quantity\":" + materialInfo.inventory.ToString() + "}";

            var content = Helpers.callURL(URL, HttpMethod.Put, new StringContent(body, Encoding.UTF8, "application/json"));

            dynamic stuff = JsonConvert.DeserializeObject(content);

            return stuff.available_quantity == materialInfo.inventory.ToString();
        }

        [FunctionName("UpdateWISH")]
        public static bool WISHUpdateItemStock([ActivityTrigger] DurableActivityContext inputs, TraceWriter log)
        {
            string accessToken = "";

            string baseURL = "https://sandbox.merchant.wish.com/api/v2";
            string URL = baseURL + "/variant/update-inventory";

            (string sku, decimal inventory) materialInfo = inputs.GetInput<(string, decimal)>();

            var dict = new Dictionary<string, string>();
            dict.Add("sku", materialInfo.sku);
            dict.Add("inventory", materialInfo.inventory.ToString());
            dict.Add("access_token", accessToken);

            var content = Helpers.callURL(URL, HttpMethod.Post, new FormUrlEncodedContent(dict));

            dynamic stuff = JsonConvert.DeserializeObject(content);

            return stuff.code == "0";
        }

        [FunctionName("Function1_HttpStart")]
        public static async Task<HttpResponseMessage> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")]HttpRequestMessage req,
            [OrchestrationClient]DurableOrchestrationClient starter,
            TraceWriter log)
        {
            // Function input comes from the request content.
            string instanceId = await starter.StartNewAsync("Function1", null);

            log.Info($"Started orchestration with ID = '{instanceId}'.");

            return starter.CreateCheckStatusResponse(req, instanceId);
        }
    }
    // Classes to parse S4HC ODATA answer
    class Results { public string AvailableQuantityInBaseUnit { get; set; } }
    class D { public Results[] Results { get; set; } }
    class OData { public D d { get; set; } }

    class Helpers
    {
        static HttpClient client = new HttpClient();


        public static string callURL(string URL) { return callURL(URL, HttpMethod.Get, null); }
        public static string callURL(string URL, HttpMethod method) { return callURL(URL, method, null); }
        public static string callURL(string URL, HttpMethod method, HttpContent content)
        {
            var request = new HttpRequestMessage()
            {
                RequestUri = new Uri(URL),
                Method = method
            };

            if (content != null)
                request.Content = content;

            var result = client.SendAsync(request).Result;

            string jsonContent = result.Content.ReadAsStringAsync().Result;

            return jsonContent;
        }

        public static string GetTokenMELI(string MELIclientId, string MELIclientSecret, string MELIrefreshToken)
        {
            string URL = "https://api.mercadolibre.com/oauth/token?grant_type=refresh_token&client_id="
                + MELIclientId + "&client_secret=" + MELIclientSecret + "&refresh_token=" + MELIrefreshToken;

            var content = callURL(URL, HttpMethod.Post);

            dynamic stuff = JsonConvert.DeserializeObject(content);

            return stuff.access_token;
        }
    }
}