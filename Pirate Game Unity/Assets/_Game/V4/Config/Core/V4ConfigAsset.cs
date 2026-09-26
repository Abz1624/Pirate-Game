using UnityEngine;

namespace RoguePirates.V4.Config
{
    /// <summary>
    /// Base for every V4 config asset. Holds identity and versions; subclasses add plain serialized data (no
    /// behavior) and their own validation rules. Content hashes come from <see cref="CanonicalConfigWriter"/>.
    /// </summary>
    public abstract class V4ConfigAsset : ScriptableObject
    {
        [Tooltip("Stable id; presets, traces and the console refer to the asset by it.")]
        [ConfigIdentity] public string configId;
        [Tooltip("Bumped when fields are added, renamed or change meaning; old presets are migrated or rejected.")]
        public int schemaVersion = V4ConfigInfo.SchemaVersion;
        [Tooltip("Designer-facing balance label, stamped into run headers.")]
        public string balanceVersion = "saltwind-v3-parity";

        /// <summary>Adds rule violations for this asset. Called by <see cref="ConfigValidator"/>.</summary>
        public virtual void Validate(ConfigValidationReport report) { }

        /// <summary>Content hash of the current field values (canonical JSON, SHA-256).</summary>
        public string ComputeHash() => CanonicalConfigWriter.Hash(this);
    }
}
