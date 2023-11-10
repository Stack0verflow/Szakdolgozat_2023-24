namespace Szakdolgozat_Lengyel_Levente_BSc.DAO;

public interface IUsers
{
    public IEnumerable<Models.Users> GetUsers();
    public void AddUser(Models.Users user);
    public Users GetUser(int id);
    public void DeleteUser(int id, Models.Users user);
    public void EditUser(int id, Models.Users user);
}