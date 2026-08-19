#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using PirateGame.Net;
using ParrelSync;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// ParrelSync two-editor co-op setup for Pirate Game local ER testing.
/// Menu: Pirate Game → Co-op → ParrelSync → …
/// </summary>
public static class CoopParrelSyncSetup
{
    const string ConfigPath = "Assets/Resources/BlockchainConfig.asset";
    const string WslHostKey = @"\\wsl$\Ubuntu\home\shara\.config\solana\id.json";
    const string WslClientKey = @"\\wsl$\Ubuntu\home\shara\.config\solana\player2.json";
    const string SetupScript = "onchain/scripts/setup-parrelsync-client.sh";

    [MenuItem("Pirate Game/Co-op/ParrelSync/1. Setup Wallets + Config (Local)")]
    static void SetupWalletsAndConfigLocal()
    {
        RunWslSetupScript(BlockchainConfig.NetworkMode.Local);
        var config = EnsureConfig(BlockchainConfig.NetworkMode.Local);
        ShowWalletDialog(config, "Local");
    }

    [MenuItem("Pirate Game/Co-op/ParrelSync/1b. Setup Wallets + Config (Devnet)")]
    static void SetupWalletsAndConfigDevnet()
    {
        RunWslSetupScript(BlockchainConfig.NetworkMode.Devnet);
        var config = EnsureConfig(BlockchainConfig.NetworkMode.Devnet);
        ShowWalletDialog(config, "Devnet");
    }

    static void ShowWalletDialog(BlockchainConfig config, string networkLabel)
    {
        EditorUtility.DisplayDialog(
            $"ParrelSync wallets ({networkLabel})",
            "Host wallet: id.json\n" +
            "Client wallet: player2.json\n\n" +
            $"Network: {config.mode}\n" +
            $"Base RPC: {config.BaseRpc}\n\n" +
            $"Host path:\n{config.localPayerKeypairPath}\n\n" +
            $"Client path:\n{config.localClientPayerKeypairPath}\n\n" +
            "Next: Pirate Game → Co-op → ParrelSync → 2. Create Clone Project",
            "OK");
    }

    [MenuItem("Pirate Game/Co-op/ParrelSync/2. Create Clone Project")]
    static void CreateClone()
    {
        if (ClonesManager.IsClone())
        {
            EditorUtility.DisplayDialog("ParrelSync", "Open the original project (not the clone) to create a clone.", "OK");
            return;
        }

        SetupWalletsAndConfigSilent(BlockchainConfig.NetworkMode.Local);
        var clone = ClonesManager.CreateCloneFromCurrent();
        if (clone == null)
        {
            EditorUtility.DisplayDialog("ParrelSync", "Clone creation failed. Check the Console.", "OK");
            return;
        }

        Debug.Log($"[Coop] ParrelSync clone created at {clone.projectPath}");
        if (EditorUtility.DisplayDialog(
                "ParrelSync clone created",
                $"Clone:\n{clone.projectPath}\n\nOpen the clone in a second Unity Editor now?",
                "Open clone",
                "Later"))
        {
            ClonesManager.OpenProject(clone.projectPath);
        }
    }

    [MenuItem("Pirate Game/Co-op/ParrelSync/3. Open Clone Project")]
    static void OpenClone()
    {
        if (ClonesManager.IsClone())
        {
            EditorUtility.DisplayDialog("ParrelSync", "You are already in the clone project.", "OK");
            return;
        }

        var current = ClonesManager.GetCurrentProjectPath();
        for (var i = 0; i < 10; i++)
        {
            var candidate = current + ClonesManager.CloneNameSuffix + "_" + i;
            if (Directory.Exists(candidate))
            {
                ClonesManager.OpenProject(candidate);
                return;
            }
        }

        EditorUtility.DisplayDialog(
            "ParrelSync",
            "No clone found. Run\nPirate Game → Co-op → ParrelSync → 2. Create Clone Project",
            "OK");
    }

    [MenuItem("Pirate Game/Co-op/ParrelSync/Show Co-op Instructions")]
    static void ShowInstructions()
    {
        EditorUtility.DisplayDialog(
            "ParrelSync co-op test",
            "Prerequisites:\n" +
            "• BlockchainConfig wallets set (menu 1 or 1b)\n" +
            "• Devnet: both id.json + player2.json funded on devnet\n" +
            "• Local: mb-test-validator + ephemeral-validator running\n" +
            "• Solana Test Scene has CoopBootstrap\n\n" +
            "Steps (lobby UI):\n" +
            "1. ORIGINAL editor (Host): Play → Host Room → Copy Room Code\n" +
            "2. CLONE editor (Client): Play → Join Host Room\n" +
            "   (auto-reads .coop-match at repo root)\n" +
            "3. Host: Start Match when 2 players joined\n" +
            "4. Both steer with WASD (each wallet = one ship)\n\n" +
            "Keyboard fallback: U host | O join | P start",
            "OK");
    }

    static BlockchainConfig EnsureConfig(BlockchainConfig.NetworkMode mode)
    {
        var config = AssetDatabase.LoadAssetAtPath<BlockchainConfig>(ConfigPath);
        if (config == null)
            throw new InvalidOperationException($"Missing {ConfigPath}");

        Undo.RecordObject(config, "Configure ParrelSync wallets");
        config.mode = mode;
        config.localPayerKeypairPath = WslHostKey;
        config.localClientPayerKeypairPath = WslClientKey;
        config.sessionKeyPrefsId = "host";
        EditorUtility.SetDirty(config);
        AssetDatabase.SaveAssets();
        return config;
    }

    static void SetupWalletsAndConfigSilent(BlockchainConfig.NetworkMode mode)
    {
        var config = AssetDatabase.LoadAssetAtPath<BlockchainConfig>(ConfigPath);
        RunWslSetupScript(mode, config);
        EnsureConfig(mode);
    }

    static void RunWslSetupScript(BlockchainConfig.NetworkMode mode, BlockchainConfig config = null)
    {
        config ??= AssetDatabase.LoadAssetAtPath<BlockchainConfig>(ConfigPath);
        var rpc = mode == BlockchainConfig.NetworkMode.Devnet
            ? (string.IsNullOrWhiteSpace(config?.devnetBaseRpc) ? "https://api.devnet.solana.com" : config.devnetBaseRpc)
            : "http://localhost:8899";
        var repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
        var script = Path.Combine(repoRoot, SetupScript.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(script))
        {
            Debug.LogWarning($"[Coop] Missing setup script: {script}");
            return;
        }

        var wslScript = ToWslPath(script);
        Debug.Log($"[Coop] Running: wsl bash \"{wslScript}\" (RPC={rpc})");
        var psi = new ProcessStartInfo("wsl", $"bash -lc \"export SOLANA_URL='{rpc}'; bash \\\"{wslScript}\\\"\"")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        using var proc = Process.Start(psi);
        if (proc == null) return;
        var stdout = proc.StandardOutput.ReadToEnd();
        var stderr = proc.StandardError.ReadToEnd();
        proc.WaitForExit(120_000);
        if (!string.IsNullOrWhiteSpace(stdout))
            Debug.Log(stdout.Trim());
        if (!string.IsNullOrWhiteSpace(stderr))
            Debug.LogWarning(stderr.Trim());
        if (proc.ExitCode != 0)
            Debug.LogWarning($"[Coop] setup-parrelsync-client.sh exited {proc.ExitCode}");
    }

    /// <summary>Convert C:\foo\bar to /mnt/c/foo/bar for WSL.</summary>
    static string ToWslPath(string windowsPath)
    {
        var full = Path.GetFullPath(windowsPath);
        if (full.Length >= 2 && full[1] == ':')
        {
            var drive = char.ToLowerInvariant(full[0]);
            var rest = full.Substring(2).Replace('\\', '/');
            return $"/mnt/{drive}{rest}";
        }
        return full.Replace('\\', '/');
    }
}
#endif
