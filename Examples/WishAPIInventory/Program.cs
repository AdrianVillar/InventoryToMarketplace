using System;
using System.Collections.Generic;
using System.Net.Http;
using Newtonsoft.Json;

namespace WishAPIInventory
{
    class Program
    {
        static void Main(string[] args)
        {
            string sku = "";
            string productId = "";

            int currentInventory = WishAPICalls.GetItemStock(productId);
            Console.WriteLine("Current Inventory Product " + productId + " SKU " + sku + ": " + currentInventory.ToString());
            bool updateOk = WishAPICalls.UpdateItemStock(sku, currentInventory + 10);
            if (updateOk)
                Console.WriteLine("SKU " + sku + " inventory updated");
        }
    }

    class WishAPICalls
    {
        static string accessToken = "";

        const string baseURL = "https://sandbox.merchant.wish.com/api/v2";

        static HttpClient client = new HttpClient();

        public static int GetItemStock(string product)
        {
            string URL = baseURL + "/product?id=" + product + "&access_token=" + accessToken;

            var content = callURL(URL);

            dynamic stuff = JsonConvert.DeserializeObject(content);

            return stuff.data.Product.variants[0].Variant.inventory;
        }

        public static bool UpdateItemStock(string sku, int inventory)
        {
            string URL = baseURL + "/variant/update-inventory";

            var dict = new Dictionary<string, string>();
            dict.Add("sku", sku);
            dict.Add("inventory", inventory.ToString());
            dict.Add("access_token", accessToken);

            var content = callURL(URL, HttpMethod.Post, new FormUrlEncodedContent(dict));

            dynamic stuff = JsonConvert.DeserializeObject(content);

            return stuff.code == "0" ;
        }

        static string callURL(string URL)
        {
            return callURL(URL, HttpMethod.Get, null);

        }

        static string callURL(string URL, HttpMethod method, HttpContent content)
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
    }

}
