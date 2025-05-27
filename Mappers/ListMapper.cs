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
            CreatedAt = model.CreatedAt,
            UpdatedAt = model.UpdatedAt,
            Items = model.Items.Select(i => new ListItem
            {
                Id = i.Id,
                ItemName = i.ItemName,
                ItemDescription = i.ItemDescription
            }).ToList()
        };

        public static ListModel ToModel(List entity) => new ListModel
        {
            Id = entity.Id,
            ListName = entity.ListName,
            ListDescription = entity.ListDescription,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            Items = entity.Items.Select(i => new ListItemModel
            {
                Id = i.Id,
                ItemName = i.ItemName,
                ItemDescription = i.ItemDescription
            }).ToList()
        };
    }
}
