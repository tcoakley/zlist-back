using zListBack.Models;
using zListBack.Dtos;

namespace zListBack.Mappers
{
    public static class ListRunMapper
    {
        public static ListRun ToEntity(ListRunModel model)
        {
            return new ListRun
            {
                Id = model.Id,
                ListId = model.ListId,
                CreatedAt = model.CreatedAt,
                Items = model.Items.Select(ListRunItemMapper.ToEntity).ToList()
            };
        }

        public static ListRunModel ToModel(ListRun entity)
        {
            return new ListRunModel
            {
                Id = entity.Id,
                ListId = entity.ListId,
                CreatedAt = entity.CreatedAt,
                Items = entity.Items.Select(ListRunItemMapper.ToModel).ToList()
            };
        }
    }
}
