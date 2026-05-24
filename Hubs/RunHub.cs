using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using System.Security.Claims;

namespace zListBack.Hubs
{
    [Authorize]
    public class RunHub : Hub
    {
        private static readonly ConcurrentDictionary<int, string> _userInitials = new();
        private static readonly ConcurrentDictionary<int, string> _userDisplayNames = new();

        public async Task JoinRun(int runId, string initials, string displayName)
        {
            var userId = GetUserId();
            if (userId > 0)
            {
                _userInitials[userId] = initials;
                _userDisplayNames[userId] = displayName;
            }
            await Groups.AddToGroupAsync(Context.ConnectionId, $"run-{runId}");
        }

        public async Task LeaveRun(int runId) =>
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"run-{runId}");

        public static string GetUserInitials(int userId) =>
            _userInitials.TryGetValue(userId, out var i) ? i : string.Empty;

        public static string GetUserDisplayName(int userId) =>
            _userDisplayNames.TryGetValue(userId, out var n) ? n : string.Empty;

        private int GetUserId()
        {
            var claim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(claim, out var id) ? id : 0;
        }
    }
}
