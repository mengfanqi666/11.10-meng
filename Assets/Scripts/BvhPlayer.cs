using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class BvhPlayer : MonoBehaviour
{
    [Header("BVH File")]
    public string fileName = "sample.bvh";

    [Tooltip("BVH 多为厘米；厘米文件填 0.01，米文件填 1")]
    public float unitScale = 0.01f;

    [Tooltip("整体朝向；角色背对相机时改成 0 或 180")]
    public float characterYaw = 180f;

    [Header("Playback")]
    public bool playOnStart = true;
    public bool loop = true;
    [Range(0.1f, 3f)] public float speed = 1f;

    [Header("Visual")]
    public float jointSize = 0.025f;
    public float lineWidth = 0.012f;
    public Color jointColor = new Color(0.90f, 0.90f, 1f);
    public Color boneColor = new Color(0.20f, 0.80f, 1f);

    private BvhClip clip;
    private Transform skeletonRoot;
    private readonly List<Transform> joints = new List<Transform>();
    private readonly List<LineRenderer> lines = new List<LineRenderer>();
    private readonly List<int> lineParents = new List<int>();
    private readonly List<int> lineChildren = new List<int>();

    private float time;
    private bool playing;
    private string status = "Loading...";

    private Material jointMat;
    private Material lineMat;

    public bool IsPlaying { get { return playing; } }
    public float Speed { get { return speed; } }
    public string Status { get { return status; } }
    public float CurrentTime { get { return time; } }
    public float Duration { get { return clip != null ? clip.Duration : 0f; } }
    public int FrameCount { get { return clip != null ? clip.FrameCount : 0; } }

    public float CurrentFrame
    {
        get { return clip != null && clip.FrameTime > 0f ? time / clip.FrameTime : 0f; }
    }

    void Start()
    {
        jointMat = CreateMat(jointColor, "Standard");
        lineMat = CreateMat(boneColor, "Sprites/Default");
        Load();
    }

    public void Load()
    {
        string path = Path.Combine(Application.streamingAssetsPath, fileName);

        try
        {
            if (!File.Exists(path))
                throw new FileNotFoundException("找不到 BVH 文件: " + path);

            string text = File.ReadAllText(path);
            clip = BvhParser.Parse(text);
            BuildSkeleton();

            time = 0f;
            playing = playOnStart;
            ApplyPose(0f);

            status = "Loaded: " + fileName +
                     "  |  Frames: " + clip.FrameCount +
                     "  |  FrameTime: " + clip.FrameTime.ToString("F4") + "s" +
                     "  |  Duration: " + clip.Duration.ToString("F2") + "s";

            Debug.Log(status);
        }
        catch (Exception e)
        {
            clip = null;
            playing = false;
            status = "Load failed: " + e.Message;
            Debug.LogError(status);
        }
    }

    private void BuildSkeleton()
    {
        if (skeletonRoot != null) Destroy(skeletonRoot.gameObject);

        joints.Clear();
        lines.Clear();
        lineParents.Clear();
        lineChildren.Clear();

        skeletonRoot = new GameObject("Skeleton").transform;
        skeletonRoot.SetParent(transform, false);
        skeletonRoot.localPosition = Vector3.zero;
        skeletonRoot.localRotation = Quaternion.Euler(0f, characterYaw, 0f);

        // 1. 创建关节节点
        for (int i = 0; i < clip.Joints.Count; i++)
        {
            BvhJoint j = clip.Joints[i];

            GameObject go = new GameObject(j.Name);
            Transform t = go.transform;
            t.SetParent(j.Parent >= 0 ? joints[j.Parent] : skeletonRoot, false);
            t.localPosition = j.Offset * unitScale;
            t.localRotation = Quaternion.identity;

            // 关节小球
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "JointVisual";
            sphere.transform.SetParent(t, false);
            sphere.transform.localScale = Vector3.one * (jointSize * 2f);

            Renderer r = sphere.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = jointMat;

            Collider c = sphere.GetComponent<Collider>();
            if (c != null)
            {
                c.enabled = false;
                Destroy(c);
            }

            joints.Add(t);
        }

        // 2. 创建骨骼线
        for (int i = 0; i < clip.Joints.Count; i++)
        {
            int parent = clip.Joints[i].Parent;
            if (parent < 0) continue;

            GameObject lineObj = new GameObject("Bone_" + clip.Joints[i].Name);
            lineObj.transform.SetParent(skeletonRoot, false);

            LineRenderer lr = lineObj.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            lr.startWidth = lineWidth;
            lr.endWidth = lineWidth;
            lr.sharedMaterial = lineMat;
            lr.startColor = boneColor;
            lr.endColor = boneColor;
            lr.numCapVertices = 4;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;

            lines.Add(lr);
            lineParents.Add(parent);
            lineChildren.Add(i);
        }
    }

    void Update()
    {
        if (clip == null) return;

        if (playing && clip.Duration > 0f)
        {
            time += Time.deltaTime * speed;

            if (time > clip.Duration)
            {
                if (loop)
                {
                    time = Mathf.Repeat(time, clip.Duration);
                }
                else
                {
                    time = clip.Duration;
                    playing = false;
                }
            }
        }

        float frame = clip.FrameTime > 0f ? time / clip.FrameTime : 0f;
        ApplyPose(frame);
    }

    void LateUpdate()
    {
        for (int i = 0; i < lines.Count; i++)
        {
            int p = lineParents[i];
            int c = lineChildren[i];

            if (p < joints.Count && c < joints.Count)
            {
                lines[i].SetPosition(0, joints[p].position);
                lines[i].SetPosition(1, joints[c].position);
            }
        }
    }

    private void ApplyPose(float frameFloat)
    {
        if (clip == null || clip.FrameCount == 0) return;

        int f0 = Mathf.Clamp(Mathf.FloorToInt(frameFloat), 0, clip.FrameCount - 1);
        int f1 = Mathf.Min(f0 + 1, clip.FrameCount - 1);
        float t = Mathf.Clamp01(frameFloat - f0);

        for (int i = 0; i < clip.Joints.Count; i++)
        {
            BvhJoint j = clip.Joints[i];
            if (j.Channels.Count == 0) continue;

            Vector3 pos0 = j.Offset;
            Vector3 pos1 = j.Offset;
            Quaternion q0 = Quaternion.identity;
            Quaternion q1 = Quaternion.identity;

            int base0 = f0 * clip.ChannelCount + j.ChannelStart;
            int base1 = f1 * clip.ChannelCount + j.ChannelStart;

            for (int c = 0; c < j.Channels.Count; c++)
            {
                string ch = j.Channels[c];
                float v0 = clip.Motion[base0 + c];
                float v1 = clip.Motion[base1 + c];

                if (ch == "xposition") { pos0.x += v0; pos1.x += v1; }
                else if (ch == "yposition") { pos0.y += v0; pos1.y += v1; }
                else if (ch == "zposition") { pos0.z += v0; pos1.z += v1; }
                else
                {
                    Vector3 axis = ch == "xrotation" ? Vector3.right
                                 : ch == "yrotation" ? Vector3.up
                                 : Vector3.forward;

                    // BVH 的通道顺序就是旋转的相乘顺序
                    q0 *= Quaternion.AngleAxis(v0, axis);
                    q1 *= Quaternion.AngleAxis(v1, axis);
                }
            }

            joints[i].localPosition = Vector3.Lerp(pos0, pos1, t) * unitScale;
            joints[i].localRotation = Quaternion.Slerp(q0, q1, t);
        }
    }

    public void TogglePlay()
    {
        if (clip == null) return;

        playing = !playing;

        if (playing && !loop && time >= clip.Duration)
            time = 0f;
    }

    public void SetSpeed(float v)
    {
        speed = Mathf.Clamp(v, 0.1f, 3f);
    }

    private Material CreateMat(Color c, string shaderName)
    {
        Shader sh = Shader.Find(shaderName);
        if (sh == null) sh = Shader.Find("Unlit/Color");

        Material m = new Material(sh);
        m.color = c;
        return m;
    }
}