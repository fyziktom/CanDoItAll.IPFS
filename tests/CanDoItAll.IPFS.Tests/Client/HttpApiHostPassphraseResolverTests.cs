#nullable enable

using System;
using Ipfs.Server;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ipfs.Engine.ClientTests
{
    [TestClass]
    public sealed class HttpApiHostPassphraseResolverTests
    {
        [TestMethod]
        public void Configured_Passphrase_Takes_Precedence_Over_Environment()
        {
            var options = new HttpApiHostOptions
            {
                Passphrase = "  configured-secret  "
            };

            var resolved = HttpApiHostPassphraseResolver.ResolvePassphrase(options, environmentPassphrase: "environment-secret");

            Assert.AreEqual("configured-secret", resolved);
        }

        [TestMethod]
        public void Environment_Passphrase_Is_Used_When_Configured_Value_Is_Missing()
        {
            var resolved = HttpApiHostPassphraseResolver.ResolvePassphrase(
                new HttpApiHostOptions(),
                environmentPassphrase: "  environment-secret  ");

            Assert.AreEqual("environment-secret", resolved);
        }

        [TestMethod]
        public void Missing_Passphrase_Throws_A_Clear_Error()
        {
            var error = Throws<InvalidOperationException>(() =>
                HttpApiHostPassphraseResolver.ResolveRequiredPassphrase(
                    new HttpApiHostOptions(),
                    environmentPassphrase: null));

            StringAssert.Contains(error.Message, HttpApiHostPassphraseResolver.EnvironmentVariableName);
            StringAssert.Contains(error.Message, $"{HttpApiHostOptions.SectionName}:Passphrase");
        }

        private static T Throws<T>(Action action)
            where T : Exception
        {
            try
            {
                action();
            }
            catch (T error)
            {
                return error;
            }

            Assert.Fail($"Exception of type {typeof(T)} should be thrown.");
            return null!;
        }
    }
}
