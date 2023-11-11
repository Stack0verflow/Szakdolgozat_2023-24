using System.Data;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;

namespace Szakdolgozat_Lengyel_Levente_BSc.DAO;

public class Users : IUsers
{
    public IConfiguration Configuration { get; }
    public Users(IConfiguration configuration)
    {
        Configuration = configuration;
    }
    
    public IEnumerable<Models.Users> GetUsers()
    {
        string connectionString = Configuration["ConnectionStrings:SqlServer"] ?? throw new InvalidOperationException();
        List<Models.Users> users = new List<Models.Users>();

        SqlConnection connection = new SqlConnection(connectionString);
        string query = "SELECT * FROM Users;";
        SqlDataAdapter dataAdapter = new SqlDataAdapter(query, connection);
        DataTable dataTable = new DataTable();
        dataAdapter.Fill(dataTable);
        
        foreach (DataRow dataRow in dataTable.Rows)
        {
            Models.Users user = new Models.Users();
            user.Id = Convert.ToInt32(dataRow["Id"]);
            user.FirstName = dataRow["FirstName"].ToString() ?? throw new InvalidOperationException();
            user.LastName = dataRow["LastName"].ToString() ?? throw new InvalidOperationException();
            user.Password = dataRow["Password"].ToString() ?? throw new InvalidOperationException();
            user.BirthDate = Convert.ToDateTime(dataRow["BirthDate"]);
            user.HealthCareNumber = Convert.ToInt32(dataRow["HealthCareNumber"]);
            user.CurrentAddress = dataRow["CurrentAddress"].ToString() ?? throw new InvalidOperationException();
            users.Add(user);
        }
        
        return users;
    }

    public void AddUser(Models.Users user)
    {
        throw new NotImplementedException();
    }

    public IEnumerable<Models.Users> GetUser(int id, string password, string db)
    {
        List<Models.Users> users = new List<Models.Users>();
        string connectionString = "";
        switch (db)
        {
            case "sqlserver":
                connectionString = Configuration["ConnectionStrings:SqlServer"] ?? throw new InvalidOperationException();
                break;
            case "mysql":
                connectionString = "mysqlconn";
                break;
            case "sqlite":
                connectionString = "sqliteconn";
                break;
        }
        SqlConnection connection = new SqlConnection(connectionString);
        string query = "SELECT * FROM Users WHERE Id = " + id + " AND Password = '" + password + "';";
        SqlDataAdapter dataAdapter = new SqlDataAdapter(query, connection);
        DataTable dataTable = new DataTable();
        dataAdapter.Fill(dataTable);
        foreach (DataRow dataRow in dataTable.Rows)
        {
            Models.Users user = new Models.Users();
            user.Id = Convert.ToInt32(dataRow["Id"]);
            user.FirstName = dataRow["FirstName"].ToString() ?? throw new InvalidOperationException();
            user.LastName = dataRow["LastName"].ToString() ?? throw new InvalidOperationException();
            user.Password = dataRow["Password"].ToString() ?? throw new InvalidOperationException();
            user.BirthDate = Convert.ToDateTime(dataRow["BirthDate"]);
            user.HealthCareNumber = Convert.ToInt32(dataRow["HealthCareNumber"]);
            user.CurrentAddress = dataRow["CurrentAddress"].ToString() ?? throw new InvalidOperationException();
            users.Add(user);
        }

        return users;
    }

    public void DeleteUser(int id, Models.Users user)
    {
        throw new NotImplementedException();
    }

    public void EditUser(int id, Models.Users user)
    {
        throw new NotImplementedException();
    }
}