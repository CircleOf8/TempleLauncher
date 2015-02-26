using System;
using System.Collections.Generic;
using System.Text;

namespace TempleExtender
{
    public class Settings
    {
        public List<string> DataArchives { get; private set; }

        public List<string> ModuleArchives { get; private set; }

        public Dictionary<string, string> MountedDirectories { get; private set; }

        public List<string> DirectoryOverlays { get; private set; }

        public bool Windowed { get; set; }

        public bool DisableIntroMovies { get; set; }

        public bool DebuggingOutput { get; set; }

        public string InstallationDirectory { get; set; }

        public bool WriteAccessVirtualization { get; set; }

        public string VirtualWriteDirectory { get; set; }

        public Settings()
        {
            MountedDirectories = new Dictionary<string, string>();
            DirectoryOverlays = new List<string>();
            DataArchives = new List<string>();
            ModuleArchives = new List<string>();
        }

    }
}