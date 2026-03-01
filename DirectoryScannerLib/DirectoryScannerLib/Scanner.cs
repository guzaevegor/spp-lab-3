using System;
using System.IO;
using System.Threading;

namespace DirectoryScannerLib
{
    public class Scanner
    {
        private int _activeTasksCount;
        private readonly object _lockObj = new object();
        private readonly SemaphoreSlim _semaphore;

        public Scanner(int maxThreads = 10)
        {
            _semaphore = new SemaphoreSlim(maxThreads, maxThreads);
        }

        public DirectoryNode Scan(string rootPath, CancellationToken token)
        {
            var rootDirInfo = new DirectoryInfo(rootPath);
            var rootNode = new DirectoryNode
            {
                Name = rootDirInfo.Name,
                FullPath = rootDirInfo.FullName,
                IsDirectory = true
            };

            _activeTasksCount = 0;

            Interlocked.Increment(ref _activeTasksCount);
            ThreadPool.QueueUserWorkItem(_ => ProcessDirectory(rootNode, token));

            lock (_lockObj)
            {
                while (_activeTasksCount > 0)
                {
                    Monitor.Wait(_lockObj);
                }
            }

            CalculateSizes(rootNode);
            CalculatePercentages(rootNode, rootNode.Size);

            return rootNode;
        }

        private void ProcessDirectory(DirectoryNode node, CancellationToken token)
        {
            try
            {
                if (token.IsCancellationRequested) return;

                _semaphore.Wait(token);
                var dirInfo = new DirectoryInfo(node.FullPath);

                foreach (var info in dirInfo.EnumerateFileSystemInfos())
                {
                    if (token.IsCancellationRequested) break;

                    // Проверка на символическую ссылку для .NET Framework 4.7.2
                    if (info.Attributes.HasFlag(FileAttributes.ReparsePoint)) continue;

                    if (info is FileInfo fileInfo)
                    {
                        node.Children.Add(new DirectoryNode
                        {
                            Name = fileInfo.Name,
                            FullPath = fileInfo.FullName,
                            Size = fileInfo.Length,
                            IsDirectory = false
                        });
                    }
                    else if (info is DirectoryInfo subDirInfo)
                    {
                        var subNode = new DirectoryNode
                        {
                            Name = subDirInfo.Name,
                            FullPath = subDirInfo.FullName,
                            IsDirectory = true
                        };
                        node.Children.Add(subNode);

                        Interlocked.Increment(ref _activeTasksCount);
                        ThreadPool.QueueUserWorkItem(_ => ProcessDirectory(subNode, token));
                    }
                }
            }
            catch (UnauthorizedAccessException) { /* Игнорируем папки без доступа */ }
            catch (OperationCanceledException) { /* Игнорируем отмену */ }
            catch (DirectoryNotFoundException) { /* Игнорируем удаленные папки */ }
            finally
            {
               
                _semaphore.Release();

                if (Interlocked.Decrement(ref _activeTasksCount) == 0)
                {
                    lock (_lockObj)
                    {
                        Monitor.Pulse(_lockObj);
                    }
                }
            }
        }

        private long CalculateSizes(DirectoryNode node)
        {
            long totalSize = 0;
            foreach (var child in node.Children)
            {
                if (child.IsDirectory)
                    totalSize += CalculateSizes(child);
                else
                    totalSize += child.Size;
            }

            if (node.IsDirectory)
                node.Size = totalSize;

            return node.Size;
        }

        private void CalculatePercentages(DirectoryNode node, long parentSize)
        {
            if (parentSize > 0)
            {
                node.PercentOfParent = (double)node.Size / parentSize * 100.0;
            }

            foreach (var child in node.Children)
            {
                CalculatePercentages(child, node.Size);
            }
        }
    }
}
