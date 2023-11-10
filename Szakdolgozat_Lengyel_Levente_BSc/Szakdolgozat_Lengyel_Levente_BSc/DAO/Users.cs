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
        
        Console.WriteLine(users);
        return users;
    }

    public void AddUser(Models.Users user)
    {
        throw new NotImplementedException();
    }

    public Users GetUser(int id)
    {
        throw new NotImplementedException();
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