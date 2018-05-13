using System;
using System.Net.Http;
using System.Text;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;

namespace InvToMarketBatch
{
    public static class Function1
    {
        static HttpClient client = new HttpClient();
        // API Key provided by SAP from the portal
        static string S4HCAPIKey = "";
        const decimal MELIStockLimit = 99999;

        [FunctionName("BatchModel")]
        public static void Run([TimerTrigger("0 */1 * * * *")]TimerInfo myTimer, TraceWriter log)
        {
            string material = "TG11";
            string supplyingPlant1710 = "1710";
            string supplyingPlant1010 = "1010";
            string item1710 = "MLA723041885";
            string item1010 = "MLA723996990";

            log.Info($"C# Timer trigger function executed at: {DateTime.Now}");
            // Get Inventory from S4HC
            decimal inv1710 = decimal.Parse((GetInventory(material, supplyingPlant1710)));
            log.Info("The inventory of material " + material + " in the SupplyingPlant " + supplyingPlant1710 + " is " + inv1710.ToString());
            // Validate if the S4HC is larger than the MELI max accepted inventory
            inv1710 = inv1710 > MELIStockLimit ? MELIStockLimit : inv1710;

            decimal inv1010 = decimal.Parse((GetInventory(material, supplyingPlant1010)));
            log.Info("The inventory of material " + material + " in the SupplyingPlant " + supplyingPlant1010 + " is " + inv1010.ToString());
            // Validate if the S4HC is larger than the MELI max accepted inventory
            inv1010 = inv1010 > MELIStockLimit ? MELIStockLimit : inv1010;

            // Get the access token
            MELIWebCalls.GetToken();
            //Upldate product 1
            if (MELIWebCalls.UpdateItemStock(item1710, Decimal.ToInt32(inv1710)))
                log.Info("Item " + item1710 + " inventory updated");

            // Update product 2
            if (MELIWebCalls.UpdateItemStock(item1010, Decimal.ToInt32(inv1010)))
                log.Info("Item " + item1010 + " inventory updated");
        }

        static string GetInventory(string material, string supplyingPlant)
        {
            string baseURL = "https://sandbox.api.sap.com/s4hanacloud/sap/opu/odata/sap/API_PRODUCT_AVAILY_INFO_BASIC/CalculateAvailabilityTimeseries?";
            string URL = baseURL + "ATPCheckingRule='A'&Material='" + material + "'&SupplyingPlant='" + supplyingPlant + "'";

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

            return odata.d.Results[0].AvailableQuantityInBaseUnit;
        }
    }

    public class Results { public string AvailableQuantityInBaseUnit { get; set; } }
    public class D { public Results[] Results { get; set; } }
    public class OData { public D d { get; set; } }

    class MELIWebCalls
    {
        static string refreshToken = "";
        static string clientId = "";
        static string clientSecret = "";
        static string access_token;

        private static HttpClient client = new HttpClient();

        public static string GetToken()
        {
            string URL = "https://api.mercadolibre.com/oauth/token?grant_type=refresh_token&client_id="
                + clientId + "&client_secret=" + clientSecret + "&refresh_token=" + refreshToken;

            var content = callURL(URL, HttpMethod.Post);

            dynamic stuff = JsonConvert.DeserializeObject(content);

            access_token = stuff.access_token;

            return stuff.access_token;
        }

        public static int GetItemStock(string item)
        {
            string URL = "https://api.mercadolibre.com/items/" + item + "?access_token=" + access_token;

            var content = callURL(URL);

            dynamic stuff = JsonConvert.DeserializeObject(content);

            return stuff.available_quantity;

        }

        public static bool UpdateItemStock(string item, int inventory)
        {
            string URL = "https://api.mercadolibre.com/items/" + item + "?access_token=" + access_token;
            string body = "{\"available_quantity\":" + inventory.ToString() + "}";

            var content = callURL(URL, HttpMethod.Put, body);

            dynamic stuff = JsonConvert.DeserializeObject(content);

            return stuff.available_quantity == inventory.ToString();
        }

        static string callURL(string URL)
        {
            return callURL(URL, HttpMethod.Get, string.Empty);
        }

        static string callURL(string URL, HttpMethod method)
        {
            return callURL(URL, method, string.Empty);
        }

        static string callURL(string URL, HttpMethod method, string body)
        {
            var request = new HttpRequestMessage()
            {
                RequestUri = new Uri(URL),
                Method = method
            };
            request.Headers.TryAddWithoutValidation("Accept", "application/json");

            if (body != string.Empty)
                request.Content = new StringContent(body, Encoding.UTF8, "application/json");

            var result = client.SendAsync(request).Result;

            string jsonContent = result.Content.ReadAsStringAsync().Result;

            return jsonContent;

        }
    }

}
