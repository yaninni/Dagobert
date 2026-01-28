using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Dagobert.Utilities;

public static class MathUtils
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetMedian(List<int> values)
    {
        if (values == null || values.Count == 0) return 0;
        
        var sorted = values.OrderBy(v => v).ToList();
        int count = sorted.Count;
        if (count % 2 == 1) return sorted[count / 2];
        return (sorted[count / 2 - 1] + sorted[count / 2]) / 2;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double GetMedian(List<double> values)
    {
        if (values == null || values.Count == 0) return 0;
        
        var sorted = values.OrderBy(v => v).ToList();
        int count = sorted.Count;
        if (count % 2 == 1) return sorted[count / 2];
        return (sorted[count / 2 - 1] + sorted[count / 2]) / 2;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ClampAndCast(double value, int min, int max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return (int)value;
    }

    public static double GenerateGaussianSample(double mean, double stdDev, Func<double> randomDoubleProvider)
    {
        double u1 = 1.0 - randomDoubleProvider();
        double u2 = 1.0 - randomDoubleProvider();
        double randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
        return mean + stdDev * randStdNormal;
    }
}
