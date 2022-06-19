namespace IntelOrca.OpenLauncher.Core
{
    public struct DownloadProgressReport
    {
        public string Status { get; }
        public float? Value { get; }

        public DownloadProgressReport(string status, float? value)
        {
            Status = status;
            Value = value;
        }
    }
}
