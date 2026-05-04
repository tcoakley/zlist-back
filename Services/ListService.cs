using zListBack.Models;
using zListBack.Dtos;
using zListBack.Repositories;
using zListBack.Mappers;

namespace zListBack.Services
{
    public class ListService
    {
        private readonly ListRepository _listRepository;

        public ListService(ListRepository listRepository)
        {
            _listRepository = listRepository;
        }

        public async Task<Result<ListModel>> AddList(ListModel listModel, int userId)
        {
            try
            {
                var listEntity = ListMapper.ToEntity(listModel);
                listEntity.CreatedAt = DateTime.UtcNow;
                listEntity.UpdatedAt = DateTime.UtcNow;

                var result = await _listRepository.AddList(listEntity, userId);
                if (!result.Success || result.Model == null)
                    return Result<ListModel>.Fail(result.Message ?? "Failed to add list.");

                var resultDto = ListMapper.ToModel(result.Model);
                return Result<ListModel>.Ok(resultDto);
            }
            catch (Exception ex)
            {
                return Result<ListModel>.Fail(ex.Message);
            }
        }

        public async Task<Result<ListItemModel>> AddListItem(ListItemModel model)
        {
            var entity = ListItemMapper.ToEntity(model);
            var result = await _listRepository.AddListItem(entity);

            return result.Success && result.Model != null
                ? Result<ListItemModel>.Ok(ListItemMapper.ToModel(result.Model))
                : Result<ListItemModel>.Fail(result.Message ?? "Failed to add list item.");
        }

        public async Task<Result<ListModel>> GetList(int id)
        {
            var result = await _listRepository.GetList(id);
            return result.Success && result.Model != null
                ? Result<ListModel>.Ok(ListMapper.ToModel(result.Model))
                : Result<ListModel>.Fail(result.Message ?? "List not found.");
        }

        public async Task<Result<List<ListModel>>> GetLists()
        {
            var result = await _listRepository.GetLists();
            return result.Success && result.Model != null
                ? Result<List<ListModel>>.Ok(result.Model.Select(ListMapper.ToModel).ToList())
                : Result<List<ListModel>>.Fail(result.Message ?? "Failed to retrieve lists.");
        }

        public async Task<Result<ListRunModel>> CreateListRun(int listId)
        {
            var result = await _listRepository.CreateListRun(listId);
            return result.Success && result.Model != null
                ? Result<ListRunModel>.Ok(ListRunMapper.ToModel(result.Model))
                : Result<ListRunModel>.Fail(result.Message ?? "Failed to create list run.");
        }

        public async Task<Result<List<ListRunModel>>> GetListRuns(int listId)
        {
            var result = await _listRepository.GetListRuns(listId);
            return result.Success && result.Model != null
                ? Result<List<ListRunModel>>.Ok(result.Model.Select(ListRunMapper.ToModel).ToList())
                : Result<List<ListRunModel>>.Fail(result.Message ?? "Failed to get list runs.");
        }

        public async Task<Result<ListRunItemModel>> AddListRunItem(int listRunId, ListItemModel model, bool oneTime)
        {
            var itemEntity = ListItemMapper.ToEntity(model);

            var result = await _listRepository.AddListRunItem(listRunId, itemEntity, oneTime);
            return result.Success && result.Model != null
                ? Result<ListRunItemModel>.Ok(ListRunItemMapper.ToModel(result.Model))
                : Result<ListRunItemModel>.Fail(result.Message ?? "Failed to add list run item.");
        }

        public async Task<Result<bool>> EditList(ListModel model)
        {
            var entity = ListMapper.ToEntity(model);
            return await _listRepository.EditList(entity);
        }

        public async Task<Result<bool>> EditListItem(ListItemModel model)
        {
            var entity = ListItemMapper.ToEntity(model);
            return await _listRepository.EditListItem(entity);
        }

        public async Task<Result<bool>> DeleteListItem(int itemId)
        {
            return await _listRepository.DeleteListItem(itemId);
        }

        public async Task<Result<bool>> DeleteList(int listId)
        {
            return await _listRepository.DeleteList(listId);
        }
    }
}
