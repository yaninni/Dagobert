using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using Dagobert.Utilities;

namespace Dagobert
{
    public enum DelayStrategy
    {
        Uniform,
        Gaussian,
        LogNormal,
        Bimodal
    }

    public static class RandomDelayGenerator
    {
        public static int LastGeneratedDelay { get; private set; } = 0;
        public static List<float> History { get; private set; } = new List<float>();
        private const int MaxHistorySize = 100;
        private static readonly RandomNumberGenerator Cng = RandomNumberGenerator.Create();

        public static int HumanizationScore
        {
            get
            {
                int count = History.Count;
                if (count < 10) return 50;

                double sum = 0;
                for (int i = 0; i < count; i++) sum += History[i];
                double mean = sum / count;
                
                if (mean == 0) return 0;

                double sumSquares = 0;
                for (int i = 0; i < count; i++)
                {
                    double diff = (double)History[i] - mean;
                    sumSquares += diff * diff;
                }
                
                double stdDev = Math.Sqrt(sumSquares / count);
                double cv = stdDev / mean;
                double score = Math.Min(cv, 0.5) / 0.25 * 100.0;

                if (cv < 0.10) score *= 0.5;

                return Math.Clamp((int)score, 0, 100);
            }
        }

        public static void RecordDelay(int ms)
        {
            LastGeneratedDelay = ms;
            History.Add((float)ms);
            if (History.Count > MaxHistorySize) History.RemoveAt(0);
        }

        public static int GetRandomDelay(int min, int max, DelayStrategy strategy)
        {
            if (min >= max)
            {
                RecordDelay(min);
                return min;
            }

            int result = strategy switch
            {
                DelayStrategy.Gaussian => NextGaussian(min, max),
                DelayStrategy.LogNormal => NextLogNormal(min, max),
                DelayStrategy.Bimodal => NextBimodal(min, max),
                _ => NextUniform(min, max),
            };

            RecordDelay(result);
            return result;
        }

        private static int NextUniform(int min, int max)
        {
            Span<byte> data = stackalloc byte[8];
            Cng.GetBytes(data);
            var ul = BitConverter.ToUInt64(data);
            ulong range = (ulong)(max - min + 1);
            return min + (int)(ul % range);
        }

        private static int NextGaussian(int min, int max)
        {
            double mean = (min + max) / 2.0;
            double stdDev = (max - min) / 6.0;
            return MathUtils.ClampAndCast(mean + stdDev * MathUtils.GenerateGaussianSample(mean, stdDev, GetNextDouble), min, max);
        }

        private static int NextLogNormal(int min, int max)
        {
            double u1 = 1.0 - GetNextDouble();
            double u2 = 1.0 - GetNextDouble();
            double standardNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
            double mu = 0;
            double sigma = 0.5;
            double logNormalValue = Math.Exp(mu + sigma * standardNormal);
            double normalized = logNormalValue / 4.0;
            double range = max - min;
            return MathUtils.ClampAndCast(min + (range * normalized), min, max);
        }

        private static int NextBimodal(int min, int max)
        {
            double modeSelector = GetNextDouble();
            double range = max - min;
            double result;

            if (modeSelector < 0.70)
            {
                double mean = min + (range * 0.25);
                double stdDev = range / 8.0;
                result = MathUtils.GenerateGaussianSample(mean, stdDev, GetNextDouble);
            }
            else
            {
                double mean = min + (range * 0.75);
                double stdDev = range / 6.0;
                result = MathUtils.GenerateGaussianSample(mean, stdDev, GetNextDouble);
            }
            return MathUtils.ClampAndCast(result, min, max);
        }

        private static double GetNextDouble()
        {
            Span<byte> data = stackalloc byte[8];
            Cng.GetBytes(data);
            return BitConverter.ToUInt64(data) / (double)ulong.MaxValue;
        }
    }
}