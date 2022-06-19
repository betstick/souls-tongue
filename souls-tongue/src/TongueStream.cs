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

		public void Write(uint Message)
		{
			Write(BitConverter.GetBytes(Message));
		}

		public void Write(short Message)
		{
			Write(BitConverter.GetBytes(Message));
		}

		public void Write(ushort Message)
		{
			Write(BitConverter.GetBytes(Message));
		}

		public void Write(byte Message)
		{
			Write(new byte[] { Message });
		}

		public void Write(sbyte Message)
		{
			Write(new byte[] { (byte)Message }); //TODO: validate this
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
			if(Message == null)
			{
				Message = "NONE";
			}

			Byte[] UTFBytes = System.Text.Encoding.UTF8.GetBytes(Message);
			Byte[] LenBytes = BitConverter.GetBytes(UTFBytes.Length);

			Write(LenBytes);
			Write(UTFBytes);
		}

		public void WriteList<T>(List<T> list)
		{
			Write(list.Count);
			foreach (dynamic Element in list)
			{
				Write(Element);
			}
		}

		public void WriteArray(Array array)
		{
			Write(array.Length);
			foreach (dynamic Element in array)
			{
				Write(Element);
			}
		}

		public void WriteListComplex<T>(List<T> Array, Action<T> SerializeElementFunc)
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

		public static System.Numerics.Vector3 GetCorrectedPos(System.Numerics.Vector3 Vector)
		{
			return new System.Numerics.Vector3(-Vector.X, -Vector.Z, Vector.Y);
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
			String TextureKey = Path.GetFileNameWithoutExtension(Texture.Path).ToLowerInvariant();

			if (!Program.TexturePaths.ContainsKey(TextureKey))
			{
				TextureKey = "";
			}

			Write(TextureKey != "" ? Program.ResolveSoulsPath(Program.TexturePaths[TextureKey]) : "");
			//Write(Texture.Path);
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

		public void Write(FLVER2.Material Material)
		{
			//Material
			Write(Material.Name);
			Write(Material.MTD);
			Write(Material.Flags);
			Write(Material.GXIndex);
			WriteList(Material.Textures);

			//MTD
			String name = Material.MTD.Split("\\").Last();
			String path = "";

			//PTDE or DS3 style pathing
			if(File.Exists(Program.dataPath + "\\mtd\\Mtd-mtdbnd\\" + name))
			{
				path = Program.dataPath + "\\mtd\\Mtd-mtdbnd\\" + name;
			}
			else if(File.Exists(Program.dataPath + "\\mtd\\allmaterialbnd-mtdbnd-dcx\\" + name))
			{
				path = Program.dataPath + "\\mtd\\allmaterialbnd-mtdbnd-dcx\\" + name;
			}

			MTD mtd = SoulsFile<MTD>.Read(path);

			WriteList(mtd.Params);
		}

		public void Write(FLVER2.Mesh Mesh)
		{
			//Mesh
			WriteList(Mesh.BoneIndices);
			Write(Mesh.DefaultBoneIndex);
			Write(Mesh.MaterialIndex);
			WriteList(Mesh.Vertices);
			Write(Mesh.FaceSets[0]);
		}

		public void Write(FLVER.Vertex Vertex)
		{
			Write(GetCorrectedPos(Vertex.Position));

			Write(Vertex.BoneIndices[0]);
			Write(Vertex.BoneIndices[1]);
			Write(Vertex.BoneIndices[2]);
			Write(Vertex.BoneIndices[3]);

			Write(Vertex.BoneWeights[0]);
			Write(Vertex.BoneWeights[1]);
			Write(Vertex.BoneWeights[2]);
			Write(Vertex.BoneWeights[3]);

			WriteList(Vertex.UVs);

			Write(GetCorrectedPos(Vertex.Normal));
			Write(Vertex.NormalW);

			WriteList(Vertex.Colors);
		}

		public void Write(FLVER2.FaceSet FaceSet)
		{
			Write((int)FaceSet.Flags);
			Write(FaceSet.CullBackfaces);

			//Fake array send, we pretend to send 3-tuples of ints
			List<int> Faces = FaceSet.Triangulate(true);

			Write((int)(Faces.Count / 3));
			foreach (int FaceVert in Faces)
			{
				Write(FaceVert);
			}
		}

		public void Write(FLVER.Dummy Dummy)
		{
			Write(Dummy.ReferenceID);

			Write(GetCorrectedPos(Dummy.Position));

			Write(GetCorrectedPos(Dummy.Upward));

			Write(Dummy.UseUpwardVector);
			Write(Dummy.AttachBoneIndex);
			Write(Dummy.ParentBoneIndex);
		}

		public void Write(FLVER2 Flver)
		{
			//Skeleton
			WriteList(BlenderBone.GetBlenderBones(Flver.Bones));

			//Materials
			WriteList(Flver.Materials);

			//Import Mesh
			WriteList(Flver.Meshes);

			//Dummies
			WriteList(Flver.Dummies);
		}

		public void Write(FLVER.VertexColor Color)
		{
			Write(Color.R);
			Write(Color.G);
			Write(Color.B);
			Write(Color.A);
		}

		public void Write(BlenderBone Bone)
		{
			Write(Bone.Name);
			Write(Bone.ParentIndex);
			Write(Bone.HeadPos);
			Write(Bone.TailPos);
			Write(Bone.bInitialized);
		}

		public void Write(MSB1 msb)
		{
			WriteList(msb.Events.Environments);
			WriteList(msb.Events.Generators);
			WriteList(msb.Events.Lights);
			WriteList(msb.Events.MapOffsets);
			WriteList(msb.Events.Messages);
			WriteList(msb.Events.Navmeshes);
			WriteList(msb.Events.ObjActs);
			WriteList(msb.Events.PseudoMultiplayers);
			WriteList(msb.Events.SFXs);
			WriteList(msb.Events.Sounds);
			WriteList(msb.Events.SpawnPoints);
			WriteList(msb.Events.Treasures);
			WriteList(msb.Events.WindSFXs);

			WriteList(msb.Models.Collisions);
			WriteList(msb.Models.Enemies);
			WriteList(msb.Models.MapPieces);
			WriteList(msb.Models.Navmeshes);
			WriteList(msb.Models.Objects);
			WriteList(msb.Models.Players);

			WriteList(msb.Parts.Collisions);
			WriteList(msb.Parts.ConnectCollisions);
			WriteList(msb.Parts.DummyEnemies);
			WriteList(msb.Parts.DummyObjects);
			WriteList(msb.Parts.Enemies);
			WriteList(msb.Parts.MapPieces);
			WriteList(msb.Parts.Navmeshes);
			WriteList(msb.Parts.Objects);
			WriteList(msb.Parts.Players);

			WriteList(msb.Regions.Regions);
		}

		//MSB1 event sub classes
		public void Write(MSB1.Event.Environment env)
		{
			Write(env.UnkT00);
			Write(env.UnkT04);
			Write(env.UnkT08);
			Write(env.UnkT0C);
			Write(env.UnkT10);
			Write(env.UnkT14);

			MSB1.Event e = env;
			Write(e);
		}

		public void Write(MSB1.Event.Generator gen)
		{
			Write(gen.GenType);
			Write(gen.InitialSpawnCount);
			Write(gen.LimitNum);
			Write(gen.MaxGenNum);
			Write(gen.MaxInterval);
			Write(gen.MaxNum);
			Write(gen.MinGenNum);
			Write(gen.MinInterval);
			WriteArray(gen.SpawnPartNames);
			WriteArray(gen.SpawnPointNames);

			MSB1.Event e = gen;
			Write(e);
		}

		public void Write(MSB1.Event.Light light)
		{
			Write(light.PointLightID);

			MSB1.Event e = light;
			Write(e);
		}

		public void Write(MSB1.Event.MapOffset mo)
		{
			Write(mo.Degree);
			Write(GetCorrectedPos(mo.Position));

			MSB1.Event e = mo;
			Write(e);
		}

		public void Write(MSB1.Event.Message msg)
		{
			Write(msg.Hidden);
			Write(msg.MessageID);
			Write(msg.UnkT02);

			MSB1.Event e = msg;
			Write(e);
		}

		public void Write(MSB1.Event.Navmesh nvm)
		{
			Write(nvm.NavmeshRegionName);

			MSB1.Event e = nvm;
			Write(e);
		}

		public void Write(MSB1.Event.ObjAct objAct)
		{
			Write(objAct.EventFlagID);
			Write(objAct.ObjActEntityID);
			Write(objAct.ObjActParamID);
			Write(objAct.ObjActPartName);
			Write((ushort)objAct.ObjActState); //its an enum

			MSB1.Event e = objAct;
			Write(e);
		}

		public void Write(MSB1.Event.PseudoMultiplayer pmp)
		{
			Write(pmp.ActivateGoodsID);
			Write(pmp.EventFlagID);
			Write(pmp.HostEntityID);

			MSB1.Event e = pmp;
			Write(e);
		}

		public void Write(MSB1.Event.SFX sfx)
		{
			Write(sfx.FFXID);

			MSB1.Event e = sfx;
			Write(e);
		}

		public void Write(MSB1.Event.Sound sound)
		{
			Write(sound.SoundID);
			Write(sound.SoundType);

			MSB1.Event e = sound;
			Write(e);
		}

		public void Write(MSB1.Event.SpawnPoint spawn)
		{
			Write(spawn.SpawnPointName);

			MSB1.Event e = spawn;
			Write(e);
		}

		public void Write(MSB1.Event.Treasure treasure)
		{
			Write(treasure.InChest);
			WriteArray(treasure.ItemLots);
			Write(treasure.StartDisabled);
			Write(treasure.TreasurePartName);

			MSB1.Event e = treasure;
			Write(e);
		}

		public void Write(MSB1.Event.WindSFX wind)
		{
			Write(wind.UnkT0C);
			Write(wind.UnkT1C);
			Write(wind.WindSwingCycle0);
			Write(wind.WindSwingCycle1);
			Write(wind.WindSwingCycle2);
			Write(wind.WindSwingCycle3);
			Write(wind.WindSwingPow0);
			Write(wind.WindSwingPow1);
			Write(wind.WindSwingPow2);
			Write(wind.WindSwingPow3);
			Write(wind.WindVecMax);
			Write(wind.WindVecMin);

			MSB1.Event e = wind;
			Write(e);
		}

		//MSB1 model sub classes
		public void Write(MSB1.Model.Collision collision)
		{
			Write(collision.Name);
			Write(collision.SibPath);
		}

		public void Write(MSB1.Model.Enemy enemy)
		{
			Write(enemy.Name);
			Write(enemy.SibPath);
		}

		public void Write(MSB1.Model.MapPiece piece)
		{
			Write(piece.Name);
			Write(piece.SibPath);
		}

		public void Write(MSB1.Model.Navmesh navmesh)
		{
			Write(navmesh.Name);
			Write(navmesh.SibPath);
		}

		public void Write(MSB1.Model.Object obj)
		{
			Write(obj.Name);
			Write(obj.SibPath);
		}

		public void Write(MSB1.Model.Player player)
		{
			Write(player.Name);
			Write(player.SibPath);
		}

		//MSB1 part subclasses
		public void Write(MSB1.Part.Collision col)
		{
			Write(col.DisableBonfireEntityID);
			Write(col.DisableStart);
			Write(col.EnvLightMapSpotIndex);
			Write(col.HitFilterID);
			Write(col.LockCamParamID1);
			Write(col.LockCamParamID2);
			Write(col.MapNameID);
			WriteArray(col.NvmGroups);
			Write(col.PlayRegionID);
			Write(col.ReflectPlaneHeight);
			Write(col.SoundSpaceType);

			MSB1.Part part = col;
			Write(part);
		}

		public void Write(MSB1.Part.ConnectCollision concol)
		{
			Write(concol.CollisionName);
			WriteArray(concol.MapID);

			MSB1.Part part = concol;
			Write(part);
		}

		public void Write(MSB1.Part.DummyEnemy dummyEnemy)
		{
			MSB1.Part.EnemyBase enemyBase = dummyEnemy;
			Write(enemyBase);
		}

		public void Write(MSB1.Part.DummyObject dummyObj)
		{
			MSB1.Part.ObjectBase objectBase = dummyObj;
			Write(objectBase);
		}

		public void Write(MSB1.Part.Enemy enemy)
		{
			MSB1.Part.EnemyBase enemyBase = enemy;
			Write(enemyBase);
		}

		public void Write(MSB1.Part.MapPiece mapPiece)
		{
			MSB1.Part part = mapPiece;
			Write(part);
		}

		public void Write(MSB1.Part.Navmesh navmesh)
		{
			WriteArray(navmesh.NvmGroups);

			MSB1.Part part = navmesh;
			Write(part);
		}

		public void Write(MSB1.Part.Object obj)
		{
			MSB1.Part.ObjectBase objBase = obj;
			Write(objBase);
		}
		
		public void Write(MSB1.Part.Player player)
		{
			MSB1.Part part = player;
			Write(part);
		}

		//MSB1 region subclasses
		public void Write(MSB1.Region region)
		{
			Write(region.EntityID);
			Write(region.Name);
			Write(GetCorrectedPos(region.Position));
			Write(region.Rotation); //TODO: does this need to be corrected?
			Write(region.Shape);
		}

		//MSB1 supporting classes
		public void Write(MSB1.Event e) //event is a reserved keyword :(
		{
			Write(e.EntityID);
			Write(e.EventID);
			Write(e.Name);
			Write(e.PartName);
			Write(e.RegionName);
		}

		public void Write(MSB1.Part.ObjectBase objBase)
		{
			Write(objBase.BreakTerm);
			Write(objBase.CollisionName);
			Write(objBase.InitAnimID);
			Write(objBase.NetSyncType);
			Write(objBase.UnkT0E);
			Write(objBase.UnkT10);

			MSB1.Part part = objBase;
			Write(part);
		}

		public void Write(MSB1.Part.EnemyBase enemyBase)
		{
			Write(enemyBase.CharaInitID);
			Write(enemyBase.CollisionName);
			Write(enemyBase.DamageAnimID);
			Write(enemyBase.InitAnimID);
			//WriteArray(enemyBase.MovePointNames); //this gives "ambiguous" errors
			Write(enemyBase.MovePointNames.Length);
			foreach (String s in enemyBase.MovePointNames)
			{
				Write(s);
			}
			Write(enemyBase.NPCParamID);
			Write(enemyBase.PlatoonID);
			Write(enemyBase.PointMoveType);
			Write(enemyBase.TalkID);
			Write(enemyBase.ThinkParamID);

			MSB1.Part part = enemyBase;
			Write(part);
		}

		public void Write(MSB1.Part part)
		{
			Write(part.DisablePointLightEffect);
			WriteArray(part.DispGroups);
			Write(part.DofID);
			Write(part.DrawByReflectCam);
			WriteArray(part.DrawGroups);
			Write(part.DrawOnlyReflectCam);
			Write(part.EntityID);
			Write(part.FogID);
			Write(part.IsShadowDest);
			Write(part.IsShadowOnly);
			Write(part.IsShadowSrc);
			Write(part.LanternID);
			Write(part.LensFlareID);
			Write(part.LightID);
			Write(part.LodParamID);
			Write(part.ModelName);
			Write(part.Name);
			Write(GetCorrectedPos(part.Position));
			Write(part.Rotation); //TODO: does this need correction?
			Write(part.Scale);
			Write(part.ScatterID);
			Write(part.ShadowID);
			Write(part.SibPath);
			Write(part.ToneCorrectID);
			Write(part.ToneMapID);
			Write(part.UseDepthBiasFloat);
		}

		public void Write(MSB.Shape shape)
		{
			Write("not implemented yet...");
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
