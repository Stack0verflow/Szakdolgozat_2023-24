using System;

namespace Vizsgalo
{
    public class Scanner
    {
        private static string _getDatabase = "";
        private static string _urlSqlServer = "Sqlserver?id=3&password=akivagyok92";
        private static string _urlMysql = "Mysql?id=3&password=akivagyok92";
        
        private static HttpClient _httpClient = new()
        {
            BaseAddress = new Uri("http://localhost:5227/Users/")
        };

        public static async Task Main()
        {
            Console.WriteLine("Examination started");
            
            //sends a GET request to the given url and writes the response to the console
            await GetAsync(_httpClient);
            
            Console.WriteLine("Examination finished");
        }
        
        //sends a GET request to the given url and writes the response to the console
        static async Task GetAsync(HttpClient httpClient)
        {
            //SQL Server - GET
            await SqlServerGet(httpClient);

            //MySQL - GET
            // await MySqlGet(httpClient);
        }

        static async Task SqlServerGet(HttpClient httpClient)
        {
            using HttpResponseMessage response = await httpClient.GetAsync(_urlSqlServer);
            
            Console.WriteLine(response.RequestMessage);
            
            // response.EnsureSuccessStatusCode()
            //     .WriteRequestToConsole();
    
            var jsonResponse = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"{jsonResponse}\n");
        }
        
        
        static async Task MySqlGet(HttpClient httpClient)
        {
            using HttpResponseMessage response = await httpClient.GetAsync(_urlMysql);
            
            Console.WriteLine(response.RequestMessage);
            
            // response.EnsureSuccessStatusCode()
            //     .WriteRequestToConsole();
    
            var jsonResponse = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"{jsonResponse}\n");
        }
    }
}