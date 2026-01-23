using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

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
                if (History.Count < 10) return 50;

                double mean = History.Average();
                if (mean == 0) return 0;

                double sumSquares = History.Sum(val => (val - mean) * (val - mean));
                double stdDev = Math.Sqrt(sumSquares / History.Count);
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
            var data = new byte[8];
            Cng.GetBytes(data);
            var ul = BitConverter.ToUInt64(data, 0);
            ulong range = (ulong)(max - min + 1);
            return min + (int)(ul % range);
        }

        private static int NextGaussian(int min, int max)
        {
            double u1 = 1.0 - GetNextDouble();
            double u2 = 1.0 - GetNextDouble();
            double randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
            double mean = (min + max) / 2.0;
            double stdDev = (max - min) / 6.0;
            return ClampAndCast(mean + stdDev * randStdNormal, min, max);
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
            return ClampAndCast(min + (range * normalized), min, max);
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
                result = GenerateGaussianSample(mean, stdDev);
            }
            else
            {
                double mean = min + (range * 0.75);
                double stdDev = range / 6.0;
                result = GenerateGaussianSample(mean, stdDev);
            }
            return ClampAndCast(result, min, max);
        }

        private static double GenerateGaussianSample(double mean, double stdDev)
        {
            double u1 = 1.0 - GetNextDouble();
            double u2 = 1.0 - GetNextDouble();
            double randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
            return mean + stdDev * randStdNormal;
        }

        private static int ClampAndCast(double value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return (int)value;
        }

        private static double GetNextDouble()
        {
            var data = new byte[8];
            Cng.GetBytes(data);
            return BitConverter.ToUInt64(data, 0) / (double)ulong.MaxValue;
        }
    }
}