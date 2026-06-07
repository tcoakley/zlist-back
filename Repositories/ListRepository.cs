using System.Data;
using Dapper;
using zListBack.Models;
using zListBack.Dtos;


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
			INSERT INTO UserLists (UserId, ListId, IsOwner)
			VALUES (@UserId, @ListId, 1);";

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
                list.IsOwner = true;

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

        // Returns all owned lists for the downgrade selection screen (includes archived).
        public async Task<System.Collections.Generic.List<List>> GetOwnedListsForSelection(int userId)
        {
            const string sql = @"
                SELECT l.Id, l.ListName, l.IsArchived,
                       COUNT(DISTINCT li.Id) AS TotalItems
                FROM Lists l
                INNER JOIN UserLists ul ON ul.ListId = l.Id AND ul.UserId = @UserId AND ul.IsOwner = 1
                LEFT JOIN ListItems li ON li.ListId = l.Id
                GROUP BY l.Id, l.ListName, l.IsArchived
                ORDER BY l.IsArchived, l.ListName;";

            return (await _connection.QueryAsync<List>(sql, new { UserId = userId })).ToList();
        }

        public async Task ArchiveUnselectedLists(int userId, IEnumerable<int> keepListIds)
        {
            var keepIds = keepListIds.ToList();
            const string sql = @"
                UPDATE l SET l.IsArchived = CASE WHEN l.Id IN @KeepIds THEN 0 ELSE 1 END
                FROM Lists l
                INNER JOIN UserLists ul ON ul.ListId = l.Id AND ul.UserId = @UserId AND ul.IsOwner = 1;";

            await _connection.ExecuteAsync(sql, new { UserId = userId, KeepIds = keepIds.Count > 0 ? keepIds : new List<int> { -1 } });
        }

        public async Task RestoreArchivedLists(int userId)
        {
            const string sql = @"
                UPDATE l SET l.IsArchived = 0
                FROM Lists l
                INNER JOIN UserLists ul ON ul.ListId = l.Id AND ul.UserId = @UserId AND ul.IsOwner = 1
                WHERE l.IsArchived = 1;";

            await _connection.ExecuteAsync(sql, new { UserId = userId });
        }

        public async Task<Result<bool>> DeleteList(int listId, int userId)
        {
            try
            {
                const string ownerCheckSql = @"
			        SELECT IsOwner
			        FROM UserLists
			        WHERE ListId = @ListId AND UserId = @UserId;";

                const string deleteInvitationsSql = @"
			        DELETE FROM ListInvitations
			        WHERE ListId = @ListId;";

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

                var isOwner = await _connection.ExecuteScalarAsync<bool?>(
                    ownerCheckSql,
                    new { ListId = listId, UserId = userId }
                );

                if (isOwner == null)
                    return Result<bool>.Fail("List not found");

                if (!isOwner.Value)
                    return Result<bool>.Fail("Only the list owner can delete a list.");

                using var transaction = _connection.BeginTransaction();

                await _connection.ExecuteAsync(
                    deleteInvitationsSql,
                    new { ListId = listId },
                    transaction
                );

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


        public async Task<Result<List>> GetList(int id, int userId)
        {
            try
            {
                const string listSql = @"
			        SELECT
				        l.Id,
				        l.ListName,
				        l.ListDescription,
				        l.CreatedAt,
				        l.UpdatedAt,
				        ul.IsOwner
			        FROM Lists l
			        INNER JOIN UserLists ul ON ul.ListId = l.Id
			        WHERE l.Id = @Id
			        AND ul.UserId = @UserId;";

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
                    new { Id = id, UserId = userId }
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

        public async Task<Result<System.Collections.Generic.List<List>>> GetLists(int userId)
        {
            try
            {
                const string listsSql = @"
                    SELECT
                        l.Id,
                        l.ListName,
                        l.ListDescription,
                        l.CreatedAt,
                        l.UpdatedAt,
                        COUNT(DISTINCT li.Id) AS TotalItems,
                        COUNT(DISTINCT lr.Id) AS TotalRuns,
                        MAX(lr.CreatedAt)     AS LastRun,
                        ul.IsOwner,
                        (SELECT COUNT(*) FROM UserLists ul2 WHERE ul2.ListId = l.Id) AS MemberCount,
                        (SELECT TOP 1 RTRIM(ISNULL(u2.FirstName, '') + ' ' + ISNULL(u2.LastName, ''))
                         FROM UserLists ul2
                         INNER JOIN Users u2 ON u2.Id = ul2.UserId
                         WHERE ul2.ListId = l.Id AND ul2.IsOwner = 1) AS OwnerName
                    FROM Lists l
                    INNER JOIN UserLists ul ON ul.ListId = l.Id
                    LEFT JOIN ListItems li  ON li.ListId = l.Id
                    LEFT JOIN ListRuns lr   ON lr.ListId = l.Id
                    WHERE ul.UserId = @UserId AND l.IsArchived = 0
                    GROUP BY l.Id, l.ListName, l.ListDescription, l.CreatedAt, l.UpdatedAt, ul.IsOwner
                    ORDER BY MAX(lr.CreatedAt) DESC, l.CreatedAt DESC;";

                const string activeRunsSql = @"
                    SELECT lr.ListId, MAX(lr.Id) AS ActiveRunId
                    FROM ListRuns lr
                    INNER JOIN UserLists ul ON ul.ListId = lr.ListId
                    WHERE ul.UserId = @UserId
                    AND lr.CompletedAt IS NULL
                    GROUP BY lr.ListId;";

                var lists = (await _connection.QueryAsync<List>(listsSql, new { UserId = userId })).ToList();
                var activeRuns = (await _connection.QueryAsync(activeRunsSql, new { UserId = userId })).ToList();

                var activeRunByListId = activeRuns
                    .ToDictionary(r => (int)r.ListId, r => (int)r.ActiveRunId);

                foreach (var list in lists)
                {
                    list.Items = [];
                    list.ActiveRunId = activeRunByListId.TryGetValue(list.Id, out var runId) ? runId : 0;
                }

                return Result<System.Collections.Generic.List<List>>.Ok(lists);
            }
            catch (Exception ex)
            {
                return Result<System.Collections.Generic.List<List>>.Fail(ex.Message);
            }
        }

        public async Task<Result<ListRun>> CreateListRun(int listId)
        {
            try
            {
                const string activeRunSql = @"
                    SELECT TOP 1 Id, ListId, CreatedAt
                    FROM ListRuns
                    WHERE ListId = @ListId AND CompletedAt IS NULL
                    ORDER BY Id DESC;";

                const string activeRunItemsSql = @"
                    SELECT
                        lri.Id, lri.ListRunId, lri.ListItemId,
                        lri.ListItemName, lri.ListItemDescription, lri.SortOrder,
                        lri.CompletedAt, lri.CompletedBy,
                        CASE
                            WHEN lri.CompletedBy IS NOT NULL
                            THEN LEFT(ISNULL(u.FirstName, ''), 1) + LEFT(ISNULL(u.LastName, ''), 1)
                            ELSE NULL
                        END AS CompletedByInitials,
                        CASE
                            WHEN lri.CompletedBy IS NOT NULL
                            THEN ISNULL(u.FirstName, '') + ' ' + ISNULL(u.LastName, '') + ' - ' + ISNULL(u.Email, '')
                            ELSE NULL
                        END AS CompletedByName
                    FROM ListRunItems lri
                    LEFT JOIN Users u ON u.Id = lri.CompletedBy
                    WHERE lri.ListRunId = @RunId
                    ORDER BY lri.SortOrder, lri.Id;";

                var existingRun = await _connection.QuerySingleOrDefaultAsync<ListRun>(
                    activeRunSql, new { ListId = listId });

                if (existingRun != null)
                {
                    var existingItems = await _connection.QueryAsync<ListRunItem>(
                        activeRunItemsSql, new { RunId = existingRun.Id });
                    existingRun.Items = existingItems.ToList();
                    return Result<ListRun>.Ok(existingRun);
                }

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
                insertedRun.Items = new System.Collections.Generic.List<ListRunItem>();

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

        public async Task<Result<bool>> CompleteListRun(int runId, int userId)
        {
            try
            {
                const string runExistsSql = @"
                    SELECT COUNT(1) FROM ListRuns WHERE Id = @RunId;";

                const string stampRunSql = @"
                    UPDATE ListRuns
                    SET
                        CompletedAt = GETUTCDATE(),
                        CompletedBy = @UserId
                    WHERE Id = @RunId;";

                var exists = await _connection.ExecuteScalarAsync<int>(runExistsSql, new { RunId = runId });
                if (exists == 0)
                    return Result<bool>.Fail("List run not found");

                await _connection.ExecuteAsync(stampRunSql, new { RunId = runId, UserId = userId });

                return Result<bool>.Ok(true);
            }
            catch (Exception ex)
            {
                return Result<bool>.Fail(ex.Message);
            }
        }

        public async Task<Result<bool>> SetListRunItemCompletion(int runItemId, bool isComplete, int userId)
        {
            try
            {
                const string sql = @"
                    UPDATE ListRunItems
                    SET
                        CompletedAt = CASE WHEN @IsComplete = 1 THEN GETUTCDATE() ELSE NULL END,
                        CompletedBy = CASE WHEN @IsComplete = 1 THEN @UserId ELSE NULL END
                    WHERE Id = @RunItemId;";

                var rowsAffected = await _connection.ExecuteAsync(sql, new
                {
                    RunItemId = runItemId,
                    IsComplete = isComplete,
                    UserId = userId
                });

                if (rowsAffected == 0)
                    return Result<bool>.Fail("List run item not found");

                return Result<bool>.Ok(true);
            }
            catch (Exception ex)
            {
                return Result<bool>.Fail(ex.Message);
            }
        }

        public async Task<Result<ListRun>> GetListRun(int runId)
        {
            try
            {
                const string runSql = @"
                    SELECT Id, ListId, CreatedAt, CompletedAt, CompletedBy
                    FROM ListRuns
                    WHERE Id = @RunId;";

                const string runItemsSql = @"
                    SELECT
                        lri.Id,
                        lri.ListRunId,
                        lri.ListItemId,
                        lri.ListItemName,
                        lri.ListItemDescription,
                        lri.SortOrder,
                        lri.CompletedAt,
                        lri.CompletedBy,
                        CASE
                            WHEN lri.CompletedBy IS NOT NULL
                            THEN LEFT(ISNULL(u.FirstName, ''), 1) + LEFT(ISNULL(u.LastName, ''), 1)
                            ELSE NULL
                        END AS CompletedByInitials,
                        CASE
                            WHEN lri.CompletedBy IS NOT NULL
                            THEN ISNULL(u.FirstName, '') + ' ' + ISNULL(u.LastName, '') + ' - ' + ISNULL(u.Email, '')
                            ELSE NULL
                        END AS CompletedByName
                    FROM ListRunItems lri
                    LEFT JOIN Users u ON u.Id = lri.CompletedBy
                    WHERE lri.ListRunId = @RunId
                    ORDER BY lri.SortOrder, lri.Id;";

                var run = await _connection.QuerySingleOrDefaultAsync<ListRun>(runSql, new { RunId = runId });
                if (run == null)
                    return Result<ListRun>.Fail("List run not found");

                var items = await _connection.QueryAsync<ListRunItem>(runItemsSql, new { RunId = runId });
                run.Items = items.ToList();

                return Result<ListRun>.Ok(run);
            }
            catch (Exception ex)
            {
                return Result<ListRun>.Fail(ex.Message);
            }
        }

        public async Task<Result<System.Collections.Generic.List<ListRun>>> GetListRuns(int listId)
        {
            try
            {
                const string runsSql = @"
			        SELECT
				        Id,
				        ListId,
				        CreatedAt,
				        CompletedAt,
				        CompletedBy
			        FROM ListRuns
			        WHERE ListId = @ListId
			        ORDER BY CreatedAt DESC, ID DESC;";

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
                        : new System.Collections.Generic.List<ListRunItem>();
                }

                return Result<System.Collections.Generic.List<ListRun>>.Ok(runs);
            }
            catch (Exception ex)
            {
                return Result<System.Collections.Generic.List<ListRun>>.Fail(ex.Message);
            }
        }

        public async Task<Result<System.Collections.Generic.List<ListRunHistoryModel>>> GetListRunHistory(int listId)
        {
            try
            {
                const string sql = @"
                    SELECT
                        lr.Id,
                        lr.ListId,
                        lr.CreatedAt,
                        lr.CompletedAt,
                        COUNT(lri.Id) AS TotalItems,
                        SUM(CASE WHEN lri.CompletedAt IS NOT NULL THEN 1 ELSE 0 END) AS CompletedItems
                    FROM ListRuns lr
                    LEFT JOIN ListRunItems lri ON lri.ListRunId = lr.Id
                    WHERE lr.ListId = @ListId
                    GROUP BY lr.Id, lr.ListId, lr.CreatedAt, lr.CompletedAt
                    ORDER BY lr.CreatedAt DESC, lr.Id DESC;";

                var history = await _connection.QueryAsync<ListRunHistoryModel>(sql, new { ListId = listId });
                return Result<System.Collections.Generic.List<ListRunHistoryModel>>.Ok(history.ToList());
            }
            catch (Exception ex)
            {
                return Result<System.Collections.Generic.List<ListRunHistoryModel>>.Fail(ex.Message);
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

        // ─── Shared list methods ────────────────────────────────────────────────────

        public async Task<Result<System.Collections.Generic.List<ListPendingInviteModel>>> GetPendingInvitations(int listId)
        {
            try
            {
                const string sql = @"
                    SELECT
                        InvitedEmail,
                        CASE WHEN ExpiresAt < GETUTCDATE() THEN 1 ELSE 0 END AS IsExpired
                    FROM ListInvitations
                    WHERE ListId = @ListId AND Status = 'pending'
                    ORDER BY CreatedAt DESC;";

                var invites = await _connection.QueryAsync<ListPendingInviteModel>(sql, new { ListId = listId });
                return Result<System.Collections.Generic.List<ListPendingInviteModel>>.Ok(invites.ToList());
            }
            catch (Exception ex)
            {
                return Result<System.Collections.Generic.List<ListPendingInviteModel>>.Fail(ex.Message);
            }
        }

        public async Task<Result<System.Collections.Generic.List<ListMemberModel>>> GetListMembers(int listId)
        {
            try
            {
                const string sql = @"
                    SELECT
                        u.Id AS UserId,
                        u.FirstName,
                        u.LastName,
                        u.Email,
                        ul.IsOwner
                    FROM UserLists ul
                    INNER JOIN Users u ON u.Id = ul.UserId
                    WHERE ul.ListId = @ListId
                    ORDER BY ul.IsOwner DESC, u.FirstName, u.LastName;";

                var members = await _connection.QueryAsync<ListMemberModel>(sql, new { ListId = listId });
                return Result<System.Collections.Generic.List<ListMemberModel>>.Ok(members.ToList());
            }
            catch (Exception ex)
            {
                return Result<System.Collections.Generic.List<ListMemberModel>>.Fail(ex.Message);
            }
        }

        public async Task<Result<bool>> CreateListInvitation(ListInvitation invitation)
        {
            try
            {
                const string alreadyMemberSql = @"
                    SELECT COUNT(1) FROM UserLists
                    WHERE ListId = @ListId
                    AND UserId = (SELECT Id FROM Users WHERE Email = @InvitedEmail);";

                const string deletePendingSql = @"
                    DELETE FROM ListInvitations
                    WHERE ListId = @ListId AND InvitedEmail = @InvitedEmail AND Status = 'pending';";

                const string insertSql = @"
                    INSERT INTO ListInvitations
                    (ListId, InvitedByUserId, InvitedEmail, Token, Status, CreatedAt, ExpiresAt, RequiresPremium)
                    OUTPUT INSERTED.Id
                    VALUES
                    (@ListId, @InvitedByUserId, @InvitedEmail, @Token, 'pending', GETUTCDATE(), @ExpiresAt, @RequiresPremium);";

                var alreadyMember = await _connection.ExecuteScalarAsync<int>(
                    alreadyMemberSql,
                    new { invitation.ListId, invitation.InvitedEmail }
                );

                if (alreadyMember > 0)
                    return Result<bool>.Fail("This person is already a member of the list.");

                using var transaction = _connection.BeginTransaction();

                await _connection.ExecuteAsync(
                    deletePendingSql,
                    new { invitation.ListId, invitation.InvitedEmail },
                    transaction
                );

                await _connection.ExecuteScalarAsync<int>(
                    insertSql,
                    new
                    {
                        invitation.ListId,
                        invitation.InvitedByUserId,
                        invitation.InvitedEmail,
                        invitation.Token,
                        invitation.ExpiresAt,
                        invitation.RequiresPremium
                    },
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

        public async Task<Result<ListInvitation>> GetListInvitation(string token)
        {
            try
            {
                const string sql = @"
                    SELECT
                        li.Id,
                        li.ListId,
                        li.InvitedByUserId,
                        li.InvitedEmail,
                        li.Token,
                        li.Status,
                        li.CreatedAt,
                        li.ExpiresAt,
                        li.AcceptedByUserId,
                        l.ListName,
                        u.FirstName AS InvitedByFirstName,
                        u.LastName  AS InvitedByLastName,
                        CASE WHEN EXISTS (SELECT 1 FROM Users WHERE LOWER(Email) = LOWER(li.InvitedEmail))
                             THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END AS HasAccount
                    FROM ListInvitations li
                    INNER JOIN Lists l ON l.Id = li.ListId
                    INNER JOIN Users u ON u.Id = li.InvitedByUserId
                    WHERE li.Token = @Token;";

                var invitation = await _connection.QuerySingleOrDefaultAsync<ListInvitation>(sql, new { Token = token });
                if (invitation == null)
                    return Result<ListInvitation>.Fail("Invitation not found.");

                return Result<ListInvitation>.Ok(invitation);
            }
            catch (Exception ex)
            {
                return Result<ListInvitation>.Fail(ex.Message);
            }
        }

        public async Task<Result<bool>> AcceptListInvitation(string token, int userId)
        {
            try
            {
                const string getInviteSql = @"
                    SELECT li.Id, li.ListId, li.Status, li.ExpiresAt
                    FROM ListInvitations li
                    WHERE li.Token = @Token;";

                const string alreadyMemberSql = @"
                    SELECT COUNT(1) FROM UserLists WHERE ListId = @ListId AND UserId = @UserId;";

                const string updateInviteSql = @"
                    UPDATE ListInvitations
                    SET Status = 'accepted', AcceptedByUserId = @UserId
                    WHERE Token = @Token;";

                const string insertMemberSql = @"
                    INSERT INTO UserLists (UserId, ListId, IsOwner)
                    VALUES (@UserId, @ListId, 0);";

                var invite = await _connection.QuerySingleOrDefaultAsync(getInviteSql, new { Token = token });
                if (invite == null)
                    return Result<bool>.Fail("Invitation not found.");

                if ((string)invite.Status == "accepted")
                    return Result<bool>.Fail("This invitation has already been used.");

                if ((DateTime)invite.ExpiresAt < DateTime.UtcNow)
                    return Result<bool>.Fail("This invitation has expired.");

                int listId = (int)invite.ListId;

                var alreadyMember = await _connection.ExecuteScalarAsync<int>(
                    alreadyMemberSql,
                    new { ListId = listId, UserId = userId }
                );

                using var transaction = _connection.BeginTransaction();

                await _connection.ExecuteAsync(updateInviteSql, new { Token = token, UserId = userId }, transaction);

                if (alreadyMember == 0)
                {
                    await _connection.ExecuteAsync(insertMemberSql, new { UserId = userId, ListId = listId }, transaction);
                }

                transaction.Commit();

                return Result<bool>.Ok(true);
            }
            catch (Exception ex)
            {
                return Result<bool>.Fail(ex.Message);
            }
        }

        public async Task<Result<bool>> RemoveListMember(int listId, int requestingUserId, int memberUserId)
        {
            try
            {
                const string ownerCheckSql = @"
                    SELECT IsOwner FROM UserLists WHERE ListId = @ListId AND UserId = @UserId;";

                const string deleteMemberSql = @"
                    DELETE FROM UserLists
                    WHERE ListId = @ListId AND UserId = @MemberUserId AND IsOwner = 0;";

                var isOwner = await _connection.ExecuteScalarAsync<bool?>(
                    ownerCheckSql,
                    new { ListId = listId, UserId = requestingUserId }
                );

                if (isOwner == null || !isOwner.Value)
                    return Result<bool>.Fail("Only the list owner can remove members.");

                var rows = await _connection.ExecuteAsync(
                    deleteMemberSql,
                    new { ListId = listId, MemberUserId = memberUserId }
                );

                if (rows == 0)
                    return Result<bool>.Fail("Member not found or is the list owner.");

                return Result<bool>.Ok(true);
            }
            catch (Exception ex)
            {
                return Result<bool>.Fail(ex.Message);
            }
        }

        public async Task<Result<bool>> LeaveList(int listId, int userId)
        {
            try
            {
                const string checkSql = @"
                    SELECT IsOwner FROM UserLists WHERE ListId = @ListId AND UserId = @UserId;";

                const string deleteSql = @"
                    DELETE FROM UserLists WHERE ListId = @ListId AND UserId = @UserId AND IsOwner = 0;";

                var isOwner = await _connection.ExecuteScalarAsync<bool?>(
                    checkSql,
                    new { ListId = listId, UserId = userId }
                );

                if (isOwner == null)
                    return Result<bool>.Fail("You are not a member of this list.");

                if (isOwner.Value)
                    return Result<bool>.Fail("You are the list owner. Delete the list instead.");

                await _connection.ExecuteAsync(deleteSql, new { ListId = listId, UserId = userId });

                return Result<bool>.Ok(true);
            }
            catch (Exception ex)
            {
                return Result<bool>.Fail(ex.Message);
            }
        }
    }
}
