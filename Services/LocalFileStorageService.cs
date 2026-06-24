using SubmissionProcessor.Worker.Data;
using SubmissionProcessor.Worker.Models;
using SubmissionProcessor.Worker.Exceptions;

namespace SubmissionProcessor.Worker.Services
{
    public class LocalFileStorageService : IFileStorageService
    {
        private readonly IConfiguration _config;

        private readonly string _baseDirectory;

        private readonly AppDbContext _appDbContext;

        public LocalFileStorageService(AppDbContext appDbContext, IConfiguration config)
        {
            _appDbContext = appDbContext;
            _config = config;
            var baseDirectory = _config.GetSection("StorageRoot");
            _baseDirectory = Path.GetFullPath(baseDirectory["StoragePath"]!);
            Directory.CreateDirectory(_baseDirectory);
        }

        public async Task<FileStream> OpenReadAsync(int Id)
        {
            SubmissionFile submissionFile = await _appDbContext.SubmissionFiles.FindAsync(Id) ?? throw new NotFoundException($"Submission file with id {Id} was not found");
            if (!await ExistsAsync(submissionFile.StorageFileName))
            {
                throw new FileNotFoundException("File not found", submissionFile.StorageFileName);
            }

            string fullPath = GetFullPath(submissionFile.StorageFileName);
            return new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        }

        public async Task<bool> ExistsAsync(string path)
        {
            string fullPath = GetFullPath(path);
            return File.Exists(fullPath);
        }

        public async Task<bool> DeleteAsync(int Id)
        {
            SubmissionFile submissionFile = await _appDbContext.SubmissionFiles.FindAsync(Id) ?? throw new NotFoundException($"Submission file with id {Id} was not found");
            string fullPath = GetFullPath(submissionFile.StorageFileName);
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
            return true;
        }

        public string GetFullPath(string relativePath)
        {
            string combined = Path.Combine(_baseDirectory, relativePath);
            string fullPath = Path.GetFullPath(combined);

            if (!fullPath.StartsWith(_baseDirectory, StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException("Access outside base directory is not allowed.");
            }

            return fullPath;
        }
    }
}
