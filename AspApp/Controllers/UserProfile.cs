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
    //private readonly string _avatarDir = Path.Combine("Storage", "UserProfileAvatar");
    private readonly IMongoCollection<BsonDocument> _userProfileCollection;
    private readonly DirectoryInfo _avatarDirInfo;



    public UserProfileController(
        IMongoDatabase userDb,
        IConfiguration configuration,
        IWebHostEnvironment env
    )
    {
        _userProfileCollection = userDb.GetCollection<BsonDocument>(configuration["MongoDB:ProfileCollectionName"] ?? "Profile");
        _avatarDirInfo = Directory.CreateDirectory(Path.Combine(env.ContentRootPath, "Storage", "UserProfileAvatar"));
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



    // **********    Model    **********
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Model()
    {
        if (!Guid.TryParseExact(User.GetClaim(Claims.Subject), "D", out Guid userGuid))
        {
            ModelState.AddModelError("User", "Couldn't parse user id!");
            return BadRequest(ModelState);
        }

        BsonDocument? bsonDoc = await GetModelBson(userGuid);

        if (bsonDoc is null)
        {
            ModelState.AddModelError("User", "User not found!");
            return BadRequest(ModelState);
        }

        return Content(BsonToJson(bsonDoc), "application/json");

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
    public async Task<IActionResult> Model([FromQuery] string userId)
    {
        if (!Guid.TryParseExact(userId, "D", out Guid userGuid))
        {
            ModelState.AddModelError("User", "Couldn't parse user id!");
            return BadRequest(ModelState);
        }

        BsonDocument? bsonDoc = await GetModelBson(userGuid);

        if (bsonDoc is null)
        {
            ModelState.AddModelError("User", "User not found!");
            return BadRequest(ModelState);
        }

        return Content(BsonToJson(bsonDoc), "application/json");
    }

    private async Task<BsonDocument?> GetModelBson(Guid userGuid, string[]? projectItems = null)
    {
        var filter = Builders<BsonDocument>.Filter
        .Eq("_id", new BsonBinaryData(userGuid, GuidRepresentation.Standard));

        IFindFluent<BsonDocument, BsonDocument> findFluent = _userProfileCollection.Find(filter);

        if (projectItems is not null && projectItems.Length > 0)
        {
            ProjectionDefinition<BsonDocument>? projection = null;
            foreach (string item in projectItems)
            {
                if (!string.IsNullOrWhiteSpace(item))
                {
                    projection = Builders<BsonDocument>.Projection.Include(item);
                }
            }

            if (projection is not null)
            {
                findFluent = findFluent.Project<BsonDocument>(projection);
            }
        }

        return await findFluent.FirstOrDefaultAsync();
    }

    private string BsonToJson(BsonDocument bsonDoc)
    {
        string json = bsonDoc.ToJson(
            new JsonWriterSettings()
            {
                OutputMode = JsonOutputMode.RelaxedExtendedJson,
            }
        );

        return json;
    }



    // **********    Avatar    **********
    [HttpGet]
    public async Task<IActionResult> Avatar([FromQuery] string avatarId)
    {
        string avatarFilePath =
        Path.Combine(_avatarDirInfo.FullName, avatarId);

        if (System.IO.File.Exists(avatarFilePath))
        {
            return PhysicalFile(avatarFilePath, "application/octet-stream", "profileAvatar", true);
        }

        return NotFound("User profile avatar Not found!");
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(128 * 1024)]//128 KB
    public async Task<IActionResult> SubmitAvatar([FromForm] IFormFile UserAvatar)
    {
        if (!Guid.TryParseExact(User.GetClaim(Claims.Subject), "D", out Guid userGuid))
        {
            ModelState.AddModelError("User", "Couldn't parse user id!");
            return BadRequest(ModelState);
        }

        BsonDocument? bsonDoc = await GetModelBson(userGuid, ["avatarId"]);
        if (bsonDoc is null)
        {
            ModelState.AddModelError("User", "User Profile not found!");
            return BadRequest(ModelState);
        }

        string avatarId = Guid.NewGuid().ToString("D");
        string avatarFilePath = Path.Combine(_avatarDirInfo.FullName, avatarId);
        using (FileStream fs = System.IO.File.Create(avatarFilePath))
        {
            await UserAvatar.CopyToAsync(fs);
        }

        //delete the old avatar file
        if (bsonDoc.TryGetValue("avatarId", out BsonValue bsonValue) && bsonValue.IsString)
        {
            string oldAvatarId = bsonValue.AsString;
            if (string.IsNullOrWhiteSpace(oldAvatarId))
            {
                DeleteAvatarFile(oldAvatarId);
            }
        }

        //update avatarId for user profile document on mongodb
        var filter = Builders<BsonDocument>.Filter
        .Eq("_id", new BsonBinaryData(userGuid, GuidRepresentation.Standard));

        var update = Builders<BsonDocument>.Update.Set("avatarId", new BsonString(avatarId));
        await _userProfileCollection.UpdateOneAsync(filter, update);

        return Ok(new { success = true, avatarId = avatarId });
    }

    private void DeleteAvatarFile(string avatarId)
    {
        string avatarFilePath = Path.Combine(_avatarDirInfo.FullName, avatarId);

        if (System.IO.File.Exists(avatarFilePath))
        {
            System.IO.File.Delete(avatarFilePath);
        }
    }

    [HttpDelete]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAvatar()
    {
        if (!Guid.TryParseExact(User.GetClaim(Claims.Subject), "D", out Guid userGuid))
        {
            ModelState.AddModelError("User", "Couldn't parse user id!");
            return BadRequest(ModelState);
        }

        BsonDocument? bsonDoc = await GetModelBson(userGuid, ["avatarId"]);
        if (bsonDoc is null)
        {
            ModelState.AddModelError("User", "User Profile not found!");
            return BadRequest(ModelState);
        }

        //delete the avatar file and update database
        if (bsonDoc.TryGetValue("avatarId", out BsonValue bsonValue) && bsonValue.IsString)
        {
            string avatarId = bsonValue.AsString;
            if (string.IsNullOrWhiteSpace(avatarId))
            {
                //delete file
                DeleteAvatarFile(avatarId);

                //update mongodb
                var filter = Builders<BsonDocument>.Filter
                .Eq("_id", new BsonBinaryData(userGuid, GuidRepresentation.Standard));
                var update = Builders<BsonDocument>.Update.Unset("avatarId");
                await _userProfileCollection.UpdateOneAsync(filter, update);
            }
        }

        return Ok(new { success = true });
    }



    // **********    Name    **********
    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditName([FromForm][StringLength(128)] string Name)
    {
        if (!Guid.TryParseExact(User.GetClaim(Claims.Subject), "D", out Guid userGuid))
        {
            ModelState.AddModelError("User", "Couldn't parse user id!");
            return BadRequest(ModelState);
        }

        //update avatarId for user profile document on mongodb
        var filter = Builders<BsonDocument>.Filter
        .Eq("_id", new BsonBinaryData(userGuid, GuidRepresentation.Standard));

        var update = Builders<BsonDocument>.Update.Set("name", new BsonString(Name));
        await _userProfileCollection.UpdateOneAsync(filter, update);

        return Ok();
    }



    // **********    Description    **********
    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditDescription([FromForm][StringLength(1024)] string Description)
    {
        if (!Guid.TryParseExact(User.GetClaim(Claims.Subject), "D", out Guid userGuid))
        {
            ModelState.AddModelError("User", "Couldn't parse user id!");
            return BadRequest(ModelState);
        }

        //update avatarId for user profile document on mongodb
        var filter = Builders<BsonDocument>.Filter
        .Eq("_id", new BsonBinaryData(userGuid, GuidRepresentation.Standard));

        var update = Builders<BsonDocument>.Update.Set("description", new BsonString(Description));
        await _userProfileCollection.UpdateOneAsync(filter, update);

        return Ok();
    }





}