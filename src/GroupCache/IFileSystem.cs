// --------------------------------------------------------------------------------------------------------------------
// <copyright file="IFileSystem.cs" company="Microsoft Corporation">
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

    public interface IFileSystem
    {
        Stream FileOpenRead(string path);

        void FileDelete(string sourcePath, string tmpPath);

        Task<string> WriteAtomicAsync(Func<Stream, CancellationToken, Task> copyAsync, string tmpPath, CancellationToken ct);

        string[] DirectoryGetFiles(string directoryPath);

        void DirectoryCreate(string directoryPath);

        void DirectoryReCreate(string directoryPath);
    }
}
