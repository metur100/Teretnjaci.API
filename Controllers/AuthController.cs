using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TeretnjaciBa.Data;
using TeretnjaciBa.DTOs;
using TeretnjaciBa.Services;

namespace TeretnjaciBa.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IAuthService _authService;

    public AuthController(ApplicationDbContext context, IAuthService authService)
    {
        _context = context;
        _authService = authService;
    }

    [HttpPost("login")]
    public async Task<ActionResult<ApiResponse<LoginResponse>>> Login([FromBody] LoginRequest request)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Username == request.Username && u.IsActive);

        if (user == null || !_authService.VerifyPassword(request.Password, user.PasswordHash))
        {
            return BadRequest(new ApiResponse<LoginResponse>
            {
                Success = false,
                Message = "Pogrešno korisničko ime ili lozinka"
            });
        }

        var token = _authService.GenerateJwtToken(user);

        return Ok(new ApiResponse<LoginResponse>
        {
            Success = true,
            Message = "Uspješna prijava",
            Data = new LoginResponse
            {
                Token = token,
                User = new UserDto
                {
                    Id = user.Id,
                    Username = user.Username,
                    FullName = user.FullName,
                    Email = user.Email,
                    Role = user.Role,
                    IsActive = user.IsActive
                }
            }
        });
    }

    [HttpGet("generate-hash")]
    public IActionResult GenerateHash(string password)
    {
        var hash = BCrypt.Net.BCrypt.HashPassword(password);
        return Ok(new { password, hash });
    }

}
