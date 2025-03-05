using System;
using System.Collections.Generic;
using F3DZEX.Render;
using OpenTK.Mathematics;

namespace Z64.Skelanime;

public class SkeletonTreeLimb
{
    public SkeletonTreeLimb? Child;
    public SkeletonTreeLimb? Sibling;
    public int Index;

    public SkeletonTreeLimb(int index)
    {
        Index = index;
    }

    public void Visit(Action<int> action)
    {
        action(Index);
        Child?.Visit(action);
        Sibling?.Visit(action);
    }
}

public class Skeleton
{
    public List<Z64Object.SkeletonLimbHolder> Limbs;
    public SkeletonTreeLimb Root;

    public Skeleton(List<Z64Object.SkeletonLimbHolder> limbs, SkeletonTreeLimb root)
    {
        Limbs = limbs;
        Root = root;
    }

    public static Skeleton Get(F3DZEX.Memory mem, Z64Object.SkeletonHolder skeletonHolder)
    {
        byte[] limbsData = mem.ReadBytes(skeletonHolder.LimbsSeg, skeletonHolder.LimbCount * 4);

        var skeletonLimbsHolder = new Z64Object.SkeletonLimbsHolder("limbs", limbsData);

        var limbs = new List<Z64Object.SkeletonLimbHolder>();
        for (int i = 0; i < skeletonLimbsHolder.LimbSegments.Length; i++)
        {
            byte[] limbData = mem.ReadBytes(
                skeletonLimbsHolder.LimbSegments[i],
                Z64Object.SkeletonLimbHolder.STANDARD_LIMB_SIZE
            );
            var limb = new Z64Object.SkeletonLimbHolder(
                $"limb_{i}",
                limbData,
                Z64Object.EntryType.StandardLimb
            );
            limbs.Add(limb);
        }

        var treeRoot = new SkeletonTreeLimb(0);

        void RenderLimb(SkeletonTreeLimb treeLimb)
        {
            if (limbs[treeLimb.Index].Child != 0xFF)
            {
                treeLimb.Child = new(limbs[treeLimb.Index].Child);
                RenderLimb(treeLimb.Child);
            }

            if (limbs[treeLimb.Index].Sibling != 0xFF)
            {
                treeLimb.Sibling = new(limbs[treeLimb.Index].Sibling);
                RenderLimb(treeLimb.Sibling);
            }
        }

        RenderLimb(treeRoot);

        return new(limbs, treeRoot);
    }
}

public class SkeletonPose
{
    public Matrix4[] LimbsPose;

    public SkeletonPose(Matrix4[] limbsPose)
    {
        LimbsPose = limbsPose;
    }

    public static SkeletonPose Get(Skeleton skeleton, Animation anim, int frame)
    {
        MatrixStack matrixStack = new();

        var limbsPose = new Matrix4[skeleton.Limbs.Count];

        void RenderLimb(SkeletonTreeLimb treeLimb)
        {
            matrixStack.Push();

            matrixStack.Load(CalcMatrix(matrixStack.Top(), treeLimb.Index));

            limbsPose[treeLimb.Index] = matrixStack.Top();

            if (treeLimb.Child != null)
                RenderLimb(treeLimb.Child);

            matrixStack.Pop();

            if (treeLimb.Sibling != null)
                RenderLimb(treeLimb.Sibling);
        }

        Matrix4 CalcMatrix(Matrix4 src, int limbIdx)
        {
            Vector3 pos = GetLimbPos(limbIdx);

            short rotX = anim.GetFrameData(anim.Joints[limbIdx + 1].X, frame);
            short rotY = anim.GetFrameData(anim.Joints[limbIdx + 1].Y, frame);
            short rotZ = anim.GetFrameData(anim.Joints[limbIdx + 1].Z, frame);

            src =
                Matrix4.CreateRotationX(S16ToRad(rotX))
                * Matrix4.CreateRotationY(S16ToRad(rotY))
                * Matrix4.CreateRotationZ(S16ToRad(rotZ))
                * Matrix4.CreateTranslation(pos)
                * src;

            return src;
        }

        Vector3 GetLimbPos(int limbIdx)
        {
            return (limbIdx == 0)
                ? new Vector3(
                    anim.Joints[limbIdx].X,
                    anim.Joints[limbIdx].Y,
                    anim.Joints[limbIdx].Z
                )
                : new Vector3(
                    skeleton.Limbs[limbIdx].JointX,
                    skeleton.Limbs[limbIdx].JointY,
                    skeleton.Limbs[limbIdx].JointZ
                );
        }

        float S16ToRad(short x) => x * (float)Math.PI / 0x7FFF;

        RenderLimb(skeleton.Root);

        return new(limbsPose);
    }
}
