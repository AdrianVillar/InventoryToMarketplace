using System;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json;

namespace MELIInventory
{
    class Program
    {
        static void Main(string[] args)
        {
            string item = "MLA723041885";

            var access_token = MELIWebCalls.GetToken();
            Console.WriteLine("Access Token: " + access_token);

            int currentInventory = MELIWebCalls.GetItemStock(item);
            Console.WriteLine("Current Inventory item " + item + ": " + currentInventory.ToString());
            bool updateOk = MELIWebCalls.UpdateItemStock(item, currentInventory+1);
            if (updateOk)
                Console.WriteLine("Item " + item + " inventory updated");
        }
    }

    class MELIWebCalls
    {
        static string refreshToken = "";
        static string clientId = "";
        static string clientSecret = "";
        static string access_token;

        private static HttpClient client = new HttpClient();

        public static string GetToken ()
        {
            string URL = "https://api.mercadolibre.com/oauth/token?grant_type=refresh_token&client_id="
                + clientId + "&client_secret=" + clientSecret + "&refresh_token=" + refreshToken;

            var content = callURL(URL, HttpMethod.Post);

            dynamic stuff = JsonConvert.DeserializeObject(content);

            access_token = stuff.access_token;

            return stuff.access_token;
        }

        public static int  GetItemStock (string item)
        {
            string URL = "https://api.mercadolibre.com/items/" + item + "?access_token=" + access_token;

            var content = callURL(URL);

            dynamic stuff = JsonConvert.DeserializeObject(content);

            return stuff.available_quantity;

        }
        
        public static bool UpdateItemStock (string item, int inventory)
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
