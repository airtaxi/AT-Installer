﻿using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstallerCommons.ZipHelper
{
    public static class ZipFileWithProgress
    {
        public static void CreateFromDirectory(string sourceDirectoryName, string destinationArchiveFileName, CompressionLevel compressionLevel = CompressionLevel.Optimal, IProgress<ZipProgressStatus> progress = null)
        {
            sourceDirectoryName = Path.GetFullPath(sourceDirectoryName);

            var sourceFiles = new DirectoryInfo(sourceDirectoryName).GetFiles("*", SearchOption.AllDirectories);
            double totalBytes = sourceFiles.Sum(f => f.Length);
            long currentBytes = 0;

            using var archive = ZipFile.Open(destinationArchiveFileName, ZipArchiveMode.Create, Encoding.UTF8);
            foreach (var file in sourceFiles)
            {
                var entryName = file.FullName[(sourceDirectoryName.Length + 1)..];
                var entry = archive.CreateEntry(entryName);

                entry.LastWriteTime = file.LastWriteTime;

                using var inputStream = File.OpenRead(file.FullName);
                using var outputStream = entry.Open();
                var progressStream = new StreamWithProgress(inputStream, new ActionProgress<int>(i =>
                {
                    currentBytes += i;
                    progress?.Report(new(currentBytes / totalBytes, file.Name));
                }), null);

                progressStream.CopyTo(outputStream);
            }
        }

        public static void ExtractToDirectory(string sourceArchiveFileName, string destinationDirectoryName, IProgress<ZipProgressStatus> progress = null)
        {
            using var archive = ZipFile.Open(sourceArchiveFileName, ZipArchiveMode.Read, Encoding.UTF8);
            double totalBytes = archive.Entries.Sum(e => e.Length);
            long currentBytes = 0;

            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                var fileName = Path.Combine(destinationDirectoryName, entry.FullName);

                var directory = Path.GetDirectoryName(fileName);
                Directory.CreateDirectory(directory);

                if (fileName.EndsWith('\\') || fileName.EndsWith('/')) continue;
                using var inputStream = entry.Open();
                using var outputStream = File.Create(fileName);
                var progressStream = new StreamWithProgress(outputStream, null, new ActionProgress<int>(i =>
                {
                    currentBytes += i;
                    progress?.Report(new(currentBytes / totalBytes, entry.Name));
                }));

                inputStream.CopyTo(progressStream);

                File.SetLastWriteTime(fileName, entry.LastWriteTime.LocalDateTime);
            }
        }
    }
}
