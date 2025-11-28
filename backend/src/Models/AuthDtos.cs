// Models/AuthDtos.cs
// contains both inputs and outsputs
// RegisterRequest & LoginRequest: input binding
// LoginResponse: output binding
namespace SurveyApi.Models;

// input
public class RegisterRequest
{
    public string Email { get; set; } = default!;
    public string Password { get; set; } = default!;
}

// input
public class LoginRequest
{
    public string Email { get; set; } = default!;
    public string Password { get; set; } = default!;
}

// output
public class LoginResponse
{
    public string Token { get; set; } = default!;
}
