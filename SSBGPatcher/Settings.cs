using System;
using System.Collections.Generic;
using System.Linq;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Plugins.Order;
using Mutagen.Bethesda.Skyrim;
using Mutagen.Bethesda.Synthesis;
using Mutagen.Bethesda.Synthesis.Settings;

namespace SSBGPatcher
{
    public class Settings
    {
        [SynthesisSettingName("For Stretched Snow Begone:")]

        private List<string> _modsTrusted= new List<string>();
        private static List<string> DefaultModsTrusted()
        {
            var defaults = new List<string>();
            return defaults;
        }
        private static readonly List<string> _defaultModsTrusted = DefaultModsTrusted();
        [SynthesisSettingName("Mods that should not be overridden by Stretched Snow Begone")]
        [SynthesisTooltip("Each entry is a string matching the name of a mod that has better snow or is incompatible with Stretched Snow Begone's changes. Prefer 'MyModName.' to avoid having multiple entries for esl/esp/esm variants.")]
        [SynthesisDescription("List of names of mods that have better snow or are incompatible with Stretched Snow Begone's changes.")]
        public List<string> ModsTrusted
        {
            get { return _modsTrusted; }
            set { _modsTrusted = value; }
        }

        private HashSet<ModKey>? _trustedMods;
        public HashSet<ModKey> TrustedMods
        {
            get
            {
                if (_trustedMods == null)
                {
                    var modKeys = new List<ModKey>();
                    IList<ModKey> modFiles = Program.State.LoadOrder.Keys.ToList();
                    foreach (string modFilter in ModsTrusted.Concat(_defaultModsTrusted))
                    {
                        if (!String.IsNullOrWhiteSpace(modFilter))
                            modKeys.AddRange(modFiles.Where(modKey => modKey.FileName.String.Contains(modFilter, StringComparison.OrdinalIgnoreCase)));
                    }
                    _trustedMods = new HashSet<ModKey>(modKeys);
                }
                return _trustedMods;
            }
        }

        internal IModContext<ISkyrimMod, ISkyrimModGetter, IStatic, IStaticGetter> CheckTrusted(
            List<IModContext<ISkyrimMod, ISkyrimModGetter, IStatic, IStaticGetter>> contexts,
            IStaticGetter target, out bool trusted, out string filename)
        {
            trusted = false;
            filename = String.Empty;
            if (TrustedMods.Count > 0)
            {
                // check mods with better snow for this STAT record, use that target in the patch if present
                foreach (var context in contexts)
                {
                    if (TrustedMods.Contains(context.ModKey))
                    {
                        filename = context.ModKey.FileName;
                        trusted = true;
                        return context;
                    }
                }
            }
            // keep winning override
            return contexts[0];
        }
    }
}
