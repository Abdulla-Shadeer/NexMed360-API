using FinalProjectApi.Dtos.User;

namespace FinalProjectApi.Dtos.Patient;

public class ViewPatientDto
{
    public string Id { get; set; }
    //public ViewUserDto User { get; set; }
    public DateOnly BirthDate { get; set; }
    public string Gender { get; set; }
}
