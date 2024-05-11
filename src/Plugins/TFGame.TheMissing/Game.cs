using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using TF.Core.Entities;
using TF.Core.Files;
using TFGame.TheMissing.Files;

namespace TFGame.TheMissing
{
    public class Game : UnityGame.Game
    {
        public override string Id => "880de844-b89d-42a8-b7c5-3361d887e983";
        public override string Name => "The MISSING: J.J. Macfield and the Island of Memories";
        public override string Description => "Build Id: 4168963";
        public override Image Icon => Resources.Icon; // https://www.deviantart.com/clarence1996/art/The-MISSING-J-J-Macfield-atIoM-768659719
        public override int Version => 1;
        public override System.Text.Encoding FileEncoding => new Encoding();

        public override GameFileContainer[] GetContainers(string path)
        {
            var result = new List<GameFileContainer>();

            var txtSearch = new GameFileSearch()
            {
                RelativePath = @".",
                SearchPattern = "msg????en.txt",
                IsWildcard = true,
                RecursiveSearch = true,
                FileType = typeof(Files.Txt.File)
            };

            var ddsSearch = new GameFileSearch()
            {
                RelativePath = @".",
                SearchPattern = "stamp??.tex.dds;load.text.dds;photo_06.tex.dds;save.tex.dds;window.tex.dds",
                IsWildcard = true,
                RecursiveSearch = true,
                FileType = typeof(DDSFile)
            };

            var ttfSearch = new GameFileSearch()
            {
                RelativePath = @".",
                SearchPattern = "TT_NewCinemaB-D.ttf",
                IsWildcard = true,
                RecursiveSearch = true,
                FileType = typeof(TrueTypeFontFile)
            };

            var resources = new GameFileContainer
            {
                Path = @"TheMISSING_Data\resources.assets",
                Type = ContainerType.CompressedFile
            };

            resources.FileSearches.Add(txtSearch);
            resources.FileSearches.Add(ddsSearch);
            resources.FileSearches.Add(ttfSearch);

            result.Add(resources);

            var graffitiSearch = new GameFileSearch()
            {
                RelativePath = @".",
                SearchPattern = "OBJ_Graffiti_01.tex.dds",
                IsWildcard = true,
                RecursiveSearch = true,
                FileType = typeof(DDSFile)
            };

            var sharedAssets44 = new GameFileContainer
            {
                Path = @"TheMISSING_Data\sharedassets44.assets",
                Type = ContainerType.CompressedFile
            };

            sharedAssets44.FileSearches.Add(graffitiSearch);

            result.Add(sharedAssets44);

            return result.ToArray();
        }

        public override void ExtractFile(string inputFile, string outputPath)
        {
            var fileName = Path.GetFileName(inputFile);
            var extension = Path.GetExtension(inputFile);

            if (AllowedExtensions.Contains(extension))
            {
                Unity3DFile.Extract(inputFile, outputPath);
            }
        }

        public override void RepackFile(string inputPath, string outputFile, bool compress)
        {
            var fileName = Path.GetFileName(outputFile);
            var extension = Path.GetExtension(outputFile);

            if (AllowedExtensions.Contains(extension))
            {
                Unity3DFile.Repack(inputPath, outputFile, compress);
            }
        }

        public override void PreprocessContainer(TranslationFileContainer container, string containerPath, string extractionPath)
        {
            if (container.Type == ContainerType.CompressedFile)
            {
                var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(containerPath);

                var inputFolder = Path.GetDirectoryName(containerPath);
                var files = Directory.EnumerateFiles(inputFolder, $"{fileNameWithoutExtension}.*");
                foreach (var file in files)
                {
                    var outputFilePath = Path.Combine(extractionPath, Path.GetFileName(file));
                    File.Copy(file, outputFilePath);
                }
            }
        }

        public override void PostprocessContainer(TranslationFileContainer container, string containerPath, string extractionPath)
        {
            if (container.Type == ContainerType.CompressedFile)
            {
                var extractedFiles = Directory.GetFiles(Path.Combine(extractionPath, "Unity_Assets_Files"), "*.*", SearchOption.AllDirectories);
                foreach (var file in extractedFiles)
                {
                    if (container.Files.All(x => x.Path != file) && !file.EndsWith("mplus-1m-regular.ttf") && !file.EndsWith("resources_00001.-13"))
                    {
                        File.Delete(file);
                    }
                }
            }
        }
    }
}
