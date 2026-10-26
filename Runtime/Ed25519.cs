using System;
using System.Numerics;
using System.Security.Cryptography;

namespace Glitchr.Licensing
{
    /// <summary>
    /// Ed25519 signature verification (RFC 8032, section 5.1.7) in managed code, for the
    /// platforms Unity builds for: no native library, nothing to sign with - a game only checks
    /// what the forge signed. Arithmetic on <see cref="BigInteger"/>: a verification takes a few
    /// milliseconds, once at start.
    /// </summary>
    public static class Ed25519
    {
        public const int PublicKeyLength = 32;
        public const int SignatureLength = 64;

        private static readonly BigInteger P = BigInteger.Pow(2, 255) - 19;
        private static readonly BigInteger L = BigInteger.Pow(2, 252) + BigInteger.Parse("27742317777372353535851937790883648493");
        private static readonly BigInteger D = Mod(-121665 * Inverse(121666));
        private static readonly BigInteger SqrtMinusOne = BigInteger.ModPow(2, (P - 1) / 4, P);
        private static readonly Point Base = MakeBase();

        /// <summary>Whether <paramref name="signature"/> is the signature of <paramref name="message"/> by the owner of <paramref name="publicKey"/>.</summary>
        public static bool Verify(byte[] signature, byte[] message, byte[] publicKey)
        {
            if (signature == null || message == null || publicKey == null || signature.Length != SignatureLength || publicKey.Length != PublicKeyLength)
            {
                return false;
            }

            Point? a = Decompress(publicKey, 0);
            Point? r = Decompress(signature, 0);
            if (a == null || r == null)
            {
                return false;
            }

            BigInteger s = LittleEndian(signature, 32, 32);
            if (s >= L)
            {
                return false;
            }

            byte[] digest;
            using (SHA512 sha = SHA512.Create())
            {
                byte[] input = new byte[32 + 32 + message.Length];
                Buffer.BlockCopy(signature, 0, input, 0, 32);
                Buffer.BlockCopy(publicKey, 0, input, 32, 32);
                Buffer.BlockCopy(message, 0, input, 64, message.Length);
                digest = sha.ComputeHash(input);
            }
            BigInteger h = LittleEndian(digest, 0, digest.Length) % L;

            return Equal(Multiply(s, Base), Add(r.Value, Multiply(h, a.Value)));
        }

        private readonly struct Point
        {
            public readonly BigInteger X, Y, Z, T;

            public Point(BigInteger x, BigInteger y, BigInteger z, BigInteger t)
            {
                X = x;
                Y = y;
                Z = z;
                T = t;
            }
        }

        private static Point MakeBase()
        {
            BigInteger y = Mod(4 * Inverse(5));
            BigInteger x = RecoverX(y, 0) ?? throw new InvalidOperationException("Ed25519 base point.");

            return new Point(x, y, 1, Mod(x * y));
        }

        private static Point Add(Point p, Point q)
        {
            BigInteger a = Mod((p.Y - p.X) * (q.Y - q.X));
            BigInteger b = Mod((p.Y + p.X) * (q.Y + q.X));
            BigInteger c = Mod(2 * p.T * q.T * D);
            BigInteger d = Mod(2 * p.Z * q.Z);
            BigInteger e = b - a, f = d - c, g = d + c, h = b + a;

            return new Point(Mod(e * f), Mod(g * h), Mod(f * g), Mod(e * h));
        }

        private static Point Multiply(BigInteger scalar, Point point)
        {
            Point result = new Point(0, 1, 1, 0);
            while (scalar > 0)
            {
                if (!scalar.IsEven)
                {
                    result = Add(result, point);
                }
                point = Add(point, point);
                scalar >>= 1;
            }

            return result;
        }

        private static bool Equal(Point p, Point q)
        {
            return Mod(p.X * q.Z - q.X * p.Z).IsZero && Mod(p.Y * q.Z - q.Y * p.Z).IsZero;
        }

        private static Point? Decompress(byte[] bytes, int offset)
        {
            byte[] copy = new byte[32];
            Buffer.BlockCopy(bytes, offset, copy, 0, 32);
            int sign = copy[31] >> 7;
            copy[31] &= 0x7F;
            BigInteger y = LittleEndian(copy, 0, 32);
            BigInteger? x = RecoverX(y, sign);
            if (x == null)
            {
                return null;
            }

            return new Point(x.Value, y, 1, Mod(x.Value * y));
        }

        private static BigInteger? RecoverX(BigInteger y, int sign)
        {
            if (y >= P)
            {
                return null;
            }
            BigInteger x2 = Mod((y * y - 1) * Inverse(D * y * y + 1));
            if (x2.IsZero)
            {
                return sign == 1 ? (BigInteger?)null : BigInteger.Zero;
            }

            BigInteger x = BigInteger.ModPow(x2, (P + 3) / 8, P);
            if (!Mod(x * x - x2).IsZero)
            {
                x = Mod(x * SqrtMinusOne);
            }
            if (!Mod(x * x - x2).IsZero)
            {
                return null;
            }
            if ((int)(x & 1) != sign)
            {
                x = P - x;
            }

            return x;
        }

        private static BigInteger Inverse(BigInteger x)
        {
            return BigInteger.ModPow(Mod(x), P - 2, P);
        }

        private static BigInteger Mod(BigInteger x)
        {
            BigInteger r = x % P;

            return r.Sign < 0 ? r + P : r;
        }

        /// <summary>An unsigned little-endian integer.</summary>
        private static BigInteger LittleEndian(byte[] bytes, int offset, int length)
        {
            byte[] unsigned = new byte[length + 1];
            Buffer.BlockCopy(bytes, offset, unsigned, 0, length);

            return new BigInteger(unsigned);
        }
    }
}
