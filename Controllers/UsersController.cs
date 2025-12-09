using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TeretnjaciBa.Data;
using TeretnjaciBa.DTOs;
using TeretnjaciBa.Models;
using TeretnjaciBa.Services;

namespace TeretnjaciBa.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Owner")]
public class UsersController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IAuthService _authService;

    public UsersController(ApplicationDbContext context, IAuthService authService)
    {
        _context = context;
        _authService = authService;
    }

    // GET: api/users
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<UserDto>>>> GetUsers()
    {
        var users = await _context.Users
            .Select(u => new UserDto
            {
                Id = u.Id,
                Username = u.Username,
                FullName = u.FullName,
                Email = u.Email,
                Role = u.Role,
                IsActive = u.IsActive
            })
            .ToListAsync();

        return Ok(new ApiResponse<List<UserDto>>
        {
            Success = true,
            Data = users
        });
    }

    // POST: api/users
    [HttpPost]
    public async Task<ActionResult<ApiResponse<UserDto>>> CreateAdmin([FromBody] CreateAdminRequest request)
    {
        // Check if username already exists
        if (await _context.Users.AnyAsync(u => u.Username == request.Username))
        {
            return BadRequest(new ApiResponse<UserDto>
            {
                Success = false,
                Message = "Korisničko ime već postoji"
            });
        }

        // Check if email already exists
        if (await _context.Users.AnyAsync(u => u.Email == request.Email))
        {
            return BadRequest(new ApiResponse<UserDto>
            {
                Success = false,
                Message = "Email već postoji"
            });
        }

        var user = new User
        {
            Username = request.Username,
            PasswordHash = _authService.HashPassword(request.Password),
            FullName = request.FullName,
            Email = request.Email,
            Role = UserRoles.Admin,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return Ok(new ApiResponse<UserDto>
        {
            Success = true,
            Message = "Admin je uspješno kreiran",
            Data = new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                FullName = user.FullName,
                Email = user.Email,
                Role = user.Role,
                IsActive = user.IsActive
            }
        });
    }

    // PUT: api/users/{id}
    [HttpPut("{id}")]
    public async Task<ActionResult<ApiResponse<UserDto>>> UpdateAdmin(int id, [FromBody] UpdateAdminRequest request)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null)
        {
            return NotFound(new ApiResponse<UserDto>
            {
                Success = false,
                Message = "Korisnik nije pronađen"
            });
        }

        // Prevent updating owner
        if (user.Role == UserRoles.Owner)
        {
            return BadRequest(new ApiResponse<UserDto>
            {
                Success = false,
                Message = "Vlasnik ne može biti ažuriran"
            });
        }

        user.FullName = request.FullName;
        user.Email = request.Email;
        user.IsActive = request.IsActive;
        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(new ApiResponse<UserDto>
        {
            Success = true,
            Message = "Admin je uspješno ažuriran",
            Data = new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                FullName = user.FullName,
                Email = user.Email,
                Role = user.Role,
                IsActive = user.IsActive
            }
        });
    }

    // DELETE: api/users/{id}
    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteAdmin(int id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null)
        {
            return NotFound(new ApiResponse<object>
            {
                Success = false,
                Message = "Korisnik nije pronađen"
            });
        }

        // Prevent deleting owner
        if (user.Role == UserRoles.Owner)
        {
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = "Vlasnik ne može biti obrisan"
            });
        }

        _context.Users.Remove(user);
        await _context.SaveChangesAsync();

        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = "Admin je uspješno obrisan"
        });
    }
}
