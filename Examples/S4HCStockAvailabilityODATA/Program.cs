using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;


namespace ConsumeS4HCOData
{
    class Program
    {
        static HttpClient client = new HttpClient();
        // API Key provided by SAP from the portal
        static string S4HCAPIKey = "";

        static void Main(string[] args)
        {
            string Material = "TG11";
            string SupplyingPlant1 = "1710";
            string SupplyingPlant2 = "1010";
            string baseURL = "https://sandbox.api.sap.com/s4hanacloud/sap/opu/odata/sap/API_PRODUCT_AVAILY_INFO_BASIC/CalculateAvailabilityTimeseries?";

            string URL1 = baseURL + "ATPCheckingRule='A'&Material='" + Material + "'&SupplyingPlant='" + SupplyingPlant1 + "'";

            Console.WriteLine("The inventory of material " + Material + " in the SupplyingPlant " + SupplyingPlant1 + " is " + GetInventory(URL1));

            string URL2 = baseURL + "ATPCheckingRule='A'&Material='" + Material + "'&SupplyingPlant='" + SupplyingPlant2 + "'";

            Console.WriteLine("The inventory of material " + Material + " in the SupplyingPlant " + SupplyingPlant1 + " is " + GetInventory(URL2));

        }

        static string GetInventory(string URL)
        {
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

    // Just the needed classes and attributes to get the data we need
    public class Results
    {
        public string AvailableQuantityInBaseUnit { get; set; }
    }

    public class D
    {
        public Results[] Results { get; set; }
    }
    public class OData
    {
        public D d { get; set; }
    }
}
