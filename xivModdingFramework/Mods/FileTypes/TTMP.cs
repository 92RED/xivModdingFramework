﻿// xivModdingFramework
// Copyright © 2018 Rafael Gonzalez - All Rights Reserved
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

using ImageMagick;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using xivModdingFramework.General.Enums;
using xivModdingFramework.Mods.DataContainers;
using xivModdingFramework.SqPack.FileTypes;

namespace xivModdingFramework.Mods.FileTypes
{
    public class TTMP
    {
        private readonly string _currentWizardTTMPVersion = "1.0w";
        private readonly string _currentSimpleTTMPVersion = "1.0s";
        private string _tempMPD, _tempMPL, _source;
        private readonly DirectoryInfo _modPackDirectory;

        public TTMP(DirectoryInfo modPackDirectory, string source)
        {
            _modPackDirectory = modPackDirectory;
            _source = source;
        }

        /// <summary>
        /// Creates a mod pack that uses a wizard for installation
        /// </summary>
        /// <param name="modPackData">The data that will go into the mod pack</param>
        /// <param name="progress">The progress of the mod pack creation</param>
        /// <returns>The number of pages created for the mod pack</returns>
        public async Task<int> CreateWizardModPack(ModPackData modPackData, IProgress<double> progress)
        {
            var processCount = await Task.Run<int>(() =>
            {
                _tempMPD = Path.GetTempFileName();
                _tempMPL = Path.GetTempFileName();
                var imageList = new Dictionary<string, string>();
                var pageCount = 1;

                var modPackJson = new ModPackJson
                {
                    TTMPVersion = _currentWizardTTMPVersion,
                    Name = modPackData.Name,
                    Author = modPackData.Author,
                    Version = modPackData.Version.ToString(),
                    Description = modPackData.Description,
                    ModPackPages = new List<ModPackPageJson>()
                };

                using (var binaryWriter = new BinaryWriter(File.Open(_tempMPD, FileMode.Open)))
                {
                    foreach (var modPackPage in modPackData.ModPackPages)
                    {
                        var modPackPageJson = new ModPackPageJson
                        {
                            PageIndex = modPackPage.PageIndex,
                            ModGroups = new List<ModGroupJson>()
                        };

                        modPackJson.ModPackPages.Add(modPackPageJson);

                        foreach (var modGroup in modPackPage.ModGroups)
                        {
                            var modGroupJson = new ModGroupJson
                            {
                                GroupName = modGroup.GroupName,
                                SelectionType = modGroup.SelectionType,
                                OptionList = new List<ModOptionJson>()
                            };

                            modPackPageJson.ModGroups.Add(modGroupJson);

                            foreach (var modOption in modGroup.OptionList)
                            {
                                var randomFileName = "";

                                if (modOption.Image != null)
                                {
                                    randomFileName = $"{Path.GetRandomFileName()}.png";
                                    imageList.Add(modOption.Image.FileName, randomFileName);
                                }

                                var modOptionJson = new ModOptionJson
                                {
                                    Name = modOption.Name,
                                    Description = modOption.Description,
                                    ImagePath = randomFileName,
                                    GroupName = modOption.GroupName,
                                    SelectionType = modOption.SelectionType,
                                    ModsJsons = new List<ModsJson>()
                                };

                                modGroupJson.OptionList.Add(modOptionJson);

                                foreach (var modOptionMod in modOption.Mods)
                                {
                                    var dataFile = GetDataFileFromPath(modOptionMod.Key);

                                    var modsJson = new ModsJson
                                    {
                                        Name = modOptionMod.Value.Name,
                                        Category = modOptionMod.Value.Category,
                                        FullPath = modOptionMod.Key,
                                        ModSize = modOptionMod.Value.ModDataBytes.Length,
                                        ModOffset = binaryWriter.BaseStream.Position,
                                        DatFile = dataFile.GetDataFileName()
                                    };

                                    binaryWriter.Write(modOptionMod.Value.ModDataBytes);

                                    modOptionJson.ModsJsons.Add(modsJson);
                                }
                            }
                        }

                        progress?.Report((double)pageCount / modPackData.ModPackPages.Count);

                        pageCount++;
                    }
                }

                File.WriteAllText(_tempMPL, JsonConvert.SerializeObject(modPackJson));

                var modPackPath = $"{_modPackDirectory}\\{modPackData.Name}.ttmp";

                if (File.Exists(modPackPath))
                {
                    var fileNum = 1;
                    modPackPath = $"{_modPackDirectory}\\{modPackData.Name}({fileNum}).ttmp";
                    while (File.Exists(modPackPath))
                    {
                        fileNum++;
                        modPackPath = $"{_modPackDirectory}\\{modPackData.Name}({fileNum}).ttmp";
                    }
                }

                using (var zip = ZipFile.Open(modPackPath, ZipArchiveMode.Create))
                {
                    zip.CreateEntryFromFile(_tempMPL, "TTMPL.mpl");
                    zip.CreateEntryFromFile(_tempMPD, "TTMPD.mpd");
                    foreach (var image in imageList)
                    {
                        zip.CreateEntryFromFile(image.Key, image.Value);
                    }
                }

                File.Delete(_tempMPD);
                File.Delete(_tempMPL);

                return pageCount;
            });

            return processCount;
        }

        /// <summary>
        /// Creates a mod pack that uses simple installation
        /// </summary>
        /// <param name="modPackData">The data that will go into the mod pack</param>
        /// <param name="gameDirectory">The game directory</param>
        /// <param name="progress">The progress of the mod pack creation</param>
        /// <returns>The number of mods processed for the mod pack</returns>
        public async Task<int> CreateSimpleModPack(SimpleModPackData modPackData, DirectoryInfo gameDirectory, IProgress<double> progress)
        {
            var processCount = await Task.Run<int>(() =>
            {
                var dat = new Dat(gameDirectory);
                _tempMPD = Path.GetTempFileName();
                _tempMPL = Path.GetTempFileName();
                var modCount = 1;

                var modPackJson = new ModPackJson
                {
                    TTMPVersion = _currentSimpleTTMPVersion,
                    Name = modPackData.Name,
                    Author = modPackData.Author,
                    Version = modPackData.Version.ToString(),
                    Description = modPackData.Description,
                    SimpleModsList = new List<ModsJson>()
                };

                using (var binaryWriter = new BinaryWriter(File.Open(_tempMPD, FileMode.Open)))
                {
                    foreach (var simpleModData in modPackData.SimpleModDataList)
                    {
                        var modsJson = new ModsJson
                        {
                            Name = simpleModData.Name,
                            Category = simpleModData.Category,
                            FullPath = simpleModData.FullPath,
                            ModSize = simpleModData.ModSize,
                            DatFile = simpleModData.DatFile,
                            ModOffset = binaryWriter.BaseStream.Position
                        };

                        var rawData = dat.GetRawData((int)simpleModData.ModOffset, XivDataFiles.GetXivDataFile(simpleModData.DatFile),
                            simpleModData.ModSize);

                        binaryWriter.Write(rawData);

                        modPackJson.SimpleModsList.Add(modsJson);

                        progress?.Report((double)modCount / modPackData.SimpleModDataList.Count);

                        modCount++;
                    }
                }

                File.WriteAllText(_tempMPL, JsonConvert.SerializeObject(modPackJson));

                var modPackPath = $"{_modPackDirectory}\\{modPackData.Name}.ttmp";

                if (File.Exists(modPackPath))
                {
                    var fileNum = 1;
                    modPackPath = $"{_modPackDirectory}\\{modPackData.Name}({fileNum}).ttmp";
                    while (File.Exists(modPackPath))
                    {
                        fileNum++;
                        modPackPath = $"{_modPackDirectory}\\{modPackData.Name}({fileNum}).ttmp";
                    }
                }

                using (var zip = ZipFile.Open(modPackPath, ZipArchiveMode.Create))
                {
                    zip.CreateEntryFromFile(_tempMPL, "TTMPL.mpl");
                    zip.CreateEntryFromFile(_tempMPD, "TTMPD.mpd");
                }

                File.Delete(_tempMPD);
                File.Delete(_tempMPL);

                return modCount;
            });

            return processCount;
        }

        /// <summary>
        /// Gets the data from a mod pack including images if present
        /// </summary>
        /// <param name="modPackDirectory">The directory of the mod pack</param>
        /// <returns>A tuple containing the mod pack json data and a dictionary of images if any</returns>
        public (ModPackJson ModPackJson, Dictionary<string, MagickImage> ImageDictionary) GetModPackJsonData(DirectoryInfo modPackDirectory)
        {
            ModPackJson modPackJson = null;
            var imageDictionary = new Dictionary<string, MagickImage>();

            using (var archive = ZipFile.OpenRead(modPackDirectory.FullName))
            {
                foreach (var entry in archive.Entries)
                {
                    if (entry.FullName.EndsWith(".mpl"))
                    {
                        using (var streamReader = new StreamReader(entry.Open()))
                        {
                            var jsonString = streamReader.ReadToEnd();

                            modPackJson = JsonConvert.DeserializeObject<ModPackJson>(jsonString);
                        }
                    }

                    if (entry.FullName.EndsWith(".png"))
                    {
                        imageDictionary.Add(entry.FullName, new MagickImage(entry.Open()));
                    }
                }
            }

            return (modPackJson, imageDictionary);
        }

        /// <summary>
        /// Gets the data from first generation mod packs
        /// </summary>
        /// <param name="modPackDirectory">The directory of the mod pack</param>
        /// <returns>A list containing original mod pack json data</returns>
        public List<OriginalModPackJson> GetOriginalModPackJsonData(DirectoryInfo modPackDirectory)
        {
            var modPackJsonList = new List<OriginalModPackJson>();

            using (var archive = ZipFile.OpenRead(modPackDirectory.FullName))
            {
                foreach (var entry in archive.Entries)
                {
                    if (entry.FullName.EndsWith(".mpl"))
                    {
                        using (var streamReader = new StreamReader(entry.Open()))
                        {
                            var line = streamReader.ReadLine();
                            if (line.ToLower().Contains("version"))
                            {
                                //mpInfo = JsonConvert.DeserializeObject<ModPackInfo>(line);
                            }
                            else
                            {
                                modPackJsonList.Add(JsonConvert.DeserializeObject<OriginalModPackJson>(line));
                            }

                            while (streamReader.Peek() >= 0)
                            {
                                line = streamReader.ReadLine();
                                modPackJsonList.Add(JsonConvert.DeserializeObject<OriginalModPackJson>(line));
                            }
                        }
                    }
                }
            }

            return modPackJsonList;
        }

        /// <summary>
        /// Gets the version from a mod pack
        /// </summary>
        /// <param name="modPackDirectory">The mod pack directory</param>
        /// <returns>The version of the mod pack as a string</returns>
        public string GetVersion(DirectoryInfo modPackDirectory)
        {
            ModPackJson modPackJson = null;

            using (var archive = ZipFile.OpenRead(modPackDirectory.FullName))
            {
                foreach (var entry in archive.Entries)
                {
                    if (entry.FullName.EndsWith(".mpl"))
                    {
                        using (var streamReader = new StreamReader(entry.Open()))
                        {
                            var jsonString = streamReader.ReadToEnd();

                            modPackJson = JsonConvert.DeserializeObject<ModPackJson>(jsonString);
                        }
                    }
                }
            }

            return modPackJson.TTMPVersion;
        }

        /// <summary>
        /// Imports a mod pack asynchronously 
        /// </summary>
        /// <param name="modPackDirectory">The directory of the mod pack</param>
        /// <param name="modsJson">The list of mods to be imported</param>
        /// <param name="gameDirectory">The game directory</param>
        /// <param name="modListDirectory">The mod list directory</param>
        /// <param name="progress">The progress of the import</param>
        /// <returns>The number of total mods imported</returns>
        public async Task<int> ImportModPackAsync(DirectoryInfo modPackDirectory, List<ModsJson> modsJson,
            DirectoryInfo gameDirectory, DirectoryInfo modListDirectory, IProgress<double> progress)
        {
            var processCount = await Task.Run<int>(() =>
            {
                var dat = new Dat(gameDirectory);
                var modListFullPaths = new List<string>();
                var modList = JsonConvert.DeserializeObject<ModList>(File.ReadAllText(modListDirectory.FullName));
                var modCount = 1;

                foreach (var modListMod in modList.Mods)
                {
                    modListFullPaths.Add(modListMod.fullPath);
                }

                using (var archive = ZipFile.OpenRead(modPackDirectory.FullName))
                {
                    foreach (var zipEntry in archive.Entries)
                    {
                        if (zipEntry.FullName.EndsWith(".mpd"))
                        {
                            _tempMPD = Path.GetTempFileName();
                            zipEntry.ExtractToFile(_tempMPD, true);

                            using (var binaryReader = new BinaryReader(File.OpenRead(_tempMPD)))
                            {
                                foreach (var modJson in modsJson)
                                {
                                    if (modListFullPaths.Contains(modJson.FullPath))
                                    {
                                        var existingEntry = (from entry in modList.Mods
                                                             where entry.fullPath.Equals(modJson.FullPath)
                                                             select entry).FirstOrDefault();

                                        binaryReader.BaseStream.Seek(modJson.ModOffset, SeekOrigin.Begin);

                                        var data = binaryReader.ReadBytes(modJson.ModSize);

                                        dat.WriteToDat(new List<byte>(data), existingEntry, modJson.FullPath,
                                            modJson.Category, modJson.Name, XivDataFiles.GetXivDataFile(modJson.DatFile), _source,
                                            GetDataType(modJson.FullPath));
                                    }
                                    else
                                    {
                                        binaryReader.BaseStream.Seek(modJson.ModOffset, SeekOrigin.Begin);

                                        var data = binaryReader.ReadBytes(modJson.ModSize);

                                        dat.WriteToDat(new List<byte>(data), null, modJson.FullPath,
                                            modJson.Category, modJson.Name, XivDataFiles.GetXivDataFile(modJson.DatFile), _source,
                                            GetDataType(modJson.FullPath));
                                    }

                                    progress?.Report((double)modCount/ modsJson.Count);

                                    modCount++;
                                }
                            }

                            File.Delete(_tempMPD);
                            break;
                        }
                    }
                }

                return modCount - 1;
            });

            return processCount;
        }

        /// <summary>
        /// Imports a mod pack
        /// </summary>
        /// <param name="modPackDirectory">The directory of the mod pack</param>
        /// <param name="modsJson">The list of mods to be imported</param>
        /// <param name="gameDirectory">The game directory</param>
        /// <param name="modListDirectory">The mod list directory</param>
        public void ImportModPack(DirectoryInfo modPackDirectory, List<ModsJson> modsJson, DirectoryInfo gameDirectory, DirectoryInfo modListDirectory)
        {
            var dat = new Dat(gameDirectory);
            var modListFullPaths = new List<string>();
            var modList = JsonConvert.DeserializeObject<ModList>(File.ReadAllText(modListDirectory.FullName));

            foreach (var modListMod in modList.Mods)
            {
                modListFullPaths.Add(modListMod.fullPath);
            }

            using (var archive = ZipFile.OpenRead(modPackDirectory.FullName))
            {
                foreach (var zipEntry in archive.Entries)
                {
                    if (zipEntry.FullName.EndsWith(".mpd"))
                    {
                        using (var binaryReader = new BinaryReader(zipEntry.Open()))
                        {
                            foreach (var modJson in modsJson)
                            {
                                if (modListFullPaths.Contains(modJson.FullPath))
                                {
                                    var existingEntry = (from entry in modList.Mods
                                        where entry.fullPath.Equals(modJson.FullPath)
                                        select entry).FirstOrDefault();

                                    binaryReader.BaseStream.Seek(modJson.ModOffset, SeekOrigin.Begin);

                                    var data = binaryReader.ReadBytes(modJson.ModSize);

                                    dat.WriteToDat(new List<byte>(data), existingEntry, modJson.FullPath,
                                        modJson.Category, modJson.Name, XivDataFiles.GetXivDataFile(modJson.DatFile), _source,
                                        GetDataType(modJson.FullPath));
                                }
                                else
                                {
                                    binaryReader.BaseStream.Seek(modJson.ModOffset, SeekOrigin.Begin);

                                    var data = binaryReader.ReadBytes(modJson.ModSize);

                                    dat.WriteToDat(new List<byte>(data), null, modJson.FullPath,
                                        modJson.Category, modJson.Name, XivDataFiles.GetXivDataFile(modJson.DatFile), _source,
                                        GetDataType(modJson.FullPath));
                                }
                            }
                        }
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Gets the data type from an item path
        /// </summary>
        /// <param name="path">The path of the item</param>
        /// <returns>The data type</returns>
        private int GetDataType(string path)
        {
            if (path.Contains(".tex"))
            {
                return 4;
            }

            if (path.Contains(".mdl"))
            {
                return 3;
            }

            return 2;
        }


        /// <summary>
        /// Gets a XivDataFile category for the specified path.
        /// </summary>
        /// <param name="internalPath">The internal file path</param>
        /// <returns>A XivDataFile entry for the needed dat category</returns>
        private XivDataFile GetDataFileFromPath(string internalPath)
        {
            var folderKey = internalPath.Substring(0, internalPath.IndexOf("/", StringComparison.Ordinal));

            var cats = Enum.GetValues(typeof(XivDataFile)).Cast<XivDataFile>();

            foreach (var cat in cats)
            {
                if (cat.GetFolderKey() == folderKey)
                    return cat;
            }

            throw new ArgumentException("[Dat] Could not find category for path: " + internalPath);
        }
    }
}