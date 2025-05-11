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

        public async Task<Result<List>> AddList(List list)
        {
            try
            {
                list.CreatedAt = DateTime.UtcNow;
                _context.Lists.Add(list);
                await _context.SaveChangesAsync();
                return Result<List>.Ok(list);
            }
            catch (Exception ex)
            {
                return Result<List>.Fail(ex.Message);
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
                    ListItemDescription = item.ItemDescription
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

        public async Task<Result<ListRunItem>> AddListRunItem(ListItemModel model, bool oneTime)
        {
            try
            {
                ListItem? item = null;
                if (!oneTime)
                {
                    item = new ListItem
                    {
                        ListId = model.ListId,
                        ItemName = model.ItemName,
                        ItemDescription = model.ItemDescription
                    };
                    _context.ListItems.Add(item);
                    await _context.SaveChangesAsync();
                }

                var listRunItem = new ListRunItem
                {
                    ListRunId = model.Id,
                    ListItemId = item?.Id ?? model.Id,
                    ListItemName = model.ItemName,
                    ListItemDescription = model.ItemDescription
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
