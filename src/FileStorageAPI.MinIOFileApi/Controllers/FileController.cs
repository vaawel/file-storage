using Asp.Versioning;
using FileStorageAPI.MinIOFileApi.Configs;
using FileStorageAPI.MinIOFileApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;

namespace FileStorageAPI.MinIOFileApi.Controllers;

[ApiVersion(1.0)]
[Route("v{version:apiVersion}/[controller]")]
[ApiController]
public class FileController(IMinioClient minioClient, IOptions<MinIOOptions> options) : ControllerBase
{
    private readonly IMinioClient _minioClient = minioClient ?? throw new ArgumentNullException(nameof(minioClient));
    private readonly string _defaultBucketName = options.Value.BucketName ?? throw new ArgumentNullException(nameof(options.Value.BucketName));

    [HttpGet("{fileName}")]
    public async Task<IActionResult> DownloadFile(string fileName, [FromQuery] string? bucketName)
    {
        bucketName ??= _defaultBucketName;
        var memoryStream = new MemoryStream();

        try
        {
            var objectStat = await _minioClient.StatObjectAsync(new StatObjectArgs()
                .WithBucket(bucketName)
                .WithObject(fileName)
            );
            if (objectStat == null || objectStat.DeleteMarker)
                return NotFound(new { message = "Object not found or deleted" });

            await _minioClient.GetObjectAsync(new GetObjectArgs()
                .WithBucket(bucketName)
                .WithObject(fileName)
                .WithCallbackStream(stream =>
                {
                    stream.CopyTo(memoryStream);
                })
            );

            memoryStream.Seek(0, SeekOrigin.Begin);
            var fileBytes = memoryStream.ToArray();
            var base64String = Convert.ToBase64String(fileBytes);

            return Ok(new GetObjectReply
            {
                ByteArray = fileBytes,
                ObjectStat = objectStat,
                Base64String = base64String
            });
        }
        catch (MinioException minioEx)
        {
            return StatusCode(500, new { error = minioEx.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("upload")]
    public async Task<IActionResult> UploadFile(IFormFile file, [FromQuery] string? bucketName, [FromQuery] string? path)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "File not selected." });

        bucketName ??= _defaultBucketName;
        var fileName = file.FileName;
        var objectName = string.IsNullOrEmpty(path) ? fileName : Path.Combine(path, fileName).Replace("\\", "/");

        try
        {
            await using var stream = file.OpenReadStream();
            var putObjectArgs = new PutObjectArgs()
                .WithBucket(bucketName)
                .WithObject(objectName)
                .WithStreamData(stream)
                .WithObjectSize(stream.Length)
                .WithContentType(file.ContentType);

            await _minioClient.PutObjectAsync(putObjectArgs);
            return Ok(new { File = $"{bucketName}/{objectName}" });
        }
        catch (MinioException minioEx)
        {
            return StatusCode(500, new { error = minioEx.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpDelete("{fileName}")]
    public async Task<IActionResult> DeleteFile(string fileName, [FromQuery] string? bucketName)
    {
        bucketName ??= _defaultBucketName;
        
        try
        {
            var removeObjectArgs = new RemoveObjectArgs()
                .WithBucket(bucketName)
                .WithObject(fileName);

            await _minioClient.RemoveObjectAsync(removeObjectArgs);
            return NoContent();
        }
        catch (MinioException minioEx)
        {
            return StatusCode(500, new { error = minioEx.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
