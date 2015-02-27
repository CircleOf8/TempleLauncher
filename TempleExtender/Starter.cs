using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace TempleExtender
{
    [StructLayout(LayoutKind.Sequential)]
    public struct AnimalCompanionList
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 10)]
        public int[] Prototypes;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 10)]
        public int[] Levels;
    }

    public class Starter
    {
        /// <summary>
        /// Patches the loaded temple.dll library with the given settings.
        /// </summary>
        /// <param name="filename">Path to the temple.dll that should be used.</param>
        /// <param name="settings">The settings to use for starting the game.</param>
        /// <exception cref="Exception"></exception>
        public static void Start(string filename, Settings settings)
        {
            using (var library = new TempleLibrary(filename))
            {
                if (!library.Valid)
                {
                    throw new Exception(string.Format(
                        "Unable to start the game. Cannot load the game library {0}: {1}", filename, library.Error));
                }

                // Test code that just overwrites the prototype of the first animal companion to 1000
                var animalCompanions = library.ReadStructure<AnimalCompanionList>(0x2EF14C);
                animalCompanions.Prototypes[0] = 1000;
                library.WriteStructure(0x2EF14C, animalCompanions);

                using (new DebugOutputRedirection(library, settings))
                {
                    using (new FilesystemRedirector(library, settings))
                    {
                        library.StartGame(settings);
                    }
                }
            }
        }
    }
}