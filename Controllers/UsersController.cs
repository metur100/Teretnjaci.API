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
    public async Task<ActionResult<ApiResponse<UserDto>>> CreateUser([FromBody] CreateUserRequest request)
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

        // Validate role
        if (request.Role != UserRoles.Admin && request.Role != UserRoles.Owner)
        {
            return BadRequest(new ApiResponse<UserDto>
            {
                Success = false,
                Message = "Nevažeća uloga. Dozvoljene uloge: Admin, Owner"
            });
        }

        var user = new User
        {
            Username = request.Username,
            PasswordHash = _authService.HashPassword(request.Password),
            FullName = request.FullName,
            Email = request.Email,
            Role = request.Role,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return Ok(new ApiResponse<UserDto>
        {
            Success = true,
            Message = $"Korisnik ({request.Role}) je uspješno kreiran",
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

    [HttpPut("{id}")]
    public async Task<ActionResult<ApiResponse<UserDto>>> UpdateUser(int id, [FromBody] UpdateUserRequest request)
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

        var currentUserRole = User.Claims.FirstOrDefault(c => c.Type == "role")?.Value;
        var currentUserId = int.Parse(User.Claims.FirstOrDefault(c => c.Type == "userId")?.Value ?? "0");

        // Update basic fields
        if (!string.IsNullOrEmpty(request.Username) && request.Username != user.Username)
        {
            if (await _context.Users.AnyAsync(u => u.Username == request.Username && u.Id != id))
            {
                return BadRequest(new ApiResponse<UserDto>
                {
                    Success = false,
                    Message = "Korisničko ime već postoji"
                });
            }
            user.Username = request.Username;
        }

        if (request.Email != user.Email)
        {
            if (await _context.Users.AnyAsync(u => u.Email == request.Email && u.Id != id))
            {
                return BadRequest(new ApiResponse<UserDto>
                {
                    Success = false,
                    Message = "Email već postoji"
                });
            }
            user.Email = request.Email;
        }

        user.FullName = request.FullName;
        user.IsActive = request.IsActive;
        user.UpdatedAt = DateTime.UtcNow;

        // Handle role changes
        if (!string.IsNullOrEmpty(request.Role) && request.Role != user.Role)
        {
            // Validate role
            if (request.Role != UserRoles.Admin && request.Role != UserRoles.Owner)
            {
                return BadRequest(new ApiResponse<UserDto>
                {
                    Success = false,
                    Message = "Nevažeća uloga"
                });
            }

            user.Role = request.Role;
        }

        await _context.SaveChangesAsync();

        return Ok(new ApiResponse<UserDto>
        {
            Success = true,
            Message = "Korisnik je uspješno ažuriran",
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
    public async Task<ActionResult<ApiResponse<object>>> DeleteUser(int id)
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

        // Prevent deleting yourself
        var currentUserId = int.Parse(User.Claims.FirstOrDefault(c => c.Type == "userId")?.Value ?? "0");
        if (user.Id == currentUserId)
        {
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = "Ne možete obrisati svoj nalog"
            });
        }

        _context.Users.Remove(user);
        await _context.SaveChangesAsync();

        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = "Korisnik je uspješno obrisan"
        });
    }
}