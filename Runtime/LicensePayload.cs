using System;

namespace Glitchr.Licensing
{
    /// <summary>
    /// What a licence token says, as the forge signs it (omnibase/forge, LicenseToken). The times
    /// are UNIX seconds; 0 where the forge wrote null (a perpetual licence, every update).
    /// </summary>
    [Serializable]
    public class LicensePayload
    {
        /// <summary>The token's format: 1.</summary>
        public int v;

        /// <summary>The licence's id on the forge.</summary>
        public long license;

        /// <summary>The software's slug: a token for another software is refused.</summary>
        public string software;

        /// <summary>The seat's e-mail.</summary>
        public string email;

        /// <summary>SHA-256 (hex) of the machine identifier it was activated for.</summary>
        public string machine;

        public long issued_at;

        /// <summary>Past it, activate again, online.</summary>
        public long valid_until;

        /// <summary>When the licence itself ends; 0: perpetual.</summary>
        public long license_expires;

        /// <summary>The releases published until then are covered; 0: every one.</summary>
        public long updates_until;

        public DateTimeOffset ValidUntil => DateTimeOffset.FromUnixTimeSeconds(valid_until);

        /// <summary>Whether a release published at <paramref name="releasedAt"/> is covered by its updates.</summary>
        public bool Covers(DateTimeOffset releasedAt)
        {
            return updates_until == 0 || releasedAt.ToUnixTimeSeconds() <= updates_until;
        }
    }
}
