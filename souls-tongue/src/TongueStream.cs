using SoulsFormats;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using static SoulsFormats.MTD;

namespace souls_tongue.src
{
    public abstract class TongueStream
    {
        //Needs to be called so we can flush buffered streams at the end
        public virtual void Close()
        {

        }

        // Basic serialization methods
        public void Write<T>(T Message)
        {
            throw new NotImplementedException();
        }

        public abstract void Write(Byte[] Message);

        public void Write(int Message)
        {
            Write(BitConverter.GetBytes(Message));
        }

        public void Write(short Message)
        {
            Write((int)Message);
        }

        public void Write(float Message)
        {
            Write(BitConverter.GetBytes(Message));
        }

        public void Write(bool Message)
        {
            Write(BitConverter.GetBytes(Message));
        }

        public void Write(String Message)
        {
            Byte[] UTFBytes = System.Text.Encoding.UTF8.GetBytes(Message);
            Byte[] LenBytes = BitConverter.GetBytes(UTFBytes.Length);

            Write(LenBytes);
            Write(UTFBytes);
        }
        public void WriteArray<T>(List<T> Array)
        {
            Write(Array.Count);
            foreach(dynamic Element in Array)
            {
                Write(Element);
            }
        }

        public void WriteArrayComplex<T>(List<T> Array, Action<T> SerializeElementFunc)
        {
            Write(Array.Count);
            foreach (T Element in Array)
            {
                SerializeElementFunc(Element);
            }
        }

        // Specific serializations for Souls types
        public void Write(System.Numerics.Vector3 Vector)
        {
            Write(Vector.X);
            Write(Vector.Y);
            Write(Vector.Z);
        }

        public void Write(FLVER.Bone Bone)
        {
            Write(Bone.Name);

            Write(Bone.Translation);
            Write(Bone.Rotation);
            Write(Bone.Scale);

            //Parent
            Write(Bone.ParentIndex);
            Write(Bone.ChildIndex);
            Write(Bone.NextSiblingIndex);
            Write(Bone.PreviousSiblingIndex);
        }

        public void Write(FLVER2.Texture Texture)
        {
            //Write(Texture.Path);

            String TextureKey = Path.GetFileNameWithoutExtension(Texture.Path).ToLowerInvariant();

            if (!Program.TexturePaths.ContainsKey(TextureKey))
            {
                TextureKey = "";
            }

            /*if (TextureKey == "m10_wall_sewer") TextureKey = "";
            if (TextureKey == "m10_00_wall_stone_s") TextureKey = "";
            if (TextureKey == "m10_obj_bone_rust_n") TextureKey = "";*/

            Write(TextureKey != "" ? Program.ResolveSoulsPath(Program.TexturePaths[TextureKey]) : "");
            Write(Texture.Scale.X);
            Write(Texture.Scale.Y);
            Write(Texture.Type);
        }

        public void Write(Param MTDParam)
        {
            Write(MTDParam.Name);
            Write((int)MTDParam.Type);

            switch (MTDParam.Type)
            {
                case ParamType.Bool:
                    Write((bool)MTDParam.Value);
                    break;
                case ParamType.Int:
                    Write((int)MTDParam.Value);
                    break;
                case ParamType.Int2:
                    int[] Int2 = (int[])MTDParam.Value;
                    Write(Int2[0]);
                    Write(Int2[1]);
                    break;
                case ParamType.Float:
                    Write((float)MTDParam.Value);
                    break;
                case ParamType.Float2:
                    float[] Float2 = (float[])MTDParam.Value;
                    Write(Float2[0]);
                    Write(Float2[1]);
                    break;
                case ParamType.Float3:
                    float[] Float3 = (float[])MTDParam.Value;
                    Write(Float3[0]);
                    Write(Float3[1]);
                    Write(Float3[2]);
                    break;
                case ParamType.Float4:
                    float[] Float4 = (float[])MTDParam.Value;
                    Write(Float4[0]);
                    Write(Float4[1]);
                    Write(Float4[2]);
                    Write(Float4[3]);
                    break;
                default:
                    break;
            }
        }

        public void Write(FLVER.Vertex Vertex)
        {
            Write(Vertex.Position);

            Write(Vertex.BoneIndices[0]);
            Write(Vertex.BoneIndices[1]);
            Write(Vertex.BoneIndices[2]);
            Write(Vertex.BoneIndices[3]);

            Write(Vertex.BoneWeights[0]);
            Write(Vertex.BoneWeights[1]);
            Write(Vertex.BoneWeights[2]);
            Write(Vertex.BoneWeights[3]);

            WriteArray(Vertex.UVs);

            Write(Vertex.Normal);
            Write(Vertex.NormalW);

            WriteArray(Vertex.Colors);
        }

        public void Write(FLVER2.Material Material)
        {
            //Material
            Write(Material.Name);
            Write(Material.MTD);
            Write(Material.Flags);
            Write(Material.GXIndex);
            WriteArray(Material.Textures);

            //MTD
            String name = Material.MTD.Split("\\").Last();
            String path = Program.dataPath + "\\mtd\\Mtd-mtdbnd\\" + name;
            MTD mtd = SoulsFile<MTD>.Read(path);

            WriteArray(mtd.Params);
        }

        public void Write(FLVER2.Mesh Mesh)
        {
            //Mesh
            WriteArray(Mesh.BoneIndices);
            Write(Mesh.DefaultBoneIndex);
            Write(Mesh.MaterialIndex);

            WriteArray(Mesh.Vertices);

            //Facesets
            Write(Mesh.FaceSets[0]);       
        }

        public void Write(FLVER2.FaceSet FaceSet)
        {
            Write((int)FaceSet.Flags);
            WriteArray(FaceSet.Triangulate(true));
        }

        public void Write(FLVER.Dummy Dummy)
        {
            Write(Dummy.ReferenceID);

            Write(-Dummy.Position.X);
            Write(-Dummy.Position.Z);
            Write(Dummy.Position.Y);
            
            Write(-Dummy.Upward.X);
            Write(-Dummy.Upward.Z);
            Write(Dummy.Upward.Y);

            Write(Dummy.UseUpwardVector);
            Write(Dummy.AttachBoneIndex);
            Write(Dummy.ParentBoneIndex);
        }

        public void Write(FLVER2 Flver)
        {
            Write(Flver.Bones.Count);
            Write(Flver.Meshes.Count);
            Write(Flver.Materials.Count);
            Write(Flver.Dummies.Count);
            Write(Flver.GXLists.Count);

            //Skeleton
            WriteArray(Flver.Bones);

            //Materials
            WriteArray(Flver.Materials);

            //Import Mesh
            WriteArray(Flver.Meshes);

            //Dummies
            WriteArray(Flver.Dummies);
        }

        public void Write(FLVER.VertexColor Color)
        {
            Write(Color.R);
            Write(Color.G);
            Write(Color.B);
            Write(Color.A);
        }

}

public class NetworkTongueStream : TongueStream
    {
        NetworkStream TCPStream;
        public NetworkTongueStream(NetworkStream TCPStream)
        {
            this.TCPStream = TCPStream;
        }
        public override void Write(Byte[] Message)
        {
            TCPStream.Write(Message, 0, Message.Length);
        }
    }

    public class StdOutTongueStream : TongueStream
    {
        protected Stream S;
        public StdOutTongueStream()
        {
            S = new BufferedStream(Console.OpenStandardOutput());
        }
        public override void Write(byte[] Message)
        {
            S.Write(Message, 0, Message.Length);
        }

        public override void Close()
        {
             S.Close();
        }
    }

    public class DebugTongueStream : TongueStream
    {
        public override void Write(byte[] Message)
        {
            String ByteString = string.Join(", ", Message.Select(b => b.ToString()));
            Console.Write(ByteString, 0, ByteString.Length);
        }
    }

    public class FileOutputTongueStream : TongueStream
    {
        protected Stream F;
        public FileOutputTongueStream()
        {
            F = new BufferedStream(new FileStream("dump_buffered.txt", FileMode.Create));
        }
        public override void Write(byte[] Message)
        {
            F.Write(Message, 0, Message.Length);
        }
    }

    public class QuietTongueStream : TongueStream
    {
        public override void Write(byte[] Message)
        {
        }
    }
}
