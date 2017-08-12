using System;
using System.Globalization;
using System.Numerics;
using Windows.Security.Cryptography;
using Windows.Security.Cryptography.Core;
using Windows.Storage.Streams;

namespace LessPass
{
    /// <summary>
    ///   Note: this class attempts to replicate what's done in https://github.com/lesspass/core/blob/master/src/v2.js,
    ///   without attempting to improve anything.
    /// </summary>
    public static class Generator
    {
        public const string LowercaseLetters = "abcdefghijklmnopqrstuvwxyz";
        public const string UppercaseLetters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        public const string Symbols = "!\"#$%&'()*+,-./:;<=>?@[\\]^_`{|}~";
        public const string Numbers = "0123456789";

        public enum Algorithms
        {
            Sha256,
            Sha384,
            Sha512
        }

        [Flags]
        public enum CharSets
        {
            None = 0,

            Uppercase = 1,
            Lowercase = 2,
            Numbers = 4,
            Symbols = 8,

            Letters = Uppercase | Lowercase,
            All = Letters | Numbers | Symbols
        }

        public static char[] GetCharacterSet(CharSets sets, out char[][] allSets)
        {
            int length = 0;
            int allSetsLength = 0;

            // Compute length in advance
            if ((sets & CharSets.Lowercase) == CharSets.Lowercase)
            {
                length += 26;
                allSetsLength++;
            }
            if ((sets & CharSets.Uppercase) == CharSets.Uppercase)
            {
                length += 26;
                allSetsLength++;
            }
            if ((sets & CharSets.Numbers) == CharSets.Numbers)
            {
                length += 10;
                allSetsLength++;
            }
            if ((sets & CharSets.Symbols) == CharSets.Symbols)
            {
                length += 32;
                allSetsLength++;
            }

            // Make string
            int index = 0;
            int setIndex = 0;
            char[] buffer = new char[length];
            allSets = new char[allSetsLength][];

            if ((sets & CharSets.Lowercase) == CharSets.Lowercase)
            {
                LowercaseLetters.CopyTo(0, buffer, 0, 26);
                index += 26;

                allSets[setIndex++] = LowercaseLetters.ToCharArray();
            }

            if ((sets & CharSets.Uppercase) == CharSets.Uppercase)
            {
                UppercaseLetters.CopyTo(0, buffer, index, 26);
                index += 26;

                allSets[setIndex++] = UppercaseLetters.ToCharArray();
            }

            if ((sets & CharSets.Numbers) == CharSets.Numbers)
            {
                Numbers.CopyTo(0, buffer, index, 10);
                index += 10;

                allSets[setIndex++] = Numbers.ToCharArray();
            }

            if ((sets & CharSets.Symbols) == CharSets.Symbols)
            {
                Symbols.CopyTo(0, buffer, index, 32);

                allSets[setIndex] = Symbols.ToCharArray();
            }

            return buffer;
        }

        public static string GenerateEntropy(string password, string salt, CharSets sets,
            uint keySize, Algorithms digest, int length, uint iterations)
        {
            string algorithm;

            switch (digest)
            {
                case Algorithms.Sha256:
                    algorithm = KeyDerivationAlgorithmNames.Pbkdf2Sha256;
                    break;
                case Algorithms.Sha384:
                    algorithm = KeyDerivationAlgorithmNames.Pbkdf2Sha384;
                    break;
                case Algorithms.Sha512:
                    algorithm = KeyDerivationAlgorithmNames.Pbkdf2Sha512;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            KeyDerivationAlgorithmProvider provider = KeyDerivationAlgorithmProvider.OpenAlgorithm(algorithm);

            // This is our secret password
            IBuffer buffSecret = CryptographicBuffer.ConvertStringToBinary(password, BinaryStringEncoding.Utf8);

            // Use the provided salt
            IBuffer buffSalt = CryptographicBuffer.ConvertStringToBinary(salt, BinaryStringEncoding.Utf8);

            // Create the derivation parameters.
            KeyDerivationParameters pbkdf2Params = KeyDerivationParameters.BuildForPbkdf2(buffSalt, iterations);

            // Create a key from the secret value.
            CryptographicKey keyOriginal = provider.CreateKey(buffSecret);

            // Derive a key based on the original key and the derivation parameters.
            IBuffer keyDerived = CryptographicEngine.DeriveKeyMaterial(
                keyOriginal,
                pbkdf2Params,
                keySize
            );

            // Return the buffer as a hex string
            return CryptographicBuffer.EncodeToHexString(keyDerived);
        }

        public static string RenderPassword(CharSets sets, string entropy, int maxLength)
        {
            // Compute password
            BigInteger quotient = BigInteger.Parse('0' + entropy, NumberStyles.AllowHexSpecifier);
            char[] possibleChars = GetCharacterSet(sets, out var allSets);
            int setsCount = allSets.Length;
            maxLength -= setsCount;

            char[] passwordChars = new char[maxLength];
            int charsLength = possibleChars.Length;

            for (int i = 0; i < maxLength; i++)
            {
                quotient = BigInteger.DivRem(quotient, charsLength, out BigInteger remainder);
                passwordChars[i] = possibleChars[(int)remainder];
            }

            // Add one char per rule
            char[] additionalChars = new char[setsCount];

            for (int i = 0; i < setsCount; i++)
            {
                char[] set = allSets[i];

                quotient = BigInteger.DivRem(quotient, set.Length, out BigInteger remainder);
                additionalChars[i] = set[(int)remainder];
            }

            // Randomly distribute rule-chars into password-chars
            string password = new string(passwordChars);

            for (int i = 0; i < setsCount; i++)
            {
                quotient = BigInteger.DivRem(quotient, password.Length, out BigInteger remainder);

                int iRem = (int)remainder;

                password = string.Concat(
                    password.Substring(0, iRem), additionalChars[i].ToString(), password.Substring(iRem)
                );
            }

            return password;
        }

        public static string Generate(string password, string salt, CharSets sets = CharSets.All,
            uint keySize = 32, Algorithms digest = Algorithms.Sha256, int length = 16, uint iterations = 100_000)
        {
            return RenderPassword(sets, GenerateEntropy(password, salt, sets, keySize, digest, length, iterations), length);
        }
    }
}
