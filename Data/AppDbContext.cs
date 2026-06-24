using Microsoft.EntityFrameworkCore;
using SubmissionProcessor.Worker.Models;

namespace SubmissionProcessor.Worker.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<SubmissionFile> SubmissionFiles { get; set; }

    public DbSet<ProcessingJob> ProcessingJobs { get; set; }
}
