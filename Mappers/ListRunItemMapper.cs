using zListBack.Models;
using zListBack.Dtos;

namespace zListBack.Mappers
{
    public static class ListRunItemMapper
    {
        public static ListRunItem ToEntity(ListRunItemModel model)
        {
            return new ListRunItem
            {
                Id = model.Id,
                ListRunId = model.ListRunId,
                ListItemId = model.ListItemId,
                ListItemName = model.ListItemName,
                ListItemDescription = model.ListItemDescription,
                CompletedAt = model.CompletedAt,
                CompletedBy = model.CompletedBy
            };
        }

        public static ListRunItemModel ToModel(ListRunItem entity)
        {
            return new ListRunItemModel
            {
                Id = entity.Id,
                ListRunId = entity.ListRunId,
                ListItemId = entity.ListItemId,
                ListItemName = entity.ListItemName,
                ListItemDescription = entity.ListItemDescription,
                CompletedAt = entity.CompletedAt,
                CompletedBy = entity.CompletedBy
            };
        }
    }
}
