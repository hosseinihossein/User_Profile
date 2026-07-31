using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Security.Principal;
using AspApp.Filters;
using AspApp.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using OpenIddict.Validation.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace AspApp.Controllers;

[ApiController]
[Route("[controller]/Api/[action]")]
public class UserProfileController : ControllerBase
{
    private readonly UserManager<Identity_UserDbModel> _userManager;
    private readonly string _storage_Users = Path.Combine("Storage", "Identity", "Users");

    public UserProfileController(UserManager<Identity_UserDbModel> userManager)
    {
        _userManager = userManager;
    }



    [HttpGet]
    [GenerateAntiforgeryTokenCookie]
    [Authorize]
    public IActionResult GetCsrf()
    {
        return Ok();
    }



    [HttpGet]
    public IActionResult EnableTurnstile([FromServices] IConfiguration configuration)
    {
        return Ok(new { enableTurnstile = configuration.GetValue<bool>("TurnsTileEnable", false) });
    }



    [HttpGet]
    [Authorize]
    public async Task<IActionResult> ProfileModel()
    {
        var user = await _userManager.FindByIdAsync(User.GetClaim(Claims.Subject)!);
        if (user is null)
        {
            return Forbid();
        }

        User_Profile_Model profileModel = new()
        {
            CreatedAt = user.CreatedAt,
            Description = user.Description,
            DisplayName = user.DisplayName,
            Email = user.Email!,
            EmailConfirmed = user.EmailConfirmed,
            HasImage = user.HasImage,
            Id = user.Id,
            ImageVersion = user.ImageVersion,
            PublicEmail = user.PublicEmail,
            RemoteImageUrl = user.RemoteImageUrl,
            Username = user.UserName!,
            Roles = [.. await _userManager.GetRolesAsync(user)],
        };

        return Ok(profileModel);
    }



    [HttpGet]
    public async Task<IActionResult> ProfileImage([FromQuery] Guid id,
    [FromServices] IWebHostEnvironment _env)
    {
        string userImagePath =
        Path.Combine(_env.ContentRootPath, _storage_Users, id.ToString("N"), "image");

        if (System.IO.File.Exists(userImagePath))
        {
            return PhysicalFile(userImagePath, "application/octet-stream", "userImage", true);
        }

        return NotFound("User Image Not Found!");
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(128 * 1024)]//128 KB
    public async Task<IActionResult> SubmitUserImage([FromForm] IFormFile UserImage,
    [FromServices] IWebHostEnvironment _env)
    {
        var user = await _userManager.FindByIdAsync(User.GetClaim(Claims.Subject)!);
        if (user is null)
        {
            return Forbid();
        }

        DirectoryInfo userDirectoryInfo = Directory.CreateDirectory(
            Path.Combine(_env.ContentRootPath, _storage_Users, user.Id.ToString("N"))
        );
        string userImagePath = Path.Combine(userDirectoryInfo.FullName, "image");
        using (FileStream fs = System.IO.File.Create(userImagePath))
        {
            await UserImage.CopyToAsync(fs);
        }

        user.HasImage = true;
        user.ImageVersion++;

        //save
        await _userManager.UpdateAsync(user);

        return Ok(new { success = true, user.HasImage, user.ImageVersion });
    }

    [HttpDelete]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUserImage([FromServices] IWebHostEnvironment _env)
    {
        var user = await _userManager.FindByIdAsync(User.GetClaim(Claims.Subject)!);
        if (user is null)
        {
            return Forbid();
        }

        string userImagePath =
        Path.Combine(_env.ContentRootPath, _storage_Users, user.Id.ToString("N"), "image");

        if (System.IO.File.Exists(userImagePath))
        {
            System.IO.File.Delete(userImagePath);

            //edit user
            user.HasImage = false;
            user.ImageVersion = 0;
            user.RemoteImageUrl = null;

            await _userManager.UpdateAsync(user);
        }

        return Ok(new { success = true });
    }



    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditUsername([FromForm][StringLength(64)] string Username)
    {
        var user = await _userManager.FindByIdAsync(User.GetClaim(Claims.Subject)!);
        if (user is null)
        {
            return Forbid();
        }

        IdentityResult result = await _userManager.SetUserNameAsync(user, Username);
        if (result.Succeeded)
        {
            return Ok();
        }
        else
        {
            foreach (IdentityError error in result.Errors)
            {
                ModelState.AddModelError(nameof(Username), error.Description);
            }
            return BadRequest(ModelState);
        }
    }



    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditDisplayName([FromForm][StringLength(64)] string DisplayName)
    {
        var user = await _userManager.FindByIdAsync(User.GetClaim(Claims.Subject)!);
        if (user is null)
        {
            return Forbid();
        }

        user.DisplayName = DisplayName;
        IdentityResult result = await _userManager.UpdateAsync(user);
        if (result.Succeeded)
        {
            return Ok();
        }
        else
        {
            foreach (IdentityError error in result.Errors)
            {
                ModelState.AddModelError(nameof(DisplayName), error.Description);
            }
            return BadRequest(ModelState);
        }
    }



    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeEmail([FromForm][StringLength(128)] string NewEmail)
    {
        var user = await _userManager.FindByIdAsync(User.GetClaim(Claims.Subject)!);
        if (user is null)
        {
            return Forbid();
        }

        string token = await _userManager.GenerateChangeEmailTokenAsync(user, NewEmail);
        string encodedToken = WebUtility.UrlEncode(token);

        //create an email message with the confirm-new-email link and send it to the new email address

        return Ok();
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> ConfirmNewEmail([FromQuery][StringLength(128)] string newEmail,
    [FromQuery][StringLength(256)] string token)
    {
        var user = await _userManager.FindByIdAsync(User.GetClaim(Claims.Subject)!);
        if (user is null)
        {
            return Forbid();
        }

        string decodedToken = WebUtility.UrlDecode(token);
        IdentityResult result = await _userManager.ChangeEmailAsync(user, newEmail, decodedToken);
        if (result.Succeeded)
        {
            return Redirect("/");
        }
        else
        {
            return Redirect($"/Error?{result.Errors.SelectMany(e => e.Description + ",,")}");
        }
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendEmailValidationCode()
    {
        var user = await _userManager.FindByIdAsync(User.GetClaim(Claims.Subject)!);
        if (user is null)
        {
            return Forbid();
        }

        string token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        string encodedToken = WebUtility.UrlEncode(token);

        //create an email message with the confirm-email link and send it to the user's email address

        return Ok();
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> ConfirmEmail([FromQuery][StringLength(256)] string token)
    {
        var user = await _userManager.FindByIdAsync(User.GetClaim(Claims.Subject)!);
        if (user is null)
        {
            return Forbid();
        }

        string decodedToken = WebUtility.UrlDecode(token);
        IdentityResult result = await _userManager.ConfirmEmailAsync(user, decodedToken);
        if (result.Succeeded)
        {
            return Redirect("/");
        }
        else
        {
            return Redirect($"/Error?{result.Errors.SelectMany(e => e.Description + ",,")}");
        }
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PublicEmail([FromForm] bool PublicEmail)
    {
        var user = await _userManager.FindByIdAsync(User.GetClaim(Claims.Subject)!);
        if (user is null)
        {
            return Forbid();
        }

        user.PublicEmail = PublicEmail;
        IdentityResult result = await _userManager.UpdateAsync(user);
        if (result.Succeeded)
        {
            return Ok();
        }
        else
        {
            foreach (IdentityError error in result.Errors)
            {
                ModelState.AddModelError(nameof(PublicEmail), error.Description);
            }
            return BadRequest(ModelState);
        }
    }



    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditDescription([FromForm][StringLength(1024)] string Description)
    {
        var user = await _userManager.FindByIdAsync(User.GetClaim(Claims.Subject)!);
        if (user is null)
        {
            return Forbid();
        }

        user.Description = Description;
        IdentityResult result = await _userManager.UpdateAsync(user);
        if (result.Succeeded)
        {
            return Ok();
        }
        else
        {
            foreach (IdentityError error in result.Errors)
            {
                ModelState.AddModelError(nameof(Description), error.Description);
            }
            return BadRequest(ModelState);
        }
    }





}