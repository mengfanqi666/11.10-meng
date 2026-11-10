using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine;

public class BvhJoint
{
    public string Name;
    public int Parent = -1;
    public Vector3 Offset;
    public readonly List<string> Channels = new List<string>();
    public int ChannelStart;
    public bool IsEndSite;
    public readonly List<int> Children = new List<int>();
}

public class BvhClip
{
    public readonly List<BvhJoint> Joints = new List<BvhJoint>();
    public int FrameCount;
    public float FrameTime;
    public int ChannelCount;
    public float[] Motion;   // 扁平数组：frame * ChannelCount + channel

    public float Duration
    {
        get { return FrameCount > 1 ? (FrameCount - 1) * FrameTime : 0f; }
    }
}

public static class BvhParser
{
    public static BvhClip Parse(string text)
    {
        List<string> tokens = Tokenize(text);
        if (tokens.Count == 0) throw new Exception("BVH 文件为空");

        int i = 0;
        if (!Is(tokens[i], "HIERARCHY")) throw new Exception("找不到 HIERARCHY");
        i++;

        BvhClip clip = new BvhClip();
        int nextChannel = 0;

        if (i >= tokens.Count || !Is(tokens[i], "ROOT"))
            throw new Exception("HIERARCHY 后没有 ROOT");
        i++;

        ParseJoint(tokens, ref i, clip, -1, ref nextChannel, false);

        while (i < tokens.Count && !Is(tokens[i], "MOTION")) i++;
        if (i >= tokens.Count) throw new Exception("找不到 MOTION");
        i++;

        // Frames:
        if (i < tokens.Count && tokens[i].StartsWith("Frames", StringComparison.OrdinalIgnoreCase))
        {
            i++;
            if (i < tokens.Count && tokens[i] == ":") i++;
        }
        if (i >= tokens.Count) throw new Exception("缺少 Frames");
        clip.FrameCount = int.Parse(tokens[i], CultureInfo.InvariantCulture);
        i++;

        // Frame Time:
        if (i < tokens.Count && tokens[i].StartsWith("Frame", StringComparison.OrdinalIgnoreCase))
        {
            i++;
            if (i < tokens.Count && (tokens[i] == "Time:" || tokens[i] == "Time")) i++;
            if (i < tokens.Count && tokens[i] == ":") i++;
        }
        if (i >= tokens.Count) throw new Exception("缺少 Frame Time");
        clip.FrameTime = float.Parse(tokens[i], CultureInfo.InvariantCulture);
        i++;

        clip.ChannelCount = nextChannel;
        int total = clip.FrameCount * clip.ChannelCount;
        clip.Motion = new float[total];

        int read = 0;
        while (read < total && i < tokens.Count)
        {
            float v;
            if (!float.TryParse(tokens[i], NumberStyles.Float,
                                CultureInfo.InvariantCulture, out v))
                v = 0f;
            clip.Motion[read] = v;
            read++;
            i++;
        }

        if (read < total)
            throw new Exception("运动数据不足：需要 " + total + " 个值，只读到 " + read + " 个");

        return clip;
    }

    private static void ParseJoint(List<string> tokens, ref int i, BvhClip clip,
                                   int parent, ref int nextChannel, bool endSite)
    {
        if (i >= tokens.Count) throw new Exception("关节解析意外结束");

        string name;
        if (endSite)
            name = "EndSite_" + clip.Joints.Count;
        else
        {
            name = tokens[i];
            i++;
        }

        BvhJoint joint = new BvhJoint();
        joint.Name = name;
        joint.Parent = parent;
        joint.IsEndSite = endSite;

        if (parent >= 0) clip.Joints[parent].Children.Add(clip.Joints.Count);
        clip.Joints.Add(joint);

        if (i >= tokens.Count || tokens[i] != "{")
            throw new Exception("关节 " + name + " 缺少 {");
        i++;

        while (i < tokens.Count && tokens[i] != "}")
        {
            if (Is(tokens[i], "OFFSET"))
            {
                i++;
                joint.Offset = new Vector3(
                    ParseFloat(tokens, ref i),
                    ParseFloat(tokens, ref i),
                    ParseFloat(tokens, ref i));
            }
            else if (Is(tokens[i], "CHANNELS"))
            {
                i++;
                int count = ParseInt(tokens, ref i);
                for (int c = 0; c < count; c++)
                {
                    if (i >= tokens.Count) throw new Exception("CHANNELS 数量不足");
                    joint.Channels.Add(tokens[i].ToLowerInvariant());
                    i++;
                }
                joint.ChannelStart = nextChannel;
                nextChannel += count;
            }
            else if (Is(tokens[i], "JOINT"))
            {
                i++;
                ParseJoint(tokens, ref i, clip, clip.Joints.Count - 1,
                           ref nextChannel, false);
            }
            else if (Is(tokens[i], "End"))
            {
                i++;
                if (i < tokens.Count && Is(tokens[i], "Site")) i++;
                ParseJoint(tokens, ref i, clip, clip.Joints.Count - 1,
                           ref nextChannel, true);
            }
            else
            {
                i++;
            }
        }

        if (i >= tokens.Count || tokens[i] != "}")
            throw new Exception("关节 " + name + " 缺少 }");
        i++;
    }

    private static float ParseFloat(List<string> tokens, ref int i)
    {
        if (i >= tokens.Count) throw new Exception("数值缺失");
        float v = float.Parse(tokens[i], CultureInfo.InvariantCulture);
        i++;
        return v;
    }

    private static int ParseInt(List<string> tokens, ref int i)
    {
        if (i >= tokens.Count) throw new Exception("整数缺失");
        int v = int.Parse(tokens[i], CultureInfo.InvariantCulture);
        i++;
        return v;
    }

    private static bool Is(string a, string b)
    {
        return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }

    private static List<string> Tokenize(string text)
    {
        MatchCollection matches = Regex.Matches(text, @"[{}]|[^\s{}]+");
        List<string> tokens = new List<string>(matches.Count);
        foreach (Match m in matches) tokens.Add(m.Value);
        return tokens;
    }
}