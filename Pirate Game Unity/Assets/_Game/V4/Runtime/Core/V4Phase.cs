using System;

namespace RoguePirates.V4
{
    /// <summary>The migration phase this code base has reached. Adapters tagged with a deletion phase at or below this fail a test.</summary>
    public static class V4Phase
    {
        public const int Current = 12;
    }

    /// <summary>Marks a temporary legacy adapter with the phase that deletes it.</summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
    public sealed class DeletedInPhaseAttribute : Attribute
    {
        public int Phase { get; }
        public string Reason { get; }

        public DeletedInPhaseAttribute(int phase, string reason = null)
        {
            Phase = phase;
            Reason = reason;
        }
    }

    public static class V4SceneNames
    {
        public const string Boot = "V4Boot";
        public const string MainMenu = "V4MainMenu";
        public const string SaltwindRun = "V4SaltwindRun";
        public const string ModuleLab = "V4ModuleLab";

        public const string Folder = "Assets/_Game/Scenes/V4";
        public const string BootPath = Folder + "/V4Boot.unity";
        public const string MainMenuPath = Folder + "/V4MainMenu.unity";
        public const string SaltwindRunPath = Folder + "/V4SaltwindRun.unity";
        public const string ModuleLabPath = Folder + "/V4ModuleLab.unity";
    }
}
