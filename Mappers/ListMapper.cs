using zListBack.Models;
using zListBack.Dtos;

namespace zListBack.Mappers
{
    public static class ListMapper
    {
        public static List ToEntity(ListModel model) => new List
        {
            Id = model.Id,
            ListName = model.ListName,
            ListDescription = model.ListDescription,
            CreatedAt = model.CreatedAt ?? DateTime.UtcNow,
            UpdatedAt = model.UpdatedAt,
            ActiveRunId = model.ActiveRunId,
            TotalRuns = model.TotalRuns,
            LastRun = model.LastRun,
            TotalItems = model.TotalItems,
            Items = model.Items.Select(ListItemMapper.ToEntity).ToList()
        };

        public static ListModel ToModel(List entity) => new ListModel
        {
            Id = entity.Id,
            ListName = entity.ListName,
            ListDescription = entity.ListDescription,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            ActiveRunId = entity.ActiveRunId,
            TotalRuns = entity.TotalRuns,
            LastRun = entity.LastRun,
            TotalItems = entity.TotalItems,
            IsOwner = entity.IsOwner,
            Items = entity.Items.Select(ListItemMapper.ToModel).ToList()
        };
    }
}
