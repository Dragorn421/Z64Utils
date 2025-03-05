using System;

namespace Z64.Skelanime;

public class Animation
{
    public int FrameCount;
    public int StaticIndexMax;
    public Z64Object.AnimationJointIndicesHolder.JointIndex[] Joints;
    public short[] FrameData;

    public Animation(
        int frameCount,
        int staticIndexMax,
        Z64Object.AnimationJointIndicesHolder.JointIndex[] joints,
        short[] frameData
    )
    {
        FrameCount = frameCount;
        StaticIndexMax = staticIndexMax;
        Joints = joints;
        FrameData = frameData;
    }

    public short GetFrameData(int frameDataIdx, int frame)
    {
        return FrameData[frameDataIdx < StaticIndexMax ? frameDataIdx : frameDataIdx + frame];
    }

    public static Animation Get(
        F3DZEX.Memory mem,
        Z64Object.AnimationHolder animationHolder,
        int limbsCount
    )
    {
        byte[] buff = mem.ReadBytes(
            animationHolder.JointIndices,
            (limbsCount + 1) * Z64Object.AnimationJointIndicesHolder.ENTRY_SIZE
        );
        var joints = new Z64Object.AnimationJointIndicesHolder("joints", buff).JointIndices;

        int max = 0;
        foreach (var joint in joints)
        {
            max = Math.Max(max, joint.X);
            max = Math.Max(max, joint.Y);
            max = Math.Max(max, joint.Z);
        }

        int bytesToRead =
            (max < animationHolder.StaticIndexMax ? max + 1 : animationHolder.FrameCount + max) * 2;

        buff = mem.ReadBytes(animationHolder.FrameData, bytesToRead);
        var frameData = new Z64Object.AnimationFrameDataHolder("framedata", buff).FrameData;

        return new(animationHolder.FrameCount, animationHolder.StaticIndexMax, joints, frameData);
    }
}
