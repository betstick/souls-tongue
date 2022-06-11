using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SoulsFormats;
using SoulsAssetPipeline.Animation;
using ANX.Framework;
using Assimp;
using NMatrix = System.Numerics.Matrix4x4;
using NVector3 = System.Numerics.Vector3;
using NVector4 = System.Numerics.Vector4;
using NQuaternion = System.Numerics.Quaternion;
using System.Numerics;

namespace souls_tongue
{
    public class NewHavokAnimation_SplineCompressed : NewHavokAnimation
    {
        public List<SplineCompressedAnimation.TransformTrack[]> Tracks => data_compressed.Tracks;

        // Index into array = hkx bone index, result = transform track index.
        private int[] HkxBoneIndexToTransformTrackMap => data_compressed.HkxBoneIndexToTransformTrackMap;

        private int[] TransformTrackIndexToHkxBoneMap => data_compressed.TransformTrackIndexToHkxBoneMap;

        private HavokAnimationData_SplineCompressed data_compressed => (HavokAnimationData_SplineCompressed)data;

        public int BlockCount => data_compressed.BlockCount;
        public int NumFramesPerBlock => data_compressed.NumFramesPerBlock;

        int CurrentBlock => data_compressed.GetBlock(CurrentFrame);

        public NewHavokAnimation_SplineCompressed(string name, NewAnimSkeleton_HKX skeleton,
            HKX.HKADefaultAnimatedReferenceFrame refFrame, HKX.HKAAnimationBinding binding, HKX.HKASplineCompressedAnimation anim, NewAnimationContainer container)
            : base(new HavokAnimationData_SplineCompressed(name, skeleton.OriginalHavokSkeleton, refFrame, binding, anim), skeleton, container)
        {
        }

        public NewHavokAnimation_SplineCompressed(NewHavokAnimation_SplineCompressed toClone)
            : base(toClone.data, toClone.Skeleton, toClone.ParentContainer)
        {

        }
    }
    public class NewHavokAnimation_InterleavedUncompressed : NewHavokAnimation
    {
        public HavokAnimationData_InterleavedUncompressed data_interleaved => (HavokAnimationData_InterleavedUncompressed)data;

        public int TransformTrackCount => data_interleaved.TransformTrackCount;
        public List<NewBlendableTransform> Transforms => data_interleaved.Transforms;

        // Index into array = hkx bone index, result = transform track index.
        private int[] HkxBoneIndexToTransformTrackMap => data_interleaved.HkxBoneIndexToTransformTrackMap;

        private int[] TransformTrackIndexToHkxBoneMap => data_interleaved.TransformTrackIndexToHkxBoneMap;

        public NewHavokAnimation_InterleavedUncompressed(string name, NewAnimSkeleton_HKX skeleton,
            HKX.HKADefaultAnimatedReferenceFrame refFrame, HKX.HKAAnimationBinding binding, HKX.HKAInterleavedUncompressedAnimation anim, NewAnimationContainer container)
            : base(new HavokAnimationData_InterleavedUncompressed(name, skeleton.OriginalHavokSkeleton, refFrame, binding, anim), skeleton, container)
        {
        }

        public NewHavokAnimation_InterleavedUncompressed(NewHavokAnimation_InterleavedUncompressed toClone)
            : base(toClone.data, toClone.Skeleton, toClone.ParentContainer)
        {

        }
    }

    public class NewHavokAnimation
    {
        public HavokAnimationData data;

        public readonly NewAnimationContainer ParentContainer;

        public static NewHavokAnimation Clone(NewHavokAnimation anim)
        {
            if (anim is NewHavokAnimation_SplineCompressed spline)
            {
                return new NewHavokAnimation_SplineCompressed(spline);
            }
            else if (anim is NewHavokAnimation_InterleavedUncompressed interleaved)
            {
                return new NewHavokAnimation_InterleavedUncompressed(interleaved);
            }
            else
            {
                return new NewHavokAnimation(anim.data, anim.Skeleton, anim.ParentContainer);
            }

        }

        public HKX.AnimationBlendHint BlendHint => data.BlendHint;

        public float Weight = 1.0f;

        /// <summary>
        /// Used when blending multiple animations.
        /// The weight ratio used for previous animations when blending to the next one.
        /// </summary>
        public float ReferenceWeight = 1.0f;

        public string Name => data.Name;

        public override string ToString()
        {
            return $"{Name} [{Math.Round(1 / FrameDuration)} FPS]";
        }

        public readonly NewAnimSkeleton_HKX Skeleton;

        private object _lock_boneMatrixStuff = new object();

        public NewBlendableTransform[] blendableTransforms = new NewBlendableTransform[0];
        private List<int> bonesAlreadyCalculated = new List<int>();

        public bool IsAdditiveBlend => data.IsAdditiveBlend;

        public float Duration => data.Duration;
        public float FrameDuration => data.FrameDuration;
        public int FrameCount => data.FrameCount;

        //public bool HasEnded => CurrentTime >= Duration;

        public float CurrentTime { get; private set; } = 0;
        private float oldTime = 0;

        public RootMotionDataPlayer RootMotion { get; private set; }

        public NVector4 RootMotionTransformLastFrame;
        public NVector4 RootMotionTransformDelta;

        //public float ExternalRotation { get; private set; }

        public bool EnableLooping;

        public void ApplyExternalRotation(float r)
        {
            RootMotion.ApplyExternalTransform(r, System.Numerics.Vector3.Zero);
        }

        public void Reset(System.Numerics.Vector4 startRootMotionTransform)
        {
            CurrentTime = 0;
            oldTime = 0;
            RootMotion.ResetToStart(startRootMotionTransform);
        }

        public float CurrentFrame => CurrentTime / FrameDuration;


        public NewBlendableTransform GetBlendableTransformOnCurrentFrame(int hkxBoneIndex)
        {
            return data.GetTransformOnFrameByBone(hkxBoneIndex, CurrentFrame, EnableLooping);
        }

        public void ScrubRelative(float timeDelta)
        {
            CurrentTime += timeDelta;
            if (!EnableLooping && CurrentTime > Duration)
                CurrentTime = Duration;
            RootMotion.SetTime(CurrentTime);
            oldTime = CurrentTime;
        }

        public NewHavokAnimation(HavokAnimationData data, NewAnimSkeleton_HKX skeleton, NewAnimationContainer container)
        {
            this.data = data;

            ParentContainer = container;
            Skeleton = skeleton;

            lock (_lock_boneMatrixStuff)
            {
                blendableTransforms = new NewBlendableTransform[skeleton.HkxSkeleton.Count];
            }

            RootMotion = new RootMotionDataPlayer(data.RootMotion);
        }

        public void CalculateCurrentFrame()
        {
            bonesAlreadyCalculated.Clear();

            lock (_lock_boneMatrixStuff)
            {
                void WalkTree(int i, Matrix currentMatrix, System.Numerics.Vector3 currentScale)
                {
                    if (!bonesAlreadyCalculated.Contains(i))
                    {
                        blendableTransforms[i] = GetBlendableTransformOnCurrentFrame(i);
                        currentMatrix = blendableTransforms[i].GetMatrix().ToXna() * currentMatrix;
                        currentScale *= blendableTransforms[i].Scale.ToXna();
                        //Skeleton.ModHkxBoneMatrix(i, Matrix.CreateScale(currentScale) * currentMatrix, Weight, finalizeHkxMatrices, unusedWeight);
                        bonesAlreadyCalculated.Add(i);
                    }

                    foreach (var c in Skeleton.HkxSkeleton[i].ChildIndices)
                        WalkTree(c, currentMatrix, currentScale);
                }

                foreach (var root in Skeleton.TopLevelHkxBoneIndices)
                    WalkTree(root, Matrix.Identity, System.Numerics.Vector3.One);
            }
        }

        //public void ApplyCurrentFrameToSkeletonWeighted()
        //{


        //}
    }
}
