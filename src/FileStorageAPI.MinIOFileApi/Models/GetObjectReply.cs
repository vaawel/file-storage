using Minio.DataModel;

namespace FileStorageAPI.MinIOFileApi.Models;

public class GetObjectReply
{
    public ObjectStat ObjectStat { get; set; }
    public byte[] ByteArray { get; set; }
    public string Base64String { get; set; }
}