using System;
using NUnit.Framework;

namespace Glitchr.Licensing.Tests
{
    /// <summary>
    /// The offline check: Ed25519 against RFC 8032's test vectors, then a token as the forge
    /// signs it (PHP's sodium, omnibase/forge's LicenseToken::issue(), a fixed key pair).
    /// </summary>
    public class LicenseTokenTests
    {
        private const string ForgePublicKey = "6kpsY+KcUgq+9VB7Ey7F+ZVHdq6+vnuSQh7qaRRG0iw=";
        private const string ForgeToken = "eyJ2IjoxLCJsaWNlbnNlIjo0Miwic29mdHdhcmUiOiJwcm9iZS1nYW1lIiwiZW1haWwiOiJhZGFAZXhhbXBsZS5vcmciLCJtYWNoaW5lIjoiMzE3ZjA2Zjc3YmY3MmUwMmNiNjgyYzM2NWZkNWViMDQwMGM2NDc5NTA2NTI4ZDc0NWY1ZWMzZjIzYjA0Y2FhMSIsImlzc3VlZF9hdCI6MTc5Mjk3MjgwMCwidmFsaWRfdW50aWwiOjE3OTU1NjQ4MDAsImxpY2Vuc2VfZXhwaXJlcyI6bnVsbCwidXBkYXRlc191bnRpbCI6MTgyNDUwODgwMH0.lQZSS34z_XRwGVE22WJux9fWE4SrIKtQ6tJa0rnhdZTOHlNsWkPPL37Tj8ML_xd6FuW2FHBVWS7DZUm54rA3AA";

        [TestCase("d75a980182b10ab7d54bfed3c964073a0ee172f3daa62325af021a68f707511a", "", "e5564300c360ac729086e2cc806e828a84877f1eb8e5d974d873e065224901555fb8821590a33bacc61e39701cf9b46bd25bf5f0595bbe24655141438e7a100b")]
        [TestCase("3d4017c3e843895a92b70aa74d1b7ebc9c982ccf2ec4968cc0cd55f12af4660c", "72", "92a009a9f0d4cab8720e820b5f642540a2b27b5416503f8fb3762223ebdb69da085ac1e43e15996e458f3613d0f11d8c387b2eaeb4302aeeb00d291612bb0c00")]
        public void Rfc8032VectorsVerify(string publicKey, string message, string signature)
        {
            Assert.IsTrue(Ed25519.Verify(Hex(signature), Hex(message), Hex(publicKey)));

            byte[] tampered = Hex(signature);
            tampered[10] ^= 1;
            Assert.IsFalse(Ed25519.Verify(tampered, Hex(message), Hex(publicKey)), "a signature changed by one bit");
            Assert.IsFalse(Ed25519.Verify(Hex(signature), Hex(message + "00"), Hex(publicKey)), "another message");
        }

        [Test]
        public void AForgeTokenIsReadAndChecked()
        {
            LicensePayload payload = LicenseToken.Verify(ForgeToken, ForgePublicKey);
            Assert.IsNotNull(payload, "the forge's signature");
            Assert.AreEqual(1, payload.v);
            Assert.AreEqual(42, payload.license);
            Assert.AreEqual("probe-game", payload.software);
            Assert.AreEqual("ada@example.org", payload.email);
            Assert.AreEqual(1795564800, payload.valid_until);
            Assert.AreEqual(0, payload.license_expires, "null: perpetual");
            Assert.AreEqual(1824508800, payload.updates_until);
            Assert.AreEqual(LicenseToken.MachineHash("machine-0001"), payload.machine);

            DateTimeOffset before = DateTimeOffset.FromUnixTimeSeconds(1792972900);
            Assert.IsNotNull(LicenseToken.Check(ForgeToken, ForgePublicKey, "probe-game", "machine-0001", before));
            Assert.IsNull(LicenseToken.Check(ForgeToken, ForgePublicKey, "another-game", "machine-0001", before), "another software");
            Assert.IsNull(LicenseToken.Check(ForgeToken, ForgePublicKey, "probe-game", "machine-0002", before), "another machine");
            Assert.IsNull(LicenseToken.Check(ForgeToken, ForgePublicKey, "probe-game", "machine-0001", DateTimeOffset.FromUnixTimeSeconds(1795564800)), "past valid_until");
            Assert.IsFalse(payload.RunsOutWithin(TimeSpan.FromDays(7), before), "a month left");
            Assert.IsTrue(payload.RunsOutWithin(TimeSpan.FromDays(7), DateTimeOffset.FromUnixTimeSeconds(1795564800 - 86400)), "a day left");
        }

        [Test]
        public void ATokenIsRefusedWhenAnythingIsChanged()
        {
            string[] parts = ForgeToken.Split('.');
            string forged = Base64Url.Encode(System.Text.Encoding.UTF8.GetBytes(System.Text.Encoding.UTF8.GetString(Base64Url.Decode(parts[0])).Replace("\"license\":42", "\"license\":43"))) + "." + parts[1];
            Assert.IsNull(LicenseToken.Verify(forged, ForgePublicKey), "a payload changed");
            Assert.IsNull(LicenseToken.Verify(ForgeToken, "d75a980182b10ab7d54bfed3c964073a0ee172f3daa62325af021a68f707511a"), "not base64: no key");
            Assert.IsNull(LicenseToken.Verify(ForgeToken, Convert.ToBase64String(Hex("d75a980182b10ab7d54bfed3c964073a0ee172f3daa62325af021a68f707511a"))), "another key");
            Assert.IsNull(LicenseToken.Verify("not-a-token", ForgePublicKey));
            Assert.IsNull(LicenseToken.Verify(null, ForgePublicKey));
        }

        private static byte[] Hex(string hex)
        {
            var bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = Convert.ToByte(hex.Substring(2 * i, 2), 16);
            }

            return bytes;
        }
    }
}
