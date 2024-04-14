namespace Szakdolgozat_Lengyel_Levente_BSc.DAO;

public interface IUsers
{
    public IEnumerable<Models.Users> GetUser(string id, string password, string db);
}