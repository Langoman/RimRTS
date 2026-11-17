using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Verse;

namespace RimRTS.Skirmish
{
    /// <summary>
    /// Reads WeaponBlacklist.txt from the mod folder: one ThingDef defName per line, lines
    /// starting with '#' ignored, blank lines ignored. Unknown defNames only log a warning so
    /// a typo never breaks a match.
    ///
    /// The file is read once per game session; edit it and restart the game to apply.
    /// </summary>
    public static class WeaponBlacklist
    {
        private static readonly Lazy<HashSet<string>> Cache = new Lazy<HashSet<string>>(Load);

        public static HashSet<string> DefNames()
        {
            return Cache.Value;
        }

        private static HashSet<string> Load()
        {
            HashSet<string> defNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            ModContentPack mod = LoadedModManager.RunningModsListForReading
                .FirstOrDefault(candidate =>
                    candidate.PackageId.Equals(SquadLoadout.PackageId, StringComparison.OrdinalIgnoreCase));
            if (mod == null)
            {
                Log.Warning("[RimRTS] Could not find the RimRTS mod content pack; the weapon blacklist is ignored.");
                return defNames;
            }

            string path = Path.Combine(mod.RootDir, SquadLoadout.WeaponBlacklistFileName);
            string text;
            try
            {
                text = GenFile.TextFromRawFile(path);
            }
            catch (Exception exception)
            {
                Log.Warning("[RimRTS] Could not read '" + path + "': " + exception.Message);
                return defNames;
            }

            if (text.NullOrEmpty())
            {
                return defNames;
            }

            foreach (string rawLine in text.Split('\n'))
            {
                string line = rawLine.Trim();
                int commentStart = line.IndexOf('#');
                if (commentStart >= 0)
                {
                    line = line.Substring(0, commentStart).Trim();
                }

                if (line.Length == 0)
                {
                    continue;
                }

                if (DefDatabase<ThingDef>.GetNamedSilentFail(line) == null)
                {
                    Log.Warning("[RimRTS] Weapon blacklist lists unknown ThingDef '" + line + "'.");
                }

                defNames.Add(line);
            }

            return defNames;
        }
    }
}
