using System;
using System.IO;
using UnityEngine;

namespace PirateGame.Net
{
    /// <summary>
    /// ParrelSync clone detection and shared match address for two-editor co-op.
    /// Clone projects contain a ".clone" file next to Assets/.
    /// </summary>
    public static class CoopParrelSync
    {
        const string CloneMarkerFile = ".clone";
        const string SharedMatchFileName = ".coop-match";

        public static bool IsCloneEditor
        {
            get
            {
                var root = ProjectRoot;
                return !string.IsNullOrEmpty(root) &&
                       File.Exists(Path.Combine(root, CloneMarkerFile));
            }
        }

        public static string RoleLabel => IsCloneEditor ? "Client (clone)" : "Host";

        public static string SessionPrefsKey(BlockchainConfig config)
        {
            if (IsCloneEditor)
                return "pirate_session_key_clone";
            if (config != null && !string.IsNullOrWhiteSpace(config.sessionKeyPrefsId))
                return $"pirate_session_key_{config.sessionKeyPrefsId}";
            return "pirate_session_key_host";
        }

        public static string ResolvePayerPath(BlockchainConfig config)
        {
            if (IsCloneEditor &&
                config != null &&
                !string.IsNullOrWhiteSpace(config.localClientPayerKeypairPath))
            {
                return Environment.ExpandEnvironmentVariables(config.localClientPayerKeypairPath);
            }

            if (config != null && !string.IsNullOrWhiteSpace(config.localPayerKeypairPath))
                return Environment.ExpandEnvironmentVariables(config.localPayerKeypairPath);

            return null;
        }

        public static void PublishMatchAddress(string matchPubkey)
        {
            if (string.IsNullOrWhiteSpace(matchPubkey)) return;
            var path = SharedMatchFilePath;
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
            File.WriteAllText(path, matchPubkey.Trim());
            Debug.Log($"[Coop] Published match for clone: {matchPubkey} → {path}");
        }

        public static string ReadSharedMatchAddress()
        {
            var path = SharedMatchFilePath;
            if (!File.Exists(path)) return null;
            var text = File.ReadAllText(path).Trim();
            return string.IsNullOrEmpty(text) ? null : text;
        }

        static string ProjectRoot => Directory.GetParent(Application.dataPath)?.FullName;

        /// <summary>Repo-root file both host and clone editors can read (../../ from Assets).</summary>
        static string SharedMatchFilePath =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", SharedMatchFileName));
    }
}
