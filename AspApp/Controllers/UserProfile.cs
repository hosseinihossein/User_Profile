using System.ComponentModel.DataAnnotations;
using System.Net;
using AspApp.Helpers;
using AspApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Driver;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace AspApp.Controllers;

[ApiController]
[Route("api/[controller]/[action]")]
public class UserProfileController : ControllerBase
{
    private readonly string _storage_Users = Path.Combine("Storage", "Identity", "Users");
    private readonly IMongoCollection<BsonDocument> _userProfileCollection;



    public UserProfileController(
        IMongoDatabase userDb,
        IConfiguration configuration
    )
    {
        _userProfileCollection = userDb.GetCollection<BsonDocument>(configuration["MongoDB:ProfileCollectionName"] ?? "Profile");
    }



    // **********    Anti Forgery    **********
    [HttpGet]
    [GenerateAntiforgeryTokenCookie]
    [Authorize]
    public IActionResult GetAntiForgery()
    {
        return Ok();
    }



    // **********    Turnstile    **********
    [HttpGet]
    public IActionResult IsTurnstileEnabled([FromServices] IConfiguration configuration)
    {
        return Ok(new { turnstileEnabled = configuration.GetValue<bool>("Turnstile:Enabled", false) });
    }



    // **********    Profile Model    **********
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> ProfileModel()
    {
        if (!Guid.TryParseExact(User.GetClaim(Claims.Subject), "D", out Guid userGuid))
        {
            ModelState.AddModelError("User", "Couldn't parse user id!");
            return BadRequest(ModelState);
        }

        string? json = await GetProfileJson(userGuid);

        if (json is null)
        {
            ModelState.AddModelError("User", "User not found!");
            return BadRequest(ModelState);
        }

        return Content(json, "application/json");

        /*User_Profile_Model profileModel = new()
        {
            CreatedAt = user.CreatedAt,
            Description = user.Description,
            DisplayName = user.DisplayName,
            Email = user.Email!,                   //need to get from claims not database
            EmailConfirmed = user.EmailConfirmed,
            HasImage = user.HasImage,
            Id = user.Id,                          //need to get from claims not database
            ImageVersion = user.ImageVersion,
            PublicEmail = user.PublicEmail,
            RemoteImageUrl = user.RemoteImageUrl,
            Username = user.UserName!,
            Roles = [.. await _userManager.GetRolesAsync(user)],//need to get from claims not database
        };

        return Ok(profileModel);*/
    }

    [HttpGet]
    public async Task<IActionResult> ProfileModel([FromQuery] string userId)
    {
        if (!Guid.TryParseExact(userId, "D", out Guid userGuid))
        {
            ModelState.AddModelError("User", "Couldn't parse user id!");
            return BadRequest(ModelState);
        }

        string? json = await GetProfileJson(userGuid);

        if (json is null)
        {
            ModelState.AddModelError("User", "User not found!");
            return BadRequest(ModelState);
        }

        return Content(json, "application/json");
    }

    private async Task<string?> GetProfileJson(Guid userGuid)
    {
        var filter = Builders<BsonDocument>.Filter
        .Eq("_id", new BsonBinaryData(userGuid, GuidRepresentation.Standard));

        using var cursor = await _userProfileCollection.FindAsync(filter);
        var userProfileBsonDoc = await cursor.FirstOrDefaultAsync();

        if (userProfileBsonDoc is null)
        {
            return null;
        }

        string json = userProfileBsonDoc.ToJson(
            new JsonWriterSettings()
            {
                OutputMode = JsonOutputMode.RelaxedExtendedJson,
            }
        );

        return json;
    }



    // **********    Profile Image    **********
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



    // **********    Name    **********
    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditName([FromForm][StringLength(64)] string DisplayName)
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



    // **********    Email Privacy    **********
    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EmailPrivacy([FromForm] bool PublicEmail)
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



    // **********    Description    **********
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