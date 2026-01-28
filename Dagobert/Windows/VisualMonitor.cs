using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using ECommons.DalamudServices;

namespace Dagobert.Windows;

public enum ActivityType { Info, Pulse, Success, Error, Warning }

public record ActivitySignal(ActivityType Type, Vector4 Color, string Label, DateTime Timestamp);

public static class VisualMonitor
{
    private static readonly List<ActivitySignal> Signals = new();
    private static readonly object Lock = new();
    
    // Orbit State
    private static readonly List<RetainerOrbital> Orbitals = new();
    private static string? _activeRetainerName;
    private const int MaxPulses = 20;
    
    private class RetainerOrbital
    {
        public string Name = string.Empty;
        public float OrbitRadius;
        public float Speed;
        public float Angle;
        public Vector4 Color;
        public List<PulseSignal> Pulses = new();
    }

    private class PulseSignal
    {
        public float Progress;
        public Vector4 Color;
    }

    public static void SetActiveRetainer(string? name)
    {
        lock (Lock)
        {
            _activeRetainerName = name;
        }
    }

    public static void LogActivity(ActivityType type, string label)
    {
        lock (Lock)
        {
            var color = type switch
            {
                ActivityType.Success => new Vector4(0, 1, 0.5f, 1),
                ActivityType.Error => new Vector4(1, 0.2f, 0.2f, 1),
                ActivityType.Warning => new Vector4(1, 0.8f, 0, 1),
                ActivityType.Pulse => new Vector4(0.5f, 0.8f, 1, 1),
                _ => new Vector4(0.7f, 0.7f, 0.7f, 1)
            };
            
            Signals.Insert(0, new ActivitySignal(type, color, label, DateTime.Now));
            if (Signals.Count > 20) Signals.RemoveAt(Signals.Count - 1);

            if (type == ActivityType.Pulse || type == ActivityType.Success)
            {
                // Try to find target in label first
                var target = Orbitals.Find(o => label.Contains(o.Name, StringComparison.OrdinalIgnoreCase));
                
                // Fallback to active retainer if no specific target in label
                if (target == null && _activeRetainerName != null)
                {
                    target = Orbitals.Find(o => o.Name.Equals(_activeRetainerName, StringComparison.OrdinalIgnoreCase));
                }

                if (target != null)
                {
                    target.Pulses.Add(new PulseSignal { Progress = 0, Color = color });
                    // Limit pulses to prevent unbounded growth
                    if (target.Pulses.Count > MaxPulses)
                        target.Pulses.RemoveAt(0);
                }
            }
        }
    }

    public static void SyncRetainers(IEnumerable<string> names)
    {
        Orbitals.Clear();
        int count = 0;
        foreach (var name in names)
        {
            Orbitals.Add(new RetainerOrbital
            {
                Name = name,
                OrbitRadius = 80 + (count * 15),
                Speed = 0.5f + (count * 0.1f),
                Angle = count * (MathF.PI * 2 / 10),
                Color = new Vector4(0.4f, 0.6f, 1f, 0.8f)
            });
            count++;
        }
    }

    public static void Draw()
    {
        var drawList = ImGui.GetWindowDrawList();
        var windowPos = ImGui.GetWindowPos();
        var windowSize = ImGui.GetWindowSize();
        var avail = ImGui.GetContentRegionAvail();
        
        float deltaTime = ImGui.GetIO().DeltaTime;
        float time = (float)ImGui.GetTime();

        drawList.AddRectFilledMultiColor(windowPos, windowPos + new Vector2(windowSize.X, 150), 
            ImGui.GetColorU32(new Vector4(0.05f, 0.1f, 0.2f, 0.4f)), 
            ImGui.GetColorU32(new Vector4(0.05f, 0.1f, 0.2f, 0.4f)),
            ImGui.GetColorU32(new Vector4(0, 0, 0, 0)),
            ImGui.GetColorU32(new Vector4(0, 0, 0, 0)));

        float logWidth = avail.X * 0.35f;
        if (logWidth < 250) logWidth = 250;
        float orbitWidth = avail.X - logWidth - 20;

        // --- LEFT SIDE: ACTIVITY LOG ---
        ImGui.BeginChild("ActivityLog", new Vector2(logWidth, avail.Y), false);
        {
            ImGui.TextColored(new Vector4(0.4f, 0.6f, 1f, 1f), "SYSTEM HEARTBEAT");
            ImGui.Separator();
            lock (Lock)
            {
                foreach (var signal in Signals)
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, signal.Color);
                    ImGui.TextUnformatted($"[{signal.Timestamp:HH:mm:ss}]");
                    ImGui.PopStyleColor();
                    ImGui.SameLine();
                    ImGui.TextUnformatted(signal.Label);
                }
            }
        }
        ImGui.EndChild();

        ImGui.SameLine(0, 20);
        var orbitCenter = ImGui.GetCursorScreenPos() + new Vector2(orbitWidth / 2, avail.Y * 0.45f);
        
        float coreRadius = 45 + MathF.Sin(time * 1.5f) * 4;
        
        for (int i = 0; i < 3; i++)
        {
            float glowR = coreRadius + (i * 10) + MathF.Sin(time * 3) * 5;
            drawList.AddCircle(orbitCenter, glowR, ImGui.GetColorU32(new Vector4(0.2f, 0.4f, 0.8f, 0.2f / (i + 1))), 64, 1.5f);
        }

        drawList.AddCircleFilled(orbitCenter, coreRadius, ImGui.GetColorU32(new Vector4(0.08f, 0.08f, 0.2f, 1)));
        drawList.AddCircle(orbitCenter, coreRadius + 1, ImGui.GetColorU32(new Vector4(0.3f, 0.5f, 0.9f, 0.8f)), 32, 2.5f);
        
        var coreText = "DAGOBERT";
        var textSize = ImGui.CalcTextSize(coreText);
        ImGui.SetWindowFontScale(1.2f);
        drawList.AddText(orbitCenter - textSize / 2, ImGui.GetColorU32(new Vector4(1, 1, 1, 1)), coreText);
        ImGui.SetWindowFontScale(1.0f);

        foreach (var orbital in Orbitals)
        {
            orbital.Angle += orbital.Speed * deltaTime;
            var pos = orbitCenter + new Vector2(MathF.Cos(orbital.Angle), MathF.Sin(orbital.Angle)) * orbital.OrbitRadius;

            drawList.AddCircle(orbitCenter, orbital.OrbitRadius, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.1f)), 100);

            for (int i = orbital.Pulses.Count - 1; i >= 0; i--)
            {
                var pulse = orbital.Pulses[i];
                pulse.Progress += deltaTime * 1.8f;
                if (pulse.Progress >= 1f) orbital.Pulses.RemoveAt(i);
                else
                {
                    var beamPos = Vector2.Lerp(orbitCenter, pos, pulse.Progress);
                    drawList.AddCircleFilled(beamPos, 3.5f, ImGui.GetColorU32(pulse.Color));
                    // Glow behind the head
                    drawList.AddCircleFilled(beamPos, 6f, ImGui.GetColorU32(new Vector4(pulse.Color.X, pulse.Color.Y, pulse.Color.Z, 0.3f)));
                    // Dynamic Trail
                    float trailLen = 0.15f;
                    drawList.AddLine(Vector2.Lerp(orbitCenter, pos, Math.Max(0, pulse.Progress - trailLen)), beamPos, ImGui.GetColorU32(pulse.Color), 2.5f);
                }
            }

            bool isActive = _activeRetainerName != null && orbital.Name.Equals(_activeRetainerName, StringComparison.OrdinalIgnoreCase);
            float satRadius = isActive ? 12f : 9f;
            var satColor = isActive ? new Vector4(0.6f, 0.8f, 1f, 1f) : orbital.Color;

            if (isActive)
            {
                drawList.AddCircle(pos, satRadius + MathF.Sin(time * 5) * 2, ImGui.GetColorU32(new Vector4(1, 1, 1, 0.4f)), 32, 2f);
            }

            drawList.AddCircleFilled(pos, satRadius, ImGui.GetColorU32(satColor));
            drawList.AddCircle(pos, satRadius + 1, ImGui.GetColorU32(new Vector4(1, 1, 1, 0.6f)), 32, 1.5f);
            
            var nameSize = ImGui.CalcTextSize(orbital.Name);
            var rectMin = pos + new Vector2(15, -nameSize.Y / 2 - 2);
            var rectMax = rectMin + nameSize + new Vector2(8, 4);
            drawList.AddRectFilled(rectMin, rectMax, ImGui.GetColorU32(new Vector4(0, 0, 0, 0.6f)), 4f);
            drawList.AddText(rectMin + new Vector2(4, 2), ImGui.GetColorU32(new Vector4(0.9f, 0.95f, 1f, 1f)), orbital.Name);
        }
    }
}
