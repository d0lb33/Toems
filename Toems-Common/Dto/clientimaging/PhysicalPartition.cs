namespace Toems_Common.Dto.clientimaging
{
    public class PhysicalPartition
    {
        public string Compression { get; set; }
        public string EfiBootLoader { get; set; }
        public string FileSystem { get; set; }
        public string Guid { get; set; }
        public string ImageType { get; set; }
        public string Number { get; set; }
        public string PartcloneFileSystem { get; set; }
        public string Prefix { get; set; }
        public string Type { get; set; }
        public string Uuid { get; set; }
        public VolumeGroup VolumeGroup { get; set; }
    }
}