using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Game.Backend
{
    /// <summary>
    /// Signs a run submission so the endpoint is not trivially forgeable with curl.
    ///
    /// BE HONEST WITH YOURSELF ABOUT WHAT THIS BUYS. The secret ships inside the
    /// build. Anyone willing to open the binary in dnSpy will find it in minutes
    /// and can then forge any score they like. This raises the cost of cheating
    /// from "paste a URL into a terminal" to "reverse the client", which filters
    /// out essentially all of the casual attempts and none of the determined
    /// ones. The rules in backend/src/lib/antiCheat.ts are the actual defence,
    /// because they trust none of these numbers.
    /// </summary>
    public static class RunSigner
    {
        /// <summary>
        /// Builds the exact string the server re-derives and checks.
        ///
        /// CONTRACT: this must match runSignaturePayload() in
        /// backend/src/lib/crypto.ts, field for field and separator for
        /// separator. Both sides have a test pinning the same example string;
        /// if you change the format, change both or every submission 401s.
        /// </summary>
        public static string BuildPayload(
            string runId, string playerId, string missionId, int score, int durationMs, int kills)
        {
            // Invariant culture: a locale that formats integers with a thousands
            // separator would produce "4,200" here and nowhere on the server.
            return string.Join("|",
                runId,
                playerId,
                missionId,
                score.ToString(CultureInfo.InvariantCulture),
                durationMs.ToString(CultureInfo.InvariantCulture),
                kills.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>HMAC-SHA256 of <paramref name="payload"/>, lowercase hex.</summary>
        public static string Sign(string secret, string payload)
        {
            if (secret == null) throw new ArgumentNullException(nameof(secret));
            if (payload == null) throw new ArgumentNullException(nameof(payload));

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));

            var builder = new StringBuilder(hash.Length * 2);
            foreach (byte b in hash) builder.Append(b.ToString("x2", CultureInfo.InvariantCulture));
            return builder.ToString();
        }
    }
}
