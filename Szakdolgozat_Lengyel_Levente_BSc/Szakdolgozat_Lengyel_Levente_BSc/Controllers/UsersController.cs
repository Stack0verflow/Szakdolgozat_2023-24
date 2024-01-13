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
        string id = Request.Query["id"].ToString() == "" ? "0" : Request.Query["id"].ToString(); // this is a string to make injection available
        string password = Request.Query["password"].ToString();
        string db = "sqlserver";
        Console.WriteLine(id + " " + password + " " + db);
        return View(_usersDao.GetUser(id, password, db));
    }
    
    public IActionResult Mysql()
    {
        string id = Request.Query["id"].ToString() == "" ? "0" : Request.Query["id"].ToString(); // this is a string to make injection available
        string password = Request.Query["password"].ToString();
        string db = "mysql";
        Console.WriteLine(id + " " + password + " " + db);
        return View(_usersDao.GetUser(id, password, db));
    }
    
    public IActionResult Sqlite()
    {
        string id = Request.Query["id"].ToString() == "" ? "0" : Request.Query["id"].ToString(); // this is a string to make injection available
        string password = Request.Query["password"].ToString();
        string db = "sqlite";
        Console.WriteLine(id + " " + password + " " + db);
        return View(_usersDao.GetUser(id, password, db));
    }
}