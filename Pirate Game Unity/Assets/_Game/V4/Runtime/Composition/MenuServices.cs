using System;
using System.Collections.Generic;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Config;
using RoguePirates.V4.Ports;
using RoguePirates.V4.Rewards;

namespace RoguePirates.V4.Composition
{
    /// <summary>
    /// What the V4 menu needs (M14.13 review fix): the config set with the session's active preset applied (so the
    /// v3-parity preset switches the Captain lock rule back to V3's), the Captain locks and the Rum unlocks, wired to the
    /// shared save and V3's progression state through the ports. The run validates the saved Captain with the same locks.
    /// </summary>
    public sealed class MenuServices : IDisposable
    {
        public ConfigSetRuntime Config { get; }
        public CaptainLocks Locks { get; }
        public UnlockService Unlocks { get; }

        MenuServices(ConfigSetRuntime config)
        {
            Config = config;
            Locks = CreateLocks(() => Config);
            Unlocks = new UnlockService(Save, () => Config);
        }

        static SaveService Save() => AppRoot.Instance != null ? AppRoot.Instance.Save : null;

        /// <summary>The Captain locks over a config (the menu's, or the run's).</summary>
        public static CaptainLocks CreateLocks(Func<ConfigSetRuntime> config) =>
            new CaptainLocks(config, Save, () => ContentPort.FirstConquestDone, () => LifetimeGold.Total,
                () => ContentPort.BypassProgressionGates);

        /// <summary>Leaves any run context, initializes the app and the combat experiment for the menu, and builds the services.</summary>
        public static MenuServices Create(ConfigSet set, List<string> presetErrors = null)
        {
            ContentPort.PrepareMenu();
            return ForConfig(set, RunRequest.ActivePresetJson, presetErrors);
        }

        /// <summary>The services over a config set with a preset applied (null = defaults).</summary>
        public static MenuServices ForConfig(ConfigSet set, string presetJson, List<string> presetErrors = null)
        {
            if (set == null) return null;
            var config = new ConfigSetRuntime(set);
            if (!string.IsNullOrEmpty(presetJson))
                PresetCodec.Apply(config, PresetCodec.FromJson(presetJson), 0f, presetErrors ?? new List<string>());
            return new MenuServices(config);
        }

        /// <summary>The run's combat experiment as V3's Set Sail chose it.</summary>
        public static void BeginSaltwindExperiment() => ContentPort.BeginSaltwindExperiment();

        public void Dispose() => Config?.Dispose();
    }
}
