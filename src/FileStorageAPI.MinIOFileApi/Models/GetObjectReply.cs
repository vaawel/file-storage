using Minio.DataModel;

namespace FileStorageAPI.MinIOFileApi.Models;

public class GetObjectReply
{
    public ObjectStat ObjectStat { get; set; }
    public byte[] Data { get; set; }
}