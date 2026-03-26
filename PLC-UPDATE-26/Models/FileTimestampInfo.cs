namespace PLC_UPDATE_26.Models;

public class FileTimestampInfo
{
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public DateTime LastModifiedUtc { get; set; }
    public string Sha256Hash { get; set; } = string.Empty;
    public string HostName { get; set; } = string.Empty;
    public DateTime RepositoryLastUpdated { get; set; }

    public bool IsNewer(FileTimestampInfo other)
    {
        if (other == null)
            return true;

        return LastModifiedUtc > other.LastModifiedUtc;
    }

    public override string ToString()
    {
        return $"{FileName}|{FilePath}|{FileSize}|{LastModifiedUtc:O}|{Sha256Hash}|{HostName}|{RepositoryLastUpdated:O}";
    }

    public static FileTimestampInfo? FromString(string data)
    {
        try
        {
            var parts = data.Split('|');
            if (parts.Length != 7)
                return null;

            return new FileTimestampInfo
            {
                FileName = parts[0],
                FilePath = parts[1],
                FileSize = long.Parse(parts[2]),
                LastModifiedUtc = DateTime.ParseExact(parts[3], "O", null),
                Sha256Hash = parts[4],
                HostName = parts[5],
                RepositoryLastUpdated = DateTime.ParseExact(parts[6], "O", null)
            };
        }
        catch
        {
            return null;
        }
    }
}
