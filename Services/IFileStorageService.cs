namespace SubmissionProcessor.Worker.Services
{
    public interface IFileStorageService
    {
        Task<FileStream> OpenReadAsync(int Id);
        Task<bool> ExistsAsync(string path);
        Task<bool> DeleteAsync(int Id);
        string GetFullPath(string relativePath);
    }
}
