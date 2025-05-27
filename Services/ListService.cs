using zListBack.Models;
using zListBack.Dtos;
using zListBack.Repositories;

namespace zListBack.Services
{
    public class ListService
    {
        private readonly ListRepository _listRepository;

        public ListService(ListRepository listRepository)
        {
            _listRepository = listRepository;
        }

        public async Task<Result<List>> AddList(List list, int userId)
        {
            return await _listRepository.AddList(list, userId);
        }

        public async Task<Result<ListItem>> AddListItem(ListItem item)
        {
            return await _listRepository.AddListItem(item);
        }

        public async Task<Result<List>> GetList(int id)
        {
            return await _listRepository.GetList(id);
        }

        public async Task<Result<List<List>>> GetLists()
        {
            return await _listRepository.GetLists();
        }

        public async Task<Result<ListRun>> CreateListRun(int listId)
        {
            return await _listRepository.CreateListRun(listId);
        }

        public async Task<Result<List<ListRun>>> GetListRuns(int listId)
        {
            return await _listRepository.GetListRuns(listId);
        }

        public async Task<Result<ListRunItem>> AddListRunItem(ListItemModel model, bool oneTime)
        {
            return await _listRepository.AddListRunItem(model, oneTime);
        }

        public async Task<Result<List>> EditList(List list)
        {
            return await _listRepository.EditList(list);
        }

        public async Task<Result<ListItem>> EditListItem(ListItem item)
        {
            return await _listRepository.EditListItem(item);
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
