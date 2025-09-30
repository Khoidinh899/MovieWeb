// Models/DTOs/AuthResult.cs
using MovieWeb.Models.Entities;

namespace MovieWeb.Models.DTOs
{
    public class AuthResult
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<string> Errors { get; set; } = new List<string>();
        public string? Token { get; set; }
        public User? User { get; set; }

        public static AuthResult Success(string message, string? token = null, User? user = null)
        {
            return new AuthResult
            {
                IsSuccess = true,
                Message = message,
                Token = token,
                User = user
            };
        }

        public static AuthResult Failed(string error)
        {
            return new AuthResult
            {
                IsSuccess = false,
                Message = error,
                Errors = new List<string> { error }
            };
        }

        public static AuthResult Failed(List<string> errors)
        {
            return new AuthResult
            {
                IsSuccess = false,
                Message = string.Join(", ", errors),
                Errors = errors
            };
        }
    }
}