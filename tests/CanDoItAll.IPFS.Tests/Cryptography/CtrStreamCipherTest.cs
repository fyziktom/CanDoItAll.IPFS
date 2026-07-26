using Microsoft.VisualStudio.TestTools.UnitTesting;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Parameters;
using System;

namespace PeerTalk.Cryptography
{
    [TestClass]
    public class CtrStreamCipherTest
    {
        [TestMethod]
        public void Reset_ReplaysTheInitializedCounter()
        {
            var cipher = new CtrStreamCipher(new AesEngine());
            var parameters = new ParametersWithIV(
                new KeyParameter(Convert.FromHexString("000102030405060708090A0B0C0D0E0F")),
                Convert.FromHexString("101112131415161718191A1B"));
            var plaintext = Convert.FromHexString("202122232425262728292A2B2C2D2E2F30313233");
            var firstPass = new byte[plaintext.Length];
            var secondPass = new byte[plaintext.Length];

            cipher.Init(true, parameters);
            cipher.ProcessBytes(plaintext.AsSpan(), firstPass.AsSpan());
            cipher.Reset();
            cipher.ProcessBytes(plaintext.AsSpan(), secondPass.AsSpan());

            CollectionAssert.AreEqual(firstPass, secondPass);
            CollectionAssert.AreNotEqual(plaintext, firstPass);
        }
    }
}
