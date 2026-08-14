// --------------------------------------------------------------------------------------------------------------------
// <copyright file="FileSystem.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace GroupCache
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    public class FileSystem : IFileSystem
    {
        /// <inheritdoc/>
        public Stream FileOpenRead(string path)
        {
            return File.OpenRead(path);
        }

        /// <inheritdoc/>
        public void FileDelete(string sourcePath, string tmpPath)
        {
            File.Delete(sourcePath);
        }

        public bool FileExists(string path)
        {
            return File.Exists(path);
        }

        private string GenerateRandomFilePath(string tmpPath)
        {
            string filePath;
            do
            {
                filePath = Path.Combine(tmpPath, Path.GetRandomFileName());
            }
            while (File.Exists(filePath));
            return filePath;
        }

        /// <summary>
        /// 1- create a new file at tmpPath;
        /// 2- send data to the operating system kernel for writing to tmpPath;
        /// 3- close the file;
        /// 4- flush the writing of data to tmpPath;
        /// 5- rename tmpPath on top of path.
        /// </summary>
        /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
        public async Task<string> WriteAtomicAsync(Func<Stream, CancellationToken, Task> copyAsync, string tmpPath, CancellationToken ct)
        {
            string randomPath = this.GenerateRandomFilePath(tmpPath);
            bool created = false;
            try
            {
                await using (var fileStream = new FileStream(
                    randomPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 81920,
                    FileOptions.Asynchronous | FileOptions.SequentialScan))
                {
                    created = true;
                    await copyAsync(fileStream, ct).ConfigureAwait(false);
                    await fileStream.FlushAsync(ct).ConfigureAwait(false);
                }

                return randomPath;
            }
            catch
            {
                if (created)
                {
                    File.Delete(randomPath);
                }

                throw;
            }
        }

        /// <inheritdoc/>
        public string[] DirectoryGetFiles(string directoryPath)
        {
            return Directory.GetFiles(directoryPath);
        }

        /// <inheritdoc/>
        public void DirectoryCreate(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }
        }

        /// <inheritdoc/>
        public void DirectoryReCreate(string directoryPath)
        {
            if (Directory.Exists(directoryPath))
            {
                // discard all entry
                Directory.Delete(directoryPath, true);
            }

            Directory.CreateDirectory(directoryPath);
        }
    }
}
