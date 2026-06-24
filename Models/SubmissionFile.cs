using System.ComponentModel.DataAnnotations;

namespace SubmissionProcessor.Worker.Models;

public class SubmissionFile
{
    [Key]
    public int Id { get; set; }

    public int SubmissionId { get; set; }

    public string OriginalFileName { get; set; } = null!;

    public string StorageFileName { get; set; } = null!;

    public string ContentType { get; set; } = null!;

    public long SizeBytes { get; set; }

    public string Checksum { get; set; } = null!;

    public int UploadedBy { get; set; }

    public DateTime TimeStamp { get; set; }
}
