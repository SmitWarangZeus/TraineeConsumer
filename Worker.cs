using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using SubmissionProcessor.Worker.Models;
using SubmissionProcessor.Worker.DTOs;
using System.Text.Json;
using SubmissionProcessor.Worker.Data;
using TraineeManagement.api.Exceptions;
using SubmissionProcessor.Worker.Exceptions;
using Microsoft.EntityFrameworkCore;
using SubmissionProcessor.Worker.Services;
using System.Security.Cryptography;

namespace SubmissionProcessor.Worker;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;

    private IConnection _connection;
    
    private IChannel _channel;

    private readonly IServiceScopeFactory _serviceScopeFactory;

    private const string QueueName = "file_processing_queue";

    private const string DlxExchange = "submissions.dlx";

    private const string DlqQueue = "submissions.dlq";

    private const string DlxRoutingKey = "submission.failed";

    public Worker(ILogger<Worker> logger, IServiceScopeFactory serviceScopeFactory)
    {
        _logger = logger;

        var factory = new ConnectionFactory()
        {
            HostName = Environment.GetEnvironmentVariable("FACTORY_HOSTNAME")!,
            UserName = Environment.GetEnvironmentVariable("FACTORY_USERNAME")!,
            Password = Environment.GetEnvironmentVariable("FACTORY_PASSWORD")!
        };

        _connection = factory.CreateConnectionAsync().GetAwaiter().GetResult();
        _channel = _connection.CreateChannelAsync().GetAwaiter().GetResult();

        _serviceScopeFactory = serviceScopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _channel.ExchangeDeclareAsync(QueueName, ExchangeType.Topic, durable: true);
        await _channel.ExchangeDeclareAsync(DlxExchange, ExchangeType.Headers, durable: true);

        await _channel.QueueDeclareAsync(DlqQueue, durable: true, exclusive: false, autoDelete: false);
        await _channel.QueueBindAsync(DlqQueue, DlxExchange, routingKey: DlxRoutingKey);

        IDictionary<string, object?> queueArguments = new Dictionary<string, object?>()
        {
            {"x-dead-letter-exchange", DlxExchange},
            {"x-dead-letter-routing-key", DlxRoutingKey}
        };

        await _channel.QueueDeclareAsync(QueueName, durable: true, autoDelete: false, exclusive: false, arguments: queueArguments);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (model, ea) =>
        {
            bool retry = false;
            var body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);
            _logger.LogInformation("Received: {Message}", message);
            try
            {
                await using var scope = _serviceScopeFactory.CreateAsyncScope();
                IFileStorageService _service = scope.ServiceProvider.GetRequiredService<IFileStorageService>();
                AppDbContext _appDbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                SubmissionProcessingMessage? submissionProcessingMessage = JsonSerializer.Deserialize<SubmissionProcessingMessage>(message)
                ?? throw new BadRequestException("Invalid message");

                ProcessingJob? processingJob = await _appDbContext.ProcessingJobs.FirstOrDefaultAsync(s => s.CorrelationId==submissionProcessingMessage.CorrelationId)
                ?? throw new NotFoundException($"ProcessingJob with CorrelationId {submissionProcessingMessage.CorrelationId} was not found");
                if (processingJob.Attempts>2)
                {
                    processingJob.Status = "Failed";
                    await _appDbContext.SaveChangesAsync();
                    await _channel.BasicNackAsync(deliveryTag: ea.DeliveryTag, multiple: false, requeue: false);
                    return;
                }
                processingJob.Status = "Processing";
                processingJob.Attempts++;
                processingJob.StartedTime = DateTime.UtcNow;
                await _appDbContext.SaveChangesAsync();

                SubmissionFile? submissionFile = await _appDbContext.SubmissionFiles.FirstOrDefaultAsync(s => s.Id==submissionProcessingMessage.FileId);
                if (submissionFile==null)
                {
                    processingJob.Status = "Failed";
                    processingJob.CompletedTime = DateTime.UtcNow;
                    await _appDbContext.SaveChangesAsync();
                    throw new NotFoundException($"Metadata with Id {submissionProcessingMessage.FileId} was not found");
                }
                if (!await _service.ExistsAsync(submissionFile.StorageFileName))
                {
                    processingJob.Status = "Failed";
                    processingJob.CompletedTime = DateTime.UtcNow;
                    await _appDbContext.SaveChangesAsync();
                    throw new NotFoundException($"File {submissionFile.StorageFileName} not found");
                }
                string fullPath = _service.GetFullPath(submissionFile.StorageFileName);
                FileStream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var sha256 = SHA256.Create();
                var checksum = BitConverter.ToString(sha256.ComputeHash(stream)).Replace("-", "").ToLower();
                if (checksum!=submissionFile.Checksum)
                {
                    processingJob.Status = "Queued";
                    processingJob.CompletedTime = DateTime.UtcNow;
                    retry = true;
                    throw new BadRequestException("Checksum mismatch");
                }
                processingJob.Status = "Completed";
                processingJob.CompletedTime = DateTime.UtcNow;
                await _appDbContext.SaveChangesAsync();
            } catch (Exception e)
            {
                Console.WriteLine(e);
            } finally
            {
                if (retry==true)
                {
                    await _channel.BasicNackAsync(deliveryTag: ea.DeliveryTag, multiple: false, requeue: true);
                }   
            }
            await _channel.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false);
        };
        await _channel.BasicConsumeAsync(queue: "file_processing_queue", autoAck: false, consumer: consumer);
    }

    public override void Dispose()
    {
        _channel.Dispose();
        _connection.Dispose();
        base.Dispose();
    }
}
