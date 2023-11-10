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
        var users = _usersDao.GetUsers();
        return View(users);
    }
}