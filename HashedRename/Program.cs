using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace HashedRename
{
    class Program
    {
        static string qarDictionaryName = "qar_dictionary.txt";

        static string[] archiveFolderSuffixes =
        {
            "_dat",
            "_fpk",
            "_fpkd",
            "_pftxs",
            "_sbp",
            /* 
            //tex so far always in fpk/d so wont have hashed name 
            "_caar",
            "_mtar",
            "_mtard",
            */
        };

        static string[] skipTypes = {
            ".txt",
        };

        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                return;
            }

            //Get dictionary
            string executingAssemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string dictionaryPath = Path.Combine(executingAssemblyLocation, qarDictionaryName);
            if (args[0].Contains(qarDictionaryName))
            {
                dictionaryPath = args[0];
            }

            if (!File.Exists(dictionaryPath))
            {
                Console.WriteLine("ERROR: Could not find dictionary at " + dictionaryPath);
                return;
            }

            dictionaryPath = PathFixup(dictionaryPath);

            var qarDictionary = new ConcurrentDictionary<string, string>();
            Parallel.ForEach(File.ReadLines(dictionaryPath), line =>
            {
                var hash = Utility.Hashing.PathCode64Str(line);
                qarDictionary.TryAdd(hash, line);
            });

            if (qarDictionary.Count == 0)
            {
                Console.WriteLine("ERROR: qar dictionary empty");
                return;
            }
            //

            List<string> fileList = GetFileList(args);
            if (fileList.Count == 0)
            {
                Console.WriteLine("ERROR: no files found");
                return;
            }

            HashedRename(qarDictionary, fileList);

            Console.WriteLine("All done");
        }

        private static void HashedRename(ConcurrentDictionary<string, string> qarDictionary, List<string> fileList)
        {
            foreach (string filePath in fileList)
            {
                string basePath = Path.GetDirectoryName(filePath);
                string extension = Path.GetExtension(filePath);
                string name = Path.GetFileNameWithoutExtension(filePath);

                bool isFile = File.Exists(filePath);

                if (!isFile)
                {
                    if (!archiveFolderSuffixes.Contains(name)) //tex make sure folder isn't just soly named as an archive suffix
                    {
                        //tex find out if folder has an archive suffix
                        int suffixStart = name.LastIndexOf('_');
                        if (suffixStart != -1)
                        {
                            string suffix = name.Substring(suffixStart);
                            if (archiveFolderSuffixes.Contains(suffix))
                            {
                                extension = suffix;
                                name = name.Substring(0, suffixStart);
                            }
                        }
                    }
                }

                try
                {
                    UInt64 uHash = UInt64.Parse(name, System.Globalization.NumberStyles.HexNumber);
                }
                catch
                {
                    continue;
                };

                string unHashed = null;
                if (!qarDictionary.TryGetValue(name, out unHashed))
                {
                    Console.WriteLine("Could not find dictionary match for " + name);
                }
                else
                {
                    Console.WriteLine("Found match for " + name + " : " + unHashed + ", renaming..");
                    string destination = PathCombine(basePath, unHashed + extension);
                    destination = PathFixup(destination);
                    if (isFile)
                    {
                        string destFolder = Path.GetDirectoryName(destination);
                        Directory.CreateDirectory(destFolder);
                        File.Copy(filePath, destination, true);
                        File.Delete(filePath);
                    }
                    else
                    {
                        MoveDirectory(filePath, destination);
                    }
                }
            }
        }

        private static List<string> GetFileList(string[] args)
        {
            var fileList = new List<string>();
            foreach (string arg in args)
            {
                if (File.Exists(arg))
                {
                    if (!skipTypes.Contains(Path.GetExtension(arg)))
                    {
                        fileList.Add(arg);
                    }
                }
                if (Directory.Exists(arg))
                {
                    var dirFiles = Directory.GetFiles(arg, "*.*").ToList<string>();
                    fileList.AddRange(dirFiles);
                }
            }

            return fileList;
        }

        private static string PathFixup(string dictionaryPath)
        {
            if (!Path.IsPathRooted(dictionaryPath))
            {
                dictionaryPath = Path.GetFullPath(dictionaryPath);
            }
            if (!Path.IsPathRooted(dictionaryPath))
            {
                dictionaryPath = Path.GetFullPath(dictionaryPath);
            }

            dictionaryPath = Regex.Replace(dictionaryPath, @"\\", "/");
            return dictionaryPath;
        }

        private static string PathCombine(string path1, string path2)
        {
            if (Path.IsPathRooted(path2))
            {
                path2 = path2.TrimStart(Path.DirectorySeparatorChar);
                path2 = path2.TrimStart(Path.AltDirectorySeparatorChar);
            }

            return Path.Combine(path1, path2);
        }

        public static void MoveDirectory(string source, string target)
        {
            var sourcePath = source.TrimEnd('\\', ' ');
            var targetPath = target.TrimEnd('\\', ' ');
            var files = Directory.EnumerateFiles(sourcePath, "*", SearchOption.AllDirectories)
                                 .GroupBy(s => Path.GetDirectoryName(s));
            foreach (var folder in files)
            {
                var targetFolder = folder.Key.Replace(sourcePath, targetPath);
                Directory.CreateDirectory(targetFolder);
                foreach (var file in folder)
                {
                    var targetFile = Path.Combine(targetFolder, Path.GetFileName(file));
                    if (File.Exists(targetFile))
                    {
                        DeleteAndWait(targetFile);
                    }
                    File.Move(file, targetFile);
                }
            }
            Directory.Delete(source, true);
        }

        private static void DeleteAndWait(string path)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
            else
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            while (Directory.Exists(path))
            {
                Thread.Sleep(100);
            }
        }
    }
}
