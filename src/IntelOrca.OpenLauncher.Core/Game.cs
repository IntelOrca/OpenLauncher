using System;
using System.IO;

namespace IntelOrca.OpenLauncher.Core
{
    public class Game
    {
        public static Game OpenRCT2 => new Game("OpenRCT2", "openrct2", true, new RepositoryName("OpenRCT2", "OpenRCT2"));
        public static Game OpenLoco => new Game("OpenLoco", "openloco", false, new RepositoryName("OpenLoco", "OpenLoco"));

        public string Name { get; }
        public string BinaryName { get; }
        public string DefaultLocation { get; }
        public RepositoryName Repository { get; set; }

        private Game(string name, string binaryName, bool usesDocuments, RepositoryName repositoryName)
        {
            Name = name;
            BinaryName = binaryName;

            var root = usesDocuments ?
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) :
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            DefaultLocation = Path.Combine(root, name);
            Repository = repositoryName;
        }
    }

    public struct RepositoryName
    {
        public string Owner { get; }
        public string Name { get; }

        public RepositoryName(string owner, string name)
        {
            Owner = owner;
            Name = name;
        }

        public override string ToString() => $"{Owner}/{Name}";
    }
}
