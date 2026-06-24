using System.ComponentModel.DataAnnotations;

namespace SubmissionProcessor.Worker.Models;

public class ProcessingJob
{
    [Key]
    public int Id { get; set; }

    public string CorrelationId { get; set; } = null!;

    public string Status { get; set; } = null!;

    public int Attempts { get; set; }

    public string ErrorSummary { get; set; } = "";

    public DateTime StartedTime { get; set; }

    public DateTime CompletedTime { get; set; }
}
