using Guncho.Services;
using Guncho.Shared.Models;
using Guncho.Repositories;
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
        private readonly RealmRepository _realmRepository;
        private readonly RealmAssetRepository _assetRepository;

        public RealmAssetsController(IRealmService realmService, IServerConfiguration config, RealmRepository realmRepository, RealmAssetRepository assetRepository)
        {
            _realmService = realmService;
            _config = config;
            _realmRepository = realmRepository;
            _assetRepository = assetRepository;
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

            var meta = await _realmRepository.GetByNameAsync(realmName);
            if (meta == null) return NotFound();

            var assets = await _assetRepository.GetAllForRealmAsync(meta.Id);
            var list = assets.Select(a => new RealmAssetSummaryDto
            {
                Path = a.Name,
                ContentType = a.ContentType,
                Version = (int)new DateTimeOffset(a.UpdatedAt).ToUnixTimeSeconds(),
                UpdatedAt = new DateTimeOffset(a.UpdatedAt)
            }).ToList();

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

            var meta = await _realmRepository.GetByNameAsync(realmName);
            if (meta == null) return NotFound();

            var normalized = NormalizePath(assetPath);
            var contentBytes = await _assetRepository.GetContentAsync(meta.Id, normalized);
            if (contentBytes == null) return NotFound();

            var info = (await _assetRepository.GetAllForRealmAsync(meta.Id)).FirstOrDefault(a => a.Name == normalized);
            var dto = new RealmAssetDto
            {
                Path = normalized,
                ContentType = info?.ContentType ?? "text/plain",
                Version = info != null ? (int)new DateTimeOffset(info.UpdatedAt).ToUnixTimeSeconds() : 0,
                UpdatedAt = info?.UpdatedAt ?? DateTimeOffset.UtcNow,
                Content = Encoding.UTF8.GetString(contentBytes)
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

            var meta = await _realmRepository.GetByNameAsync(realmName);
            if (meta == null) return NotFound();

            var normalized = NormalizePath(assetPath);
            var contentBytes = Encoding.UTF8.GetBytes(update.Content ?? string.Empty);
            await _assetRepository.SaveAsync(meta.Id, normalized, contentBytes, update.ContentType ?? "text/plain");

            // Compile and reload the realm, transferring players
            var outcome = await _realmService.UpdateRealmSourceAsync(realm);
            if (outcome != RealmEditingOutcome.Success)
            {
                return Conflict($"Compilation failed: {outcome}");
            }

            var dto = new RealmAssetDto
            {
                Path = normalized,
                ContentType = update.ContentType ?? "text/plain",
                Version = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                UpdatedAt = DateTimeOffset.UtcNow,
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

            var meta = await _realmRepository.GetByNameAsync(realmName);
            if (meta == null) return NotFound();

            var normalized = NormalizePath(assetPath);
            await _assetRepository.DeleteAsync(meta.Id, normalized);

            return NoContent();
        }

        private static bool IsSupportedAsset(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath)) return false;
            var normalized = NormalizePath(assetPath);
            // Accept common Inform 7/6 source and header files
            return normalized.EndsWith(".ni", StringComparison.OrdinalIgnoreCase)
                || normalized.EndsWith(".inf", StringComparison.OrdinalIgnoreCase)
                || normalized.EndsWith(".h", StringComparison.OrdinalIgnoreCase)
                || normalized.EndsWith(".i6t", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizePath(string assetPath)
        {
            return assetPath.Replace('\\', '/').Trim('/');
        }

        [HttpPut("_main")]
        public async Task<IActionResult> SetMainFileAsync(string realmName, [FromBody] RealmAssetSummaryDto dto)
        {
            var realm = await _realmService.GetRealmAsync(realmName);
            if (realm == null) return NotFound();

            var meta = await _realmRepository.GetByNameAsync(realmName);
            if (meta == null) return NotFound();

            if (string.IsNullOrWhiteSpace(dto.Path)) return BadRequest("Path is required");
            var normalized = NormalizePath(dto.Path);

            await _realmRepository.SetMainFileAsync(meta.Id, normalized);
            return NoContent();
        }
    }
}
