using System;

namespace Vizsgalo
{
    public class Scanner
    {
        // private static string url = "http://http://localhost:5227/Users/Sqlserver";
        private static string _url = "http://localhost:5227/Users/Sqlserver?id=3&password=akivagyok92";

        public static void Main()
        {
            HttpClient httpClient = new HttpClient();
            Task<HttpResponseMessage> httpResponse = httpClient.GetAsync(_url);
            HttpResponseMessage httpResponseMessage = httpResponse.Result;
            Console.WriteLine(httpResponseMessage.Content);
            httpClient.Dispose();
        }
    }
}