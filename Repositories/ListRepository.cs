using System.Data;
using Dapper;
using zListBack.Models;


namespace zListBack.Repositories
{
    public class ListRepository
    {
        private readonly IDbConnection _connection;

        public ListRepository(IDbConnection connection)
        {
            _connection = connection;
        }

        public async Task<Result<List>> AddList(List list, int userId)
        {
            try
            {
                const string insertListSql = @"
			INSERT INTO Lists
			(
				ListName,
				ListDescription
			)
			OUTPUT INSERTED.Id, INSERTED.CreatedAt, INSERTED.UpdatedAt
			VALUES
			(
				@ListName,
				@ListDescription
			);";

                const string insertUserListSql = @"
			INSERT INTO UserLists
			(
				UserId,
				ListId
			)
			VALUES
			(
				@UserId,
				@ListId
			);";

                using var transaction = _connection.BeginTransaction();

                var insertedList = await _connection.QuerySingleAsync<List>(
                    insertListSql,
                    new
                    {
                        list.ListName,
                        list.ListDescription
                    },
                    transaction
                );

                list.Id = insertedList.Id;
                list.CreatedAt = insertedList.CreatedAt;
                list.UpdatedAt = insertedList.UpdatedAt;

                await _connection.ExecuteAsync(
                    insertUserListSql,
                    new
                    {
                        UserId = userId,
                        ListId = list.Id
                    },
                    transaction
                );

                transaction.Commit();

                return Result<List>.Ok(list);
            }
            catch (Exception ex)
            {
                return Result<List>.Fail(ex.Message);
            }
        }

        public async Task<Result<bool>> EditList(List updatedList)
        {
            try
            {
                const string sql = @"
			UPDATE Lists
			SET
				ListName = @ListName,
				ListDescription = @ListDescription,
				UpdatedAt = GETUTCDATE()
			WHERE Id = @Id;";

                var rowsAffected = await _connection.ExecuteAsync(
                    sql,
                    new
                    {
                        updatedList.Id,
                        updatedList.ListName,
                        updatedList.ListDescription
                    }
                );

                if (rowsAffected == 0)
                    return Result<bool>.Fail("List not found");

                return Result<bool>.Ok(true);
            }
            catch (Exception ex)
            {
                return Result<bool>.Fail(ex.Message);
            }
        }

        public async Task<Result<bool>> DeleteList(int listId)
        {
            try
            {
                const string listExistsSql = @"
			        SELECT COUNT(1)
			        FROM Lists
			        WHERE Id = @ListId;";

                const string deleteUserListsSql = @"
			        DELETE FROM UserLists
			        WHERE ListId = @ListId;";

                const string deleteListRunItemsSql = @"
			        DELETE lri
			        FROM ListRunItems lri
			        INNER JOIN ListRuns lr ON lr.Id = lri.ListRunId
			        WHERE lr.ListId = @ListId;";

                const string deleteListRunsSql = @"
			        DELETE FROM ListRuns
			        WHERE ListId = @ListId;";

                const string deleteListItemsSql = @"
			        DELETE FROM ListItems
			        WHERE ListId = @ListId;";

                const string deleteListSql = @"
			        DELETE FROM Lists
			        WHERE Id = @ListId;";

                var exists = await _connection.ExecuteScalarAsync<int>(
                    listExistsSql,
                    new { ListId = listId }
                );

                if (exists == 0)
                    return Result<bool>.Fail("List not found");

                using var transaction = _connection.BeginTransaction();

                await _connection.ExecuteAsync(
                    deleteUserListsSql,
                    new { ListId = listId },
                    transaction
                );

                await _connection.ExecuteAsync(
                    deleteListRunItemsSql,
                    new { ListId = listId },
                    transaction
                );

                await _connection.ExecuteAsync(
                    deleteListRunsSql,
                    new { ListId = listId },
                    transaction
                );

                await _connection.ExecuteAsync(
                    deleteListItemsSql,
                    new { ListId = listId },
                    transaction
                );

                await _connection.ExecuteAsync(
                    deleteListSql,
                    new { ListId = listId },
                    transaction
                );

                transaction.Commit();

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
                const string sql = @"
			        INSERT INTO ListItems
			        (
				        ListId,
				        ItemName,
				        ItemDescription,
				        SortOrder
			        )
			        OUTPUT INSERTED.Id
			        VALUES
			        (
				        @ListId,
				        @ItemName,
				        @ItemDescription,
				        @SortOrder
			        );";

                var newId = await _connection.ExecuteScalarAsync<int>(
                    sql,
                    new
                    {
                        item.ListId,
                        item.ItemName,
                        item.ItemDescription,
                        item.SortOrder
                    }
                );

                item.Id = newId;

                return Result<ListItem>.Ok(item);
            }
            catch (Exception ex)
            {
                return Result<ListItem>.Fail(ex.Message);
            }
        }

        public async Task<Result<bool>> EditListItem(ListItem updatedItem)
        {
            try
            {
                const string sql = @"
			        UPDATE ListItems
			        SET
				        ItemName = @ItemName,
				        ItemDescription = @ItemDescription,
				        SortOrder = @SortOrder
			        WHERE Id = @Id;";

                var rowsAffected = await _connection.ExecuteAsync(
                    sql,
                    new
                    {
                        updatedItem.Id,
                        updatedItem.ItemName,
                        updatedItem.ItemDescription,
                        updatedItem.SortOrder
                    }
                );

                if (rowsAffected == 0)
                    return Result<bool>.Fail("List item not found");

                return Result<bool>.Ok(true);
            }
            catch (Exception ex)
            {
                return Result<bool>.Fail(ex.Message);
            }
        }

        public async Task<Result<bool>> DeleteListItem(int itemId)
        {
            try
            {
                const string itemExistsSql = @"
			        SELECT COUNT(1)
			        FROM ListItems
			        WHERE Id = @ItemId;";

                const string unlinkRunItemsSql = @"
			        UPDATE ListRunItems
			        SET ListItemId = NULL
			        WHERE ListItemId = @ItemId;";

                const string deleteItemSql = @"
			        DELETE FROM ListItems
			        WHERE Id = @ItemId;";

                var exists = await _connection.ExecuteScalarAsync<int>(
                    itemExistsSql,
                    new { ItemId = itemId }
                );

                if (exists == 0)
                    return Result<bool>.Fail("List item not found");

                using var transaction = _connection.BeginTransaction();

                await _connection.ExecuteAsync(
                    unlinkRunItemsSql,
                    new { ItemId = itemId },
                    transaction
                );

                await _connection.ExecuteAsync(
                    deleteItemSql,
                    new { ItemId = itemId },
                    transaction
                );

                transaction.Commit();

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
                const string listSql = @"
			        SELECT
				        Id,
				        ListName,
				        ListDescription,
				        CreatedAt,
				        UpdatedAt
			        FROM Lists
			        WHERE Id = @Id;";

                const string itemsSql = @"
			        SELECT
				        Id,
				        ListId,
				        ItemName,
				        ItemDescription,
				        SortOrder
			        FROM ListItems
			        WHERE ListId = @ListId
			        ORDER BY SortOrder, Id;";

                var list = await _connection.QuerySingleOrDefaultAsync<List>(
                    listSql,
                    new { Id = id }
                );

                if (list == null)
                    return Result<List>.Fail("List not found");

                var items = await _connection.QueryAsync<ListItem>(
                    itemsSql,
                    new { ListId = id }
                );

                list.Items = items.ToList();

                return Result<List>.Ok(list);
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
                const string listsSql = @"
			        SELECT
				        Id,
				        ListName,
				        ListDescription,
				        CreatedAt,
				        UpdatedAt
			        FROM Lists
			        ORDER BY Id;";

                const string itemsSql = @"
			        SELECT
				        Id,
				        ListId,
				        ItemName,
				        ItemDescription,
				        SortOrder
			        FROM ListItems
			        ORDER BY ListId, SortOrder, Id;";

                var lists = (await _connection.QueryAsync<List>(listsSql)).ToList();
                var items = (await _connection.QueryAsync<ListItem>(itemsSql)).ToList();

                var itemsByListId = items
                    .GroupBy(i => i.ListId)
                    .ToDictionary(g => g.Key, g => g.ToList());

                foreach (var list in lists)
                {
                    list.Items = itemsByListId.TryGetValue(list.Id, out var listItems)
                        ? listItems
                        : new List<ListItem>();
                }

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
                const string listExistsSql = @"
			        SELECT COUNT(1)
			        FROM Lists
			        WHERE Id = @ListId;";

                const string listItemsSql = @"
			        SELECT
				        Id,
				        ListId,
				        ItemName,
				        ItemDescription,
				        SortOrder
			        FROM ListItems
			        WHERE ListId = @ListId
			        ORDER BY SortOrder, Id;";

                const string insertListRunSql = @"
			        INSERT INTO ListRuns
			        (
				        ListId
			        )
			        OUTPUT INSERTED.Id, INSERTED.CreatedAt
			        VALUES
			        (
				        @ListId
			        );";

                const string insertListRunItemSql = @"
			        INSERT INTO ListRunItems
			        (
				        ListRunId,
				        ListItemId,
				        ListItemName,
				        ListItemDescription,
				        SortOrder
			        )
			        OUTPUT INSERTED.Id
			        VALUES
			        (
				        @ListRunId,
				        @ListItemId,
				        @ListItemName,
				        @ListItemDescription,
				        @SortOrder
			        );";

                var exists = await _connection.ExecuteScalarAsync<int>(
                    listExistsSql,
                    new { ListId = listId }
                );

                if (exists == 0)
                    return Result<ListRun>.Fail("List not found");

                var listItems = (await _connection.QueryAsync<ListItem>(
                    listItemsSql,
                    new { ListId = listId }
                )).ToList();

                using var transaction = _connection.BeginTransaction();

                var insertedRun = await _connection.QuerySingleAsync<ListRun>(
                    insertListRunSql,
                    new { ListId = listId },
                    transaction
                );

                insertedRun.ListId = listId;
                insertedRun.Items = new List<ListRunItem>();

                foreach (var item in listItems)
                {
                    var runItem = new ListRunItem
                    {
                        ListRunId = insertedRun.Id,
                        ListItemId = item.Id,
                        ListItemName = item.ItemName,
                        ListItemDescription = item.ItemDescription,
                        SortOrder = item.SortOrder
                    };

                    var newRunItemId = await _connection.ExecuteScalarAsync<int>(
                        insertListRunItemSql,
                        new
                        {
                            runItem.ListRunId,
                            runItem.ListItemId,
                            runItem.ListItemName,
                            runItem.ListItemDescription,
                            runItem.SortOrder
                        },
                        transaction
                    );

                    runItem.Id = newRunItemId;
                    insertedRun.Items.Add(runItem);
                }

                transaction.Commit();

                return Result<ListRun>.Ok(insertedRun);
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
                const string runsSql = @"
			        SELECT
				        Id,
				        ListId,
				        CreatedAt
			        FROM ListRuns
			        WHERE ListId = @ListId
			        ORDER BY CreatedAt DESC, Id DESC;";

                const string runItemsSql = @"
			        SELECT
				        Id,
				        ListRunId,
				        ListItemId,
				        ListItemName,
				        ListItemDescription,
				        SortOrder,
				        CompletedAt,
				        CompletedBy
			        FROM ListRunItems
			        WHERE ListRunId IN
			        (
				        SELECT Id
				        FROM ListRuns
				        WHERE ListId = @ListId
			        )
			        ORDER BY ListRunId, SortOrder, Id;";

                var runs = (await _connection.QueryAsync<ListRun>(
                    runsSql,
                    new { ListId = listId }
                )).ToList();

                var runItems = (await _connection.QueryAsync<ListRunItem>(
                    runItemsSql,
                    new { ListId = listId }
                )).ToList();

                var itemsByRunId = runItems
                    .GroupBy(i => i.ListRunId)
                    .ToDictionary(g => g.Key, g => g.ToList());

                foreach (var run in runs)
                {
                    run.Items = itemsByRunId.TryGetValue(run.Id, out var items)
                        ? items
                        : new List<ListRunItem>();
                }

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
                const string insertListItemSql = @"
			        INSERT INTO ListItems
			        (
				        ListId,
				        ItemName,
				        ItemDescription,
				        SortOrder
			        )
			        OUTPUT INSERTED.Id
			        VALUES
			        (
				        @ListId,
				        @ItemName,
				        @ItemDescription,
				        @SortOrder
			        );";

                const string insertListRunItemSql = @"
			        INSERT INTO ListRunItems
			        (
				        ListRunId,
				        ListItemId,
				        ListItemName,
				        ListItemDescription,
				        SortOrder
			        )
			        OUTPUT INSERTED.Id
			        VALUES
			        (
				        @ListRunId,
				        @ListItemId,
				        @ListItemName,
				        @ListItemDescription,
				        @SortOrder
			        );";

                using var transaction = _connection.BeginTransaction();

                if (!oneTime)
                {
                    var newListItemId = await _connection.ExecuteScalarAsync<int>(
                        insertListItemSql,
                        new
                        {
                            item.ListId,
                            item.ItemName,
                            item.ItemDescription,
                            item.SortOrder
                        },
                        transaction
                    );

                    item.Id = newListItemId;
                }

                var listRunItem = new ListRunItem
                {
                    ListRunId = listRunId,
                    ListItemId = oneTime ? null : item.Id,
                    ListItemName = item.ItemName,
                    ListItemDescription = item.ItemDescription,
                    SortOrder = item.SortOrder
                };

                var newRunItemId = await _connection.ExecuteScalarAsync<int>(
                    insertListRunItemSql,
                    new
                    {
                        listRunItem.ListRunId,
                        listRunItem.ListItemId,
                        listRunItem.ListItemName,
                        listRunItem.ListItemDescription,
                        listRunItem.SortOrder
                    },
                    transaction
                );

                listRunItem.Id = newRunItemId;

                transaction.Commit();

                return Result<ListRunItem>.Ok(listRunItem);
            }
            catch (Exception ex)
            {
                return Result<ListRunItem>.Fail(ex.Message);
            }
        }


    }
}
