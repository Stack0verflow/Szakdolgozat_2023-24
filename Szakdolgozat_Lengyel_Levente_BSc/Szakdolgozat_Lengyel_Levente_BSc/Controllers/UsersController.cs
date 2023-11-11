using Microsoft.AspNetCore.Mvc;
using Szakdolgozat_Lengyel_Levente_BSc.DAO;

namespace Szakdolgozat_Lengyel_Levente_BSc.Controllers;

public class UsersController : Controller
{
    private Users _usersDao;
    
    public UsersController(IConfiguration configuration)
    {
        _usersDao = new Users(configuration);
    }
    
    public IActionResult Sqlserver()
    {
        // var users = _usersDao.GetUsers();
        int id = Convert.ToInt32(Request.Query["id"].ToString() == "" ? "0" : Request.Query["id"].ToString());
        string password = Request.Query["password"].ToString();
        string db = "sqlserver";
        Console.WriteLine(id + " " + password + " " + db);
        return View(_usersDao.GetUser(id, password, db));
    }
}