using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;
using SoulsFormats;

namespace souls_tongue.src
{
    public struct BlenderBone
    {
        static Vector3 ShortVector = new Vector3(0f, 0.05f, 0f);
        static BlenderBone Default = new BlenderBone("", -1, Vector3.Zero, Vector3.Zero, false);
        BlenderBone(String Name, short ParentIndex, Vector3 HeadPos, Vector3 TailPos, bool bInitialized)
        {
            this.Name = Name;
            this.ParentIndex = ParentIndex;
            this.HeadPos = HeadPos;
            this.TailPos = TailPos;
            this.bInitialized = bInitialized;
        }

        public String Name;
        public short ParentIndex;
        public Vector3 HeadPos;
        public Vector3 TailPos;
        public bool bInitialized;

        public static List<BlenderBone> GetBlenderBones(List<FLVER.Bone> Bones)
        {
            List<BlenderBone> BlenderBones = new();
            Bones.ForEach(B => BlenderBones.Add(BlenderBone.Default));

            Action<int, Matrix4x4> AddBones = null;
            AddBones = (BoneIndex, ParentTransform) =>
            {
                while(BoneIndex >= 0)
                {
                    FLVER.Bone CurrBone = Bones[BoneIndex];
                    BlenderBone CurrBlenderBone = BlenderBone.Default;

                    //Assign Name and Parent
                    CurrBlenderBone.bInitialized = true;
                    CurrBlenderBone.Name = CurrBone.Name;
                    CurrBlenderBone.ParentIndex = CurrBone.ParentIndex;

                    //Assign transforms
                    Vector3 BonePos = CurrBone.Translation;
                    Vector3 BoneRot = CurrBone.Rotation;

                    //Multiplication order different than blenders mathutils
                    Matrix4x4 R = Matrix4x4.CreateRotationX(BoneRot.X) * Matrix4x4.CreateRotationZ(BoneRot.Z) * Matrix4x4.CreateRotationY(BoneRot.Y);

                    CurrBlenderBone.HeadPos = Vector3.Transform(BonePos, ParentTransform);
                    CurrBlenderBone.TailPos = CurrBlenderBone.HeadPos + (Vector3.Transform(ShortVector, R));

                    BlenderBones[BoneIndex] = CurrBlenderBone;

                    BoneIndex = CurrBone.NextSiblingIndex;

                    AddBones(CurrBone.ChildIndex, R * Matrix4x4.CreateTranslation(BonePos) * ParentTransform);
                }
            };

            AddBones(0, Matrix4x4.Identity);

            return BlenderBones;
        }
    }
}
