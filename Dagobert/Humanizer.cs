using System;

namespace Dagobert
{
    public static class Humanizer
    {
        private enum FocusState { HyperFocused, Casual, Distracted }
        private static FocusState _currentFocus = FocusState.Casual;
        public static void Reset() => _currentFocus = FocusState.Casual;
        public static int GetReactionDelay()
        {
            double u1 = 1.0 - Random.Shared.NextDouble();
            double u2 = 1.0 - Random.Shared.NextDouble();
            double randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
            return Math.Clamp((int)(250 + (40 * randStdNormal)), 180, 500);
        }
        public static int GetFittsDelay() => Random.Shared.Next(300, 600);
        public static int GetCognitiveDelay(int baseDelay)
        {
            if (Random.Shared.Next(100) < 5)
            {
                var vals = Enum.GetValues<FocusState>();
                _currentFocus = (FocusState)vals.GetValue(Random.Shared.Next(vals.Length))!;
            }

            float multiplier = _currentFocus switch
            {
                FocusState.HyperFocused => 0.7f,
                FocusState.Casual => 1.0f,
                FocusState.Distracted => 2.5f,
                _ => 1.0f
            };

            return (int)(baseDelay * multiplier);
        }
        public static int GetValueBasedDelay(int price)
        {
            return price <= 0 ? 0 : (int)(Math.Log10(price) * 200) + Random.Shared.Next(-100, 100);
        }
        public static int GetTypingDelay(int oldP, int newP)
        {
            string s1 = oldP.ToString();
            string s2 = newP.ToString();
            int diffs = 0;

            for (int i = 0; i < Math.Max(s1.Length, s2.Length); i++)
            {
                char c1 = i < s1.Length ? s1[i] : ' ';
                char c2 = i < s2.Length ? s2[i] : ' ';
                if (c1 != c2) diffs++;
            }

            return (diffs * 150) + 100;
        }

        public static bool ShouldTakeMicroBreak() => Random.Shared.Next(0, 40) == 0;
    }
}