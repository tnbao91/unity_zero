using System;
using System.Text;
using UnityEngine;

namespace Zero.Services.Save
{
    /// <summary>
    /// ScriptableObject holding per-game encryption seeds for the EncryptedJsonSaveService.
    /// Must be placed at Assets/Resources/ZeroSecrets.asset before shipping player builds.
    /// Copy ZeroSecrets.asset.example into place and replace the placeholder markers with per-game secrets.
    /// </summary>
    [CreateAssetMenu(menuName = "Zero/Zero Secrets")]
    public sealed class ZeroSecrets : ScriptableObject
    {
        private const string PlaceholderMarker = "REPLACE_BEFORE_SHIPPING";

        [SerializeField]
        private string _aesSeed = PlaceholderMarker;

        [SerializeField]
        private string _hmacSeed = PlaceholderMarker;

        /// <summary>
        /// AES seed as UTF8-encoded bytes. Derived from the string field to support per-game configuration in Inspector.
        /// </summary>
        public byte[] AesSeed => Encoding.UTF8.GetBytes(_aesSeed);

        /// <summary>
        /// HMAC seed as UTF8-encoded bytes. Derived from the string field to support per-game configuration in Inspector.
        /// </summary>
        public byte[] HmacSeed => Encoding.UTF8.GetBytes(_hmacSeed);

        /// <summary>
        /// Returns true if either seed still contains the placeholder marker, indicating this asset has not been configured.
        /// </summary>
        public bool IsPlaceholder => _aesSeed.Contains(PlaceholderMarker) || _hmacSeed.Contains(PlaceholderMarker);
    }
}
