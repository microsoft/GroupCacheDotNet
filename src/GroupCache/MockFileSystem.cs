// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MockFileSystem.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace GroupCache
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    public class MockFileSystem : IFileSystem
    {
        private readonly Dictionary<string, Dictionary<string, byte[]>> dirs = new Dictionary<string, Dictionary<string, byte[]>>();
        private readonly object @lock = new object();

        /// <inheritdoc/>
        public void DirectoryCreate(string directoryPath)
        {
            lock (this.@lock)
            {
                if (!this.dirs.ContainsKey(directoryPath))
                {
                    this.dirs.Add(directoryPath, new Dictionary<string, byte[]>());
                }
            }
        }

        /// <inheritdoc/>
        public string[] DirectoryGetFiles(string directoryPath)
        {
            lock (this.@lock)
            {
                return this.dirs[directoryPath].Keys.ToArray();
            }
        }

        /// <inheritdoc/>
        public void DirectoryReCreate(string directoryPath)
        {
            lock (this.@lock)
            {
                this.dirs.Remove(directoryPath);
                this.dirs.Add(directoryPath, new Dictionary<string, byte[]>());
            }
        }

        /// <inheritdoc/>
        public void FileDelete(string sourcePath, string tmpPath)
        {
            lock (this.@lock)
            {
                var directory = Path.GetDirectoryName(sourcePath);
                var file = Path.GetFileName(sourcePath);
                var dir = this.dirs[directory];
                Debug.Assert(dir.ContainsKey(file));
                dir.Remove(file);
            }
        }

        /// <inheritdoc/>
        public Stream FileOpenRead(string path)
        {
            lock (this.@lock)
            {
                var srcFileName = Path.GetFileName(path);
                var srcDir = Path.GetDirectoryName(path);
                return new MemoryStream(this.dirs[srcDir][srcFileName]);
            }
        }

        /// <inheritdoc/>
        public async Task<string> WriteAtomicAsync(Func<Stream, CancellationToken, Task> copyAsync, string tmpPath, CancellationToken ct)
        {
            using (var memStream = new MemoryStream())
            {
                await copyAsync(memStream, ct).ConfigureAwait(false);
                var array = memStream.ToArray();
                lock (this.@lock)
                {
                    var randomPath = this.GenerateRandomFilePath(tmpPath);
                    var srcFileName = Path.GetFileName(randomPath);
                    var srcDir = Path.GetDirectoryName(randomPath);
                    this.dirs[srcDir].Add(srcFileName, array);
                    return randomPath;
                }
            }
        }

        private bool Exists(string path)
        {
            lock (this.@lock)
            {
                var srcFileName = Path.GetFileName(path);
                var srcDir = Path.GetDirectoryName(path);
                return this.dirs[srcDir].ContainsKey(srcFileName);
            }
        }

        private string GenerateRandomFilePath(string tmpPath)
        {
            string filePath;
            do
            {
                filePath = Path.Combine(tmpPath, Guid.NewGuid().ToString());
            }
            while (this.Exists(filePath));
            return filePath;
        }
    }
}
