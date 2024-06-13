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
    private readonly string _defaultBucketName = options.Value.BucketName;

    [HttpGet("{fileName}")]
    public async Task<GetObjectReply> DownloadFile(string fileName, [FromQuery] string? bucketName)
    {
        bucketName ??= _defaultBucketName;
        var memoryStream = new MemoryStream();

        try
        {
            var objectStat= await minioClient.StatObjectAsync(new StatObjectArgs()
                .WithBucket(bucketName)
                .WithObject(fileName)
            );
            if (objectStat == null || objectStat.DeleteMarker)
                throw new Exception("Object Not Found or Deleted");
            await minioClient.GetObjectAsync(new GetObjectArgs()
                .WithBucket(bucketName)
                .WithObject(fileName)
                .WithCallbackStream(stream =>
                {
                    stream.CopyTo(memoryStream);
                })
            );

            memoryStream.Seek(0, SeekOrigin.Begin);
            return await Task.FromResult(new GetObjectReply()
            {
                Data = memoryStream.ToArray(),
                ObjectStat = objectStat
            });
        }
        catch (Exception ex)
        {
            throw new InternalServerException(ex.Message);
        }
    }

    [HttpPost("upload")]
    public async Task<IActionResult> UploadFile(IFormFile file, [FromQuery] string? bucketName, [FromQuery] string? path)
    {
        if (file.Length == 0)
            return BadRequest("File not selected.");

        bucketName ??= _defaultBucketName;
        var fileName = file.FileName;
        var objectName = string.IsNullOrEmpty(path) ? fileName : Path.Combine(path, fileName).Replace("\\", "/");
        try
        {
            await using (var stream = file.OpenReadStream())
            {
                PutObjectArgs putObjectArgs = new PutObjectArgs()
                    .WithBucket(bucketName)
                    .WithObject(objectName)
                    .WithStreamData(stream)
                    .WithObjectSize(stream.Length)
                    .WithContentType(file.ContentType);
                await minioClient.PutObjectAsync(putObjectArgs);
            }
            return Ok(new { FileName = fileName, BucketName = bucketName });
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
            RemoveObjectArgs removeObjectArgs = new RemoveObjectArgs()
                .WithBucket(bucketName)
                .WithObject(fileName);
            await minioClient.RemoveObjectAsync(removeObjectArgs);
            return NoContent();
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
}