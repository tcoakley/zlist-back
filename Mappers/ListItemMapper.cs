using zListBack.Models;
using zListBack.Dtos;

namespace zListBack.Mappers
{
    public static class ListItemMapper
    {
        public static ListItem ToEntity(ListItemModel model)
        {
            return new ListItem
            {
                Id = model.Id,
                ListId = model.ListId,
                ItemName = model.ItemName,
                ItemDescription = model.ItemDescription
            };
        }

        public static ListItemModel ToModel(ListItem entity)
        {
            return new ListItemModel
            {
                Id = entity.Id,
                ListId = entity.ListId,
                ItemName = entity.ItemName,
                ItemDescription = entity.ItemDescription
            };
        }
    }
}
