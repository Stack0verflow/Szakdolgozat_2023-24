using System.Data;
using System.Data.SqlClient;
using MySql.Data.MySqlClient;
using Microsoft.Data.Sqlite;

namespace Szakdolgozat_Lengyel_Levente_BSc.DAO;

public class Users : IUsers
{
    public IConfiguration Configuration { get; }

    private const string QueryStart = "SELECT Id, FirstName, LastName, CurrentAddress FROM Users WHERE Id = ";
    private const string QueryMiddle = " AND Password = '";
    private const string QueryEnd = "';";
    
    public Users(IConfiguration configuration)
    {
        Configuration = configuration;
    }
    
    public IEnumerable<Models.Users> GetUser(string id, string password, string db)
    {
        List<Models.Users> users = new List<Models.Users>();
        string connectionString;
        switch (db)
        { 
            case "sqlserver": 
                try
                {
                    connectionString = Configuration["ConnectionStrings:SqlServer"] ??
                                       throw new InvalidOperationException();
                    SqlConnection connection = new SqlConnection(connectionString);
                    string query = QueryStart + id + QueryMiddle + password + QueryEnd;
                    SqlDataAdapter dataAdapter = new SqlDataAdapter(query, connection);
                    DataTable dataTable = new DataTable();
                    dataAdapter.Fill(dataTable);
                    foreach (DataRow dataRow in dataTable.Rows)
                    {
                        Models.Users user = new Models.Users();
                        user.Id = Convert.ToInt32(dataRow["Id"]);
                        user.FirstName = dataRow["FirstName"].ToString() ?? throw new InvalidOperationException();
                        user.LastName = dataRow["LastName"].ToString() ?? throw new InvalidOperationException();
                        user.CurrentAddress = dataRow["CurrentAddress"].ToString() ??
                                              throw new InvalidOperationException();
                        users.Add(user);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("SQL Server exception happened (" + DateTime.Now + "): " + ex.Message);
                }

                break;
            case "mysql":
                connectionString = Configuration["ConnectionStrings:MySQL"] ?? throw new InvalidOperationException();
                MySqlConnection conn;

                try
                {
                    conn = new MySqlConnection(connectionString);
                    conn.Open();
                    using (MySqlCommand query2 = new MySqlCommand(QueryStart + id + QueryMiddle + password + QueryEnd, conn))
                    {
                        using (MySqlDataReader dr = query2.ExecuteReader())
                        {
                            while (dr.Read())
                            {
                                Models.Users user = new Models.Users();
                                user.Id = Convert.ToInt32(dr["Id"]);
                                user.FirstName = dr["FirstName"].ToString() ?? throw new InvalidOperationException();
                                user.LastName = dr["LastName"].ToString() ?? throw new InvalidOperationException();
                                user.CurrentAddress = dr["CurrentAddress"].ToString() ?? throw new InvalidOperationException();
                                users.Add(user);
                            }
                            dr.Close();
                        }
                    } 
                    conn.Close();
                }
                catch (MySqlException ex)
                {
                    Console.WriteLine("MySQL exception happened (" + DateTime.Now + "): " + ex.Message);
                }
                break;
            case "sqlite":
                try
                {
                    connectionString = Configuration["ConnectionStrings:SQLite"] ??
                                       throw new InvalidOperationException();
                    SqliteConnection sqliteConnection = new SqliteConnection(connectionString);
                    sqliteConnection.Open();

                    var command = sqliteConnection.CreateCommand();
                    command.CommandText = QueryStart + id + QueryMiddle + password + QueryEnd;

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            Models.Users user = new Models.Users();
                            user.Id = reader.GetInt32(0);
                            user.FirstName = reader.GetString(1);
                            user.LastName = reader.GetString(2);
                            user.CurrentAddress = reader.GetString(3);
                            users.Add(user);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("SQLite exception happened (" + DateTime.Now + "): " + ex.Message);
                }
                break;
        }

        return users;
    }
}