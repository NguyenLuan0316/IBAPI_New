
namespace IBAPI.ExecuteMilestone.Model
{
    public class FileMinIOResponseDto
    {
        public string Key { get; set; }
        public string Extension { get; set; }
        public string FilePath { get; set; }
        public long Size { get; set; }
        public string OriginalFilename { get; set; }
    }
}