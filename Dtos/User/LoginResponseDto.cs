namespace NexMed360Api.Dtos.User;

public class LoginResponseDto
{
    public ViewUserDto User { get; set; }
    public string AccessToken { get; set; }
}
