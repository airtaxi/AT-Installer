using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.Zip;

namespace Installer.Helper;

public class ZipHelper
{
	public static void ExtractZipFile(string sourceZipFilePath, string destinationDirectory, Action<double> progressCallback)
	{
		using var fs = File.OpenRead(sourceZipFilePath);
		using var zf = new ZipFile(fs);
		zf.IsStreamOwner = false;

		long totalBytes = CountEntriesSize(zf);
		long totalBytesExtracted = 0;

		foreach (ZipEntry zipEntry in zf)
		{
			if (!zipEntry.IsFile) continue;

			string entryFileName = zipEntry.Name;
			byte[] buffer = new byte[4096];

			string fullZipToPath = Path.Combine(destinationDirectory, entryFileName);

			string directoryName = Path.GetDirectoryName(fullZipToPath);
			if (directoryName.Length > 0) Directory.CreateDirectory(directoryName);

			using var streamWriter = File.Create(fullZipToPath);
			StreamUtils.Copy(zf.GetInputStream(zipEntry), streamWriter, buffer);

			totalBytesExtracted += zipEntry.Size;
			double progressPercentage = (double)totalBytesExtracted / totalBytes * 100;
			progressCallback?.Invoke(progressPercentage);
		}
	}

	private static long CountEntriesSize(ZipFile zipFile)
	{
		long totalSize = 0;
		foreach (ZipEntry entry in zipFile)
		{
			if (entry.IsFile)
			{
				totalSize += entry.Size;
			}
		}
		return totalSize;
	}
}
