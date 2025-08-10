using Microsoft.EntityFrameworkCore;
using zListBack.Data;
using zListBack.Models;
using zListBack.Dtos;

namespace zListBack.Repositories
{
    public class ListRepository
    {
        private readonly AppDbContext _context;

        public ListRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Result<List>> AddList(List list, int userId)
        {
            try
            {
                list.CreatedAt = DateTime.UtcNow;
                list.UpdatedAt = DateTime.UtcNow;
                _context.Lists.Add(list);
                await _context.SaveChangesAsync();

                var userList = new UserList
                {
                    UserId = userId,
                    ListId = list.Id
                };

                _context.UserLists.Add(userList);
                await _context.SaveChangesAsync();

                return Result<List>.Ok(list);
            }
            catch (Exception ex)
            {
                return Result<List>.Fail(ex.Message);
            }
        }

        public async Task<Result<List>> EditList(List updatedList)
        {
            try
            {
                var existingList = await _context.Lists.FirstOrDefaultAsync(l => l.Id == updatedList.Id);
                if (existingList == null)
                    return Result<List>.Fail("List not found");

                existingList.ListName = updatedList.ListName;
                existingList.ListDescription = updatedList.ListDescription;
                existingList.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                return Result<List>.Ok(existingList);
            }
            catch (Exception ex)
            {
                return Result<List>.Fail(ex.Message);
            }
        }

        public async Task<Result<bool>> DeleteList(int listId)
        {
            try
            {
                var list = await _context.Lists.Include(l => l.Items).FirstOrDefaultAsync(l => l.Id == listId);
                if (list == null)
                    return Result<bool>.Fail("List not found");

                var userLists = await _context.UserLists.Where(ul => ul.ListId == listId).ToListAsync();
                _context.UserLists.RemoveRange(userLists);
                _context.ListItems.RemoveRange(list.Items);
                _context.Lists.Remove(list);

                await _context.SaveChangesAsync();
                return Result<bool>.Ok(true);
            }
            catch (Exception ex)
            {
                return Result<bool>.Fail(ex.Message);
            }
        }

        public async Task<Result<ListItem>> AddListItem(ListItem item)
        {
            try
            {
                _context.ListItems.Add(item);
                await _context.SaveChangesAsync();
                return Result<ListItem>.Ok(item);
            }
            catch (Exception ex)
            {
                return Result<ListItem>.Fail(ex.Message);
            }
        }

        public async Task<Result<ListItem>> EditListItem(ListItem updatedItem)
        {
            try
            {
                var existingItem = await _context.ListItems.FirstOrDefaultAsync(i => i.Id == updatedItem.Id);
                if (existingItem == null)
                    return Result<ListItem>.Fail("List item not found");

                existingItem.ItemName = updatedItem.ItemName;
                existingItem.ItemDescription = updatedItem.ItemDescription;
                existingItem.SortOrder = updatedItem.SortOrder;

                await _context.SaveChangesAsync();
                return Result<ListItem>.Ok(existingItem);
            }
            catch (Exception ex)
            {
                return Result<ListItem>.Fail(ex.Message);
            }
        }

        public async Task<Result<bool>> DeleteListItem(int itemId)
        {
            try
            {
                var item = await _context.ListItems.FindAsync(itemId);
                if (item == null)
                    return Result<bool>.Fail("List item not found");

                var linkedRunItems = _context.ListRunItems
                    .Where(r => r.ListItemId == itemId);

                await foreach (var runItem in linkedRunItems.AsAsyncEnumerable())
                {
                    runItem.ListItemId = null;
                }

                await _context.SaveChangesAsync();

                _context.ListItems.Remove(item);
                await _context.SaveChangesAsync();

                return Result<bool>.Ok(true);
            }
            catch (Exception ex)
            {
                return Result<bool>.Fail(ex.Message);
            }
        }


        public async Task<Result<List>> GetList(int id)
        {
            try
            {
                var list = await _context.Lists.Include(l => l.Items).FirstOrDefaultAsync(l => l.Id == id);
                return list != null ? Result<List>.Ok(list) : Result<List>.Fail("List not found");
            }
            catch (Exception ex)
            {
                return Result<List>.Fail(ex.Message);
            }
        }

        public async Task<Result<List<List>>> GetLists()
        {
            try
            {
                var lists = await _context.Lists.Include(l => l.Items).ToListAsync();
                return Result<List<List>>.Ok(lists);
            }
            catch (Exception ex)
            {
                return Result<List<List>>.Fail(ex.Message);
            }
        }

        public async Task<Result<ListRun>> CreateListRun(int listId)
        {
            try
            {
                var list = await _context.Lists.Include(l => l.Items).FirstOrDefaultAsync(l => l.Id == listId);
                if (list == null)
                    return Result<ListRun>.Fail("List not found");

                var listRun = new ListRun
                {
                    ListId = listId,
                    CreatedAt = DateTime.UtcNow
                };
                _context.ListRuns.Add(listRun);
                await _context.SaveChangesAsync();

                var runItems = list.Items.Select(item => new ListRunItem
                {
                    ListRunId = listRun.Id,
                    ListItemId = item.Id,
                    ListItemName = item.ItemName,
                    ListItemDescription = item.ItemDescription,
                    SortOrder = item.SortOrder,
                }).ToList();

                _context.ListRunItems.AddRange(runItems);
                await _context.SaveChangesAsync();

                listRun.Items = runItems;
                return Result<ListRun>.Ok(listRun);
            }
            catch (Exception ex)
            {
                return Result<ListRun>.Fail(ex.Message);
            }
        }

        public async Task<Result<List<ListRun>>> GetListRuns(int listId)
        {
            try
            {
                var runs = await _context.ListRuns
                    .Where(r => r.ListId == listId)
                    .Include(r => r.Items)
                    .ToListAsync();

                return Result<List<ListRun>>.Ok(runs);
            }
            catch (Exception ex)
            {
                return Result<List<ListRun>>.Fail(ex.Message);
            }
        }

        public async Task<Result<ListRunItem>> AddListRunItem(int listRunId, ListItem item, bool oneTime)
        {
            try
            {
                if (!oneTime)
                {
                    _context.ListItems.Add(item);
                    await _context.SaveChangesAsync();
                }

                var listRunItem = new ListRunItem
                {
                    ListRunId = listRunId,
                    ListItemId = oneTime ? null : item.Id,
                    ListItemName = item.ItemName,
                    ListItemDescription = item.ItemDescription,
                    SortOrder = item.SortOrder
                };

                _context.ListRunItems.Add(listRunItem);
                await _context.SaveChangesAsync();

                return Result<ListRunItem>.Ok(listRunItem);
            }
            catch (Exception ex)
            {
                return Result<ListRunItem>.Fail(ex.Message);
            }
        }


    }
}
