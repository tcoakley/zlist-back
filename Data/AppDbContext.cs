using Microsoft.EntityFrameworkCore;
using zListBack.Models;
using zListBack.Models;

namespace zListBack.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<List> Lists { get; set; }
        public DbSet<ListItem> ListItems { get; set; }
        public DbSet<UserList> UserLists { get; set; }
        public DbSet<ListRun> ListRuns { get; set; }
        public DbSet<ListRunItem> ListRunItems { get; set; }
        public DbSet<RefreshToken> RefreshTokens { get; set; }

    }
}
