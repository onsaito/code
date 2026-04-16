using Microsoft.EntityFrameworkCore;

namespace FileUploadPOC.Models
{
    public class AppDbContext : DbContext
    {
        AppDbContext(DbContextOptions<AppDbContext> options) : base(options){ }
        public DbSet<UploadedFile> UploadedFiles { get; set; }
    }
}
