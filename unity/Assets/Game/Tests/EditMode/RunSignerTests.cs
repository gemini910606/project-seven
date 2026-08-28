using NUnit.Framework;
using Game.Backend;

namespace Game.Tests
{
    public sealed class RunSignerTests
    {
        private const string RunId = "a7e70f32-2091-416c-8da9-1546b4dff1bb";
        private const string PlayerId = "7b1fe6a8-89ea-491f-b59f-1d2aa48bbe79";

        /// <summary>
        /// The single most important assertion in the client.
        ///
        /// backend/test/crypto.test.ts pins this exact string from the server
        /// side. As long as both tests pass, the two implementations agree; if
        /// either drifts, one test goes red instead of every player silently
        /// getting a 401 on submission.
        /// </summary>
        [Test]
        public void PayloadFormatMatchesTheServerContract()
        {
            string payload = RunSigner.BuildPayload(RunId, PlayerId, "dockside-raid", 4200, 360000, 14);

            Assert.AreEqual(
                "a7e70f32-2091-416c-8da9-1546b4dff1bb|7b1fe6a8-89ea-491f-b59f-1d2aa48bbe79|dockside-raid|4200|360000|14",
                payload);
        }

        [Test]
        public void SignatureIsLowercaseHexOfExpectedLength()
        {
            string signature = RunSigner.Sign("test-secret", "anything");

            // HMAC-SHA256 is 32 bytes, so 64 hex characters.
            Assert.AreEqual(64, signature.Length);
            StringAssert.IsMatch("^[0-9a-f]{64}$", signature);
        }

        [Test]
        public void SameInputProducesSameSignature()
        {
            string payload = RunSigner.BuildPayload(RunId, PlayerId, "m", 1, 2, 3);

            Assert.AreEqual(
                RunSigner.Sign("secret", payload),
                RunSigner.Sign("secret", payload));
        }

        [Test]
        public void DifferentSecretProducesDifferentSignature()
        {
            string payload = RunSigner.BuildPayload(RunId, PlayerId, "m", 1, 2, 3);

            Assert.AreNotEqual(
                RunSigner.Sign("secret-a", payload),
                RunSigner.Sign("secret-b", payload));
        }

        [Test]
        public void EditingTheScoreChangesTheSignature()
        {
            string honest = RunSigner.BuildPayload(RunId, PlayerId, "m", 4200, 360000, 14);
            string forged = RunSigner.BuildPayload(RunId, PlayerId, "m", 999999, 360000, 14);

            Assert.AreNotEqual(RunSigner.Sign("secret", honest), RunSigner.Sign("secret", forged));
        }
    }
}
