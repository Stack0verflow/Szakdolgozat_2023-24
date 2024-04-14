using System.ComponentModel.DataAnnotations;

namespace Szakdolgozat_Lengyel_Levente_BSc.Models;

public class Users
{
    [Key]
    public int Id { get; set; }
    [Required]
    public string FirstName { get; set; }
    [Required]
    public string LastName { get; set; }
    public string CurrentAddress { get; set; }

    public override string ToString()
    {
        return $"Id: {Id}, FirstName: {FirstName}, LastName: {LastName}, CurrentAddress: {CurrentAddress}";
    }
}