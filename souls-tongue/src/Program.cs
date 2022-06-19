using System;
using System.IO;
using System.Collections.Generic;
using SoulsFormats;
using System.Threading;
using System.Text.RegularExpressions;
using souls_tongue.src;
using System.Diagnostics;

namespace souls_tongue
{
	public enum AssetType
	{
		Character,
		Map,
		Part,
		Object
	}
	class Program
	{
		public static List<String> GetFilesRecursive(String dirPath)
		{
			List<String> filePaths = new();

			DirectoryInfo di = new DirectoryInfo(dirPath);

			foreach(DirectoryInfo dir in di.GetDirectories("*", System.IO.SearchOption.AllDirectories))
			{
				foreach(FileInfo fi in dir.EnumerateFiles())
				{
					filePaths.Add(fi.FullName);
				}
			}

			return filePaths;
		}
		public static void GenerateCache(String searchPath, String outputPath)
		{
			List<String> cache = new List<string>();
			Stack<String> dirStack = new();
			dirStack.Push(searchPath);

			while(dirStack.Count > 0)
			{
				List<String> files = GetFilesRecursive(dirStack.Pop());

				foreach (String file in files)
				{
					String bndDirPath = ResolvePossibleBinderPath(file);

					if (Directory.Exists(bndDirPath))
					{
						dirStack.Push(bndDirPath);
					}
					else if (File.Exists(bndDirPath))
					{
						cache.Add(file);
					}
				}
			}
			using (StreamWriter sw = File.CreateText(outputPath))
			{
				foreach(String line in cache)
				{
					sw.WriteLine(line);
				}
			}
		}

		public static String GetTrimmedExtension(String FileName)
		{
			//split on first occurence of . or -
			string[] SplitFileName = new string[2];
			if (FileName.Contains("."))
			{
				SplitFileName = FileName.Split(new[] { '.' }, 2);
			}
			else if (FileName.Contains("-"))
			{
				SplitFileName = FileName.Split(new[] { '-' }, 2);
			}

			return SplitFileName[1];
		}

		public static void GetAssetPaths(String AssetName, AssetType Type, List<String> flverPaths, 
			List<String> animationPaths = null, List<String> skeletonPaths = null, List<String> taePaths = null)
		{
			Func<String, List<String>> GetPathList = FileName =>
			{
				if (FileName.ToLowerInvariant() == "skeleton.hkx")
				{
					return skeletonPaths;
				}

				String Extension = GetTrimmedExtension(FileName);
				
				switch (Extension)
				{
					case "flver":	return flverPaths;
					case "tae":		return taePaths;
					case "hkx":		return animationPaths;
					default: return null;
				}
			};

			List<String> SearchPaths = new();

			Action<String> AddSearchPath = S => SearchPaths.Add(ResolveSoulsPath(String.Format(S, AssetName)));

			switch (Type)
			{
				case AssetType.Character:
					AddSearchPath("chr/{0}.chrbnd");
					AddSearchPath("chr/{0}.anibnd");
					AddSearchPath("chr/{0}");
					//DS3 stuff
					AddSearchPath("chr/{0}.anibnd.dcx");
					//AddSearchPath("chr/{0}-anibnd-dcx");
					AddSearchPath("chr/{0}.chrbnd.dcx");
					//AddSearchPath("chr/{0}-chrbnd-dcx");
					AddSearchPath("chr/{0}.bdhbnd.dcx");
					//AddSearchPath("chr/{0}-bdhbnd-dcx");
					AddSearchPath("chr/{0}.texbnd.dcx");
					//AddSearchPath("chr/{0}-texbnd-dcx");
					break;
				case AssetType.Map:
					String ShortMapName = AssetName.Split("_")[0];

					AddSearchPath("map/{0}");
					AddSearchPath("map/" + ShortMapName);
					AddSearchPath("map/tx");
					break;
				case AssetType.Part:
					AddSearchPath("parts/{0}.partsbnd");
					break;
				case AssetType.Object:
					AddSearchPath("obj/{0}.objbnd");
					break;
				default:
					break;
			}

			foreach(String SearchPath in SearchPaths)
			{
				//TODO: can we narrow this down with a more restrictive search pattern
				if (!Directory.Exists(SearchPath))
				{
					continue;
				}

				string[] AllFileNames = Directory.GetFiles(SearchPath, "*", SearchOption.AllDirectories);
				
				foreach (String FileName in AllFileNames)
				{
					String CurrPath = ResolvePossibleBinderPath(FileName);
					List<String> PathArray = GetPathList(Path.GetFileName(CurrPath));
					if (PathArray != null)
					{
						PathArray.Add(CurrPath);
					}

				}
			}
		}

		public static bool IsRegexMatching(string Input, string Pattern)
		{
			return new Regex(Pattern).Match(Input).Success;
		}

		public static string GetRegexMatch(string Input, string Pattern)
		{
			return new Regex(Pattern).Match(Input).Value.Trim();
		}

		public static String dataPath;
		public static String yabberPath;
		public static String cachePath;

		public static Dictionary<String, String> TexturePaths;

		public static String ResolvePossibleBinderPath(String FullPath)
		{
			HashSet<String> BinderExtensions = new HashSet<String>() {
				"chrbnd", "anibnd", "partsbnd", "objbnd", "tpf", "mtdbnd", "behbnd", "texbnd", "tpfbhd",
				"chrbnd.dcx", "anibnd.dcx", "partsbnd.dcx", "objbnd.dcx", "tpf.dcx", "mtdbnd.dcx", 
				"behbnd.dcx", "texbnd.dcx", "tpfbhd.dcx", "tpf.old"
			};

			String Extension = GetTrimmedExtension(FullPath);

			Func<String> GetBinderResolvedName = () =>
			{
				String DirName = Path.GetDirectoryName(FullPath);
				String NakedFileName = Path.GetFileName(FullPath).Split(".")[0];

				return Path.GetFullPath(NakedFileName + "-" + Extension.Replace(".","-"), DirName);
			};

			if (!BinderExtensions.Contains(Extension))
			{
				return FullPath;
			}

			String AlreadyUnpackedDirName = Path.GetFileName(FullPath).Split(".")[0] + "-" + Extension.Replace(".","-");
			if (Directory.Exists(Path.GetDirectoryName(FullPath) + Path.DirectorySeparatorChar + AlreadyUnpackedDirName))
			{
				return GetBinderResolvedName();
			}

			//Unpack with Yabber and adjust path
			ProcessStartInfo startInfo = new ProcessStartInfo();
			startInfo.CreateNoWindow = false;
			startInfo.UseShellExecute = false;
			startInfo.FileName = Program.yabberPath;
			startInfo.WindowStyle = ProcessWindowStyle.Hidden;
			startInfo.Arguments = "\"" + FullPath + "\"";
			startInfo.RedirectStandardOutput = true;

			// Start the process with the info we specified.
			// Call WaitForExit and then the using statement will close.
			using (Process exeProcess = Process.Start(startInfo))
			{
				while (!exeProcess.HasExited)
				{
					foreach (ProcessThread t in exeProcess.Threads)
					{
						if (t.ThreadState == System.Diagnostics.ThreadState.Wait && t.WaitReason == ThreadWaitReason.UserRequest)
						{
							//kill on errors, and return original path
							exeProcess.Kill();
							return FullPath;
						}
					}
				}
				
				exeProcess.WaitForExit();
			}

			return GetBinderResolvedName();
		}

		public static String ResolveSoulsPath(String RelativePath)
		{
			//Normalize path and split up
			String NormalizedPath = Path.GetRelativePath(dataPath, Path.GetFullPath(RelativePath, dataPath));

			if (NormalizedPath == ".")
			{
				return dataPath;
			}

			String[] Dirs = NormalizedPath.Split(Path.DirectorySeparatorChar);

			String LoopPath = dataPath;
			for (int i = 0; i < Dirs.Length; i++)
			{
				String CurrPath = Path.GetFullPath(Dirs[i], LoopPath);

				FileInfo CurrFile = new FileInfo(CurrPath);

				if (CurrFile.Exists)
				{
					LoopPath = ResolvePossibleBinderPath(CurrPath);
					continue;
				}

				LoopPath = CurrPath;
			}

			return LoopPath;
		}

		static void Main(String[] args)
		{
			Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;

			dataPath = args[0].Replace("\"", "").Replace("/","\\");
			yabberPath = args[1].Replace("\"","").Replace("/", "\\");
			cachePath = args[2].Replace("\"","").Replace("/", "\\");

			if (!File.Exists(cachePath))
			{
				GenerateCache(dataPath, cachePath);
			}

			TexturePaths = ((Func<Dictionary<String, String>>)(() =>
			{
				Dictionary<String, String> Dict = new();

				StreamReader SR = new StreamReader(new FileStream(cachePath, FileMode.Open));

				while (!SR.EndOfStream)
				{
					String CurrLine = SR.ReadLine();
					String Key = Path.GetFileNameWithoutExtension(CurrLine).ToLowerInvariant();

					if (!Dict.ContainsKey(Key))
					{
						Dict.Add(Key, CurrLine);
					}
				}

				return Dict;
			}))();

			String Command = args[3];

			if (IsRegexMatching(Command, "^[^\\s]+\\s+[^\\s]+$"))
			{
				String CommandType = GetRegexMatch(Command, "^[^\\s]+(?=\\s+[^\\s]+$)");
				String AssetName = GetRegexMatch(Command, "(?<=^[^\\s]+\\s+)[^\\s]+$");

				AssetType Type = ((Func<String, AssetType>)(T =>
				{
					switch (T)
					{
						case "chr":		return AssetType.Character;
						case "part":	return AssetType.Part;
						case "map":		return AssetType.Map;
						case "obj":		return AssetType.Object;
						default:
							throw new NotImplementedException();
							return AssetType.Object;
					}
				}))(CommandType);

				List<String> flverPaths = new();
				
				GetAssetPaths(AssetName, Type, flverPaths);

				//Send out
				TongueStream TS = new StdOutTongueStream();

				//HACKY MSB1 EXPORTER
				if (Type == AssetType.Map)
				{
					String msbName = AssetName + ".msb";
					String msbPath = dataPath + "\\Map\\MapStudio\\" + msbName;
					MSB1 msb = MSB1.Read(msbPath);
					TS.Write(msb);
				}
				//END OF MSB1 EXPORTER

				TS.Write(flverPaths.Count);
				foreach(String FlverPath in flverPaths)
				{
					String FlverName = Path.GetFileNameWithoutExtension(FlverPath);

					FLVER2 CurrentFlver = FLVER2.Read(FlverPath);
					TS.Write(FlverName);
					TS.Write(CurrentFlver);
				}

				TS.Close();				
			}
		}
	}
}
