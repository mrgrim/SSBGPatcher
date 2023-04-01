using System;
using System.Collections.Generic;
using System.Linq;
using Mutagen.Bethesda;
using SSEForms = Mutagen.Bethesda.FormKeys.SkyrimSE;
using Mutagen.Bethesda.Synthesis;
using Mutagen.Bethesda.Skyrim;
using System.Threading.Tasks;
using Noggog;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Order;

namespace SSBGPatcher
{
    public class Program
    {
        static Lazy<Settings> _settings = null!;
        static public Settings settings => _settings.Value;
        
        public static async Task<int> Main(string[] args)
        {
            return await SynthesisPipeline.Instance
                .SetTypicalOpen(GameRelease.SkyrimSE, "SSBGPatcher.esp")
                .SetAutogeneratedSettings("Settings", "settings.json", out _settings)
                .AddPatch<ISkyrimMod, ISkyrimModGetter>(RunPatch)
                .Run(args);
        }

        private static readonly ModKey USSEPModKey = ModKey.FromNameAndExtension("Unofficial Skyrim Special Edition Patch.esp");
        private static readonly ModKey SSBGModKey = ModKey.FromNameAndExtension("Stretched Snow Begone.esp");

        private static IPatcherState<ISkyrimMod, ISkyrimModGetter>? _state;
        internal static IPatcherState<ISkyrimMod, ISkyrimModGetter> State
        {
            get { return _state!; }
        }
        
        public static void RunPatch(IPatcherState<ISkyrimMod, ISkyrimModGetter> state)
        {
            _state = state;
            ISkyrimModGetter? ssbgEntries = state.LoadOrder.TryGetValue(SSBGModKey)?.Mod ?? null;
            
            if(ssbgEntries is null)
                throw new Exception("Streteched Snow Begone is not present in the load order, make sure you installed it correctly.");
            
            //var materialMapping = MaterialMapping(bdsMod.Mod);
            var skipMods = Implicits.Get(state.PatchMod.GameRelease).Listings.ToHashSet();
            skipMods.Add(USSEPModKey);

            foreach(var ssbgStatic in ssbgEntries.Statics)
            {
                var contexts = ssbgStatic.ToLink().ResolveAllContexts<ISkyrimMod, ISkyrimModGetter, IStatic, IStaticGetter>(state.LinkCache).ToList();
                var currentWinner = contexts[0];

                // Do not patch winning override from game files or USSEP (Shouldn't even be possible).
                if (skipMods.Contains(currentWinner.ModKey))
                {
                    Console.WriteLine("Skip STAT {0}/{1:X8} with winning override in '{2}'",
                        currentWinner.Record.EditorID, currentWinner.Record.FormKey.ID, currentWinner.ModKey.FileName);
                    continue;
                }

                // Check whether we want to force override with a trusted mod's snow MATO
                var trueContext = settings.CheckTrusted(contexts, ssbgStatic, out var trusted, out var filename);
                var trueWinner = trueContext.Record;
                if (trusted)
                {
                    // Use trusted mod MATO. If it's the winning override then no-op, to avoid ITPO.
                    if (trueContext != currentWinner)
                    {
                        Console.WriteLine("Force-promote STAT {0}:{1}/{2:X8} from trusted mod '{3}'",
                            trueContext.ModKey.FileName, trueWinner.EditorID, trueWinner.FormKey.ID, filename);
                        state.PatchMod.Statics.GetOrAddAsOverride(trueWinner);
                    }
                    else
                    {
                        Console.WriteLine("STAT {0}/{1:X8} from trusted mod '{2}' is already the winning override",
                            trueWinner.EditorID, trueWinner.FormKey.ID, filename);
                    }
                    continue;
                }
                
                var patched = false;
                if (currentWinner.ModKey == ssbgEntries.ModKey)
                {
                    // SSBG is the winner. Look for and patch any runner-up.
                    foreach (var context in contexts)
                    {
                        if (!skipMods.Contains(context.ModKey) && !(context.ModKey == ssbgEntries.ModKey))
                        {
                            var patchedRecord = state.PatchMod.Statics.GetOrAddAsOverride(context.Record);
                            var matName = context.Record.Material;
                            Console.WriteLine("MATO {0:X8} mapped to SSBG {1:X8} in runner-up STAT {2}:{3}/{4:X8}",
                                matName.FormKey.ID, ssbgStatic.Material.FormKey.ID, context.ModKey.FileName, context.Record.EditorID, context.Record.FormKey.ID);
                            patchedRecord.Material = new FormLink<IMaterialObjectGetter>(ssbgStatic.Material.FormKey);
                            patchedRecord.MaxAngle = ssbgStatic.MaxAngle;
                            patched = true;
                            break;
                        }
                    }
                }
                else
                {
                    // SSBG is not the winner. Patch the winner.
                    var patchedRecord = state.PatchMod.Statics.GetOrAddAsOverride(currentWinner.Record);
                    var matName = currentWinner.Record.Material;
                    Console.WriteLine("MATO {0:X8} mapped to SSBG {1:X8} in winning STAT {2}:{3}/{4:X8}",
                        matName.FormKey.ID, ssbgStatic.Material.FormKey.ID, currentWinner.ModKey.FileName, currentWinner.Record.EditorID, currentWinner.Record.FormKey.ID);
                    patchedRecord.Material = new FormLink<IMaterialObjectGetter>(ssbgStatic.Material.FormKey);
                    patchedRecord.MaxAngle = ssbgStatic.MaxAngle;
                    patched = true;
                }
                
                if (!patched)
                    Console.WriteLine("STAT {0}:{1}/{2:X8} has no conflicts.", ssbgStatic.FormKey.ModKey.FileName, ssbgStatic.EditorID, ssbgStatic.FormKey.ID);
            }
        }
    }
}
