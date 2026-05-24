using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using zListBack.Dtos;
using zListBack.Models;
using zListBack.Repositories;

namespace zListBack.Controllers
{
    [ApiController]
    [Route("api/version")]
    [AllowAnonymous]
    public class VersionController : ControllerBase
    {
        private readonly AppVersionRepository _repo;

        public VersionController(AppVersionRepository repo)
        {
            _repo = repo;
        }

        [HttpGet]
        public async Task<Result<List<AppVersionModel>>> GetVersions()
        {
            var result = await _repo.GetAllVersions();
            if (!result.Success)
                return Result<List<AppVersionModel>>.Fail(result.Message ?? "Failed to load versions.");

            var models = result.Model!.Select(v => new AppVersionModel
            {
                Version = v.Version,
                ReleasedAt = v.ReleasedAt,
                Notes = v.Notes
            }).ToList();

            return Result<List<AppVersionModel>>.Ok(models);
        }
    }
}
