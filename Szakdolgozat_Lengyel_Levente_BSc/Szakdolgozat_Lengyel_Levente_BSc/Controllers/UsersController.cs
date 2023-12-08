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
        string id = Request.Query["id"].ToString() == "" ? "0" : Request.Query["id"].ToString(); //this is a string to make injection available
        string password = Request.Query["password"].ToString();
        string db = "sqlserver";
        Console.WriteLine(id + " " + password + " " + db);
        return View(_usersDao.GetUser(id, password, db));
    }
    
    public IActionResult Mysql()
    {
        // var users = _usersDao.GetUsers();
        string id = Request.Query["id"].ToString() == "" ? "0" : Request.Query["id"].ToString(); //this is a string to make injection available
        string password = Request.Query["password"].ToString();
        string db = "mysql";
        Console.WriteLine(id + " " + password + " " + db);
        return View(_usersDao.GetUser(id, password, db));
    }
}