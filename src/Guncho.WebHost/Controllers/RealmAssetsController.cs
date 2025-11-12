using Guncho.Services;
using Guncho.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace Guncho.WebHost.Controllers
{
    [Route("api/realms/{realmName}/assets")]
    [Authorize]
    public sealed class RealmAssetsController : GunchoApiController
    {
        private readonly IRealmService _realmService;
        private readonly IServerConfiguration _config;

        public RealmAssetsController(IRealmService realmService, IServerConfiguration config)
        {
            _realmService = realmService;
            _config = config;
        }

        [HttpGet("manifest")]
        [AllowAnonymous]
        public async Task<ActionResult<IReadOnlyList<RealmAssetSummaryDto>>> GetManifestAsync(string realmName)
        {
            var realm = await _realmService.GetRealmAsync(realmName);
            if (realm == null)
            {
                return NotFound();
            }

            var list = new List<RealmAssetSummaryDto>();
            var filePath = GetSourceFilePath(realmName);
            if (System.IO.File.Exists(filePath))
            {
                var updated = System.IO.File.GetLastWriteTimeUtc(filePath);
                list.Add(new RealmAssetSummaryDto
                {
                    Path = "story.ni",
                    ContentType = "text/plain",
                    Version = (int)new DateTimeOffset(updated).ToUnixTimeSeconds(),
                    UpdatedAt = new DateTimeOffset(updated)
                });
            }

            return Ok(list);
        }

        [HttpGet("{**assetPath}")]
        [AllowAnonymous]
        public async Task<ActionResult<RealmAssetDto>> GetAssetAsync(string realmName, string assetPath)
        {
            if (!IsSupportedAsset(assetPath))
            {
                return NotFound();
            }

            var realm = await _realmService.GetRealmAsync(realmName);
            if (realm == null)
            {
                return NotFound();
            }

            var filePath = GetSourceFilePath(realmName);
            if (!System.IO.File.Exists(filePath))
            {
                return NotFound();
            }

            var content = await System.IO.File.ReadAllTextAsync(filePath, Encoding.UTF8);
            var updated = System.IO.File.GetLastWriteTimeUtc(filePath);
            var dto = new RealmAssetDto
            {
                Path = "story.ni",
                ContentType = "text/plain",
                Version = (int)new DateTimeOffset(updated).ToUnixTimeSeconds(),
                UpdatedAt = new DateTimeOffset(updated),
                Content = content
            };

            return Ok(dto);
        }

        [HttpPut("{**assetPath}")]
    public async Task<ActionResult<RealmAssetDto>> PutAssetAsync(string realmName, string assetPath, [FromBody] RealmAssetUpdateDto update)
        {
            if (!IsSupportedAsset(assetPath))
            {
                return NotFound();
            }

            var realm = await _realmService.GetRealmAsync(realmName);
            if (realm == null)
            {
                return NotFound();
            }

            // Simple authorization: only the owner can edit for now
            if (User?.Identity?.Name != realm.Owner.Name)
            {
                return Forbid();
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var filePath = GetSourceFilePath(realmName);
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Optimistic concurrency check
            if (System.IO.File.Exists(filePath) && update.ExpectedVersion.HasValue)
            {
                var currentVersion = (int)new DateTimeOffset(System.IO.File.GetLastWriteTimeUtc(filePath)).ToUnixTimeSeconds();
                if (currentVersion != update.ExpectedVersion.Value)
                {
                    return Conflict($"Asset has been modified since you last loaded it (expected version {update.ExpectedVersion.Value}, current {currentVersion}).");
                }
            }

            await System.IO.File.WriteAllTextAsync(filePath, update.Content ?? string.Empty, Encoding.UTF8);

            // Compile and reload the realm, transferring players
            var outcome = await _realmService.UpdateRealmSourceAsync(realm);
            if (outcome != RealmEditingOutcome.Success)
            {
                return Conflict($"Compilation failed: {outcome}");
            }

            var updated = System.IO.File.GetLastWriteTimeUtc(filePath);
            var dto = new RealmAssetDto
            {
                Path = "story.ni",
                ContentType = update.ContentType ?? "text/plain",
                Version = (int)new DateTimeOffset(updated).ToUnixTimeSeconds(),
                UpdatedAt = new DateTimeOffset(updated),
                Content = update.Content ?? string.Empty
            };

            return Ok(dto);
        }

        [HttpDelete("{**assetPath}")]
    public async Task<IActionResult> DeleteAssetAsync(string realmName, string assetPath)
        {
            if (!IsSupportedAsset(assetPath))
            {
                return NotFound();
            }

            var realm = await _realmService.GetRealmAsync(realmName);
            if (realm == null)
            {
                return NotFound();
            }

            if (User?.Identity?.Name != realm.Owner.Name)
            {
                return Forbid();
            }

            var filePath = GetSourceFilePath(realmName);
            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
            }

            return NoContent();
        }

        private static bool IsSupportedAsset(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath)) return false;
            var normalized = assetPath.Replace('\\', '/').Trim('/');
            return string.Equals(normalized, "story.ni", StringComparison.OrdinalIgnoreCase);
        }

        private string GetSourceFilePath(string realmName)
        {
            // Map logical asset story.ni -> physical file <RealmDataPath>/<RealmName>.ni
            return Path.Combine(_config.RealmDataPath, $"{realmName}.ni");
        }
    }
}
