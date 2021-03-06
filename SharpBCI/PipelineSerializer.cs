﻿
using System;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace SharpBCI {


	/**
	 * A class which represents the entire pipeline
	 */
	[DataContract]
	public class SerializedPipeline {
		
		/**
		 * An array of all stages in the pipeline
		 */
		[DataMember(IsRequired = true)]
		public SerializedStage[] stages;

		/**
		 * The connection graph for this pipeline
		 */
		[DataMember(IsRequired = true)]
		public SerializedConnectionInfo[] stageConnections;
	}


	/**
	 * A class which represents a single stage in the pipeline
	 * Can be serialized into a JSON file
	 */
	[DataContract]
	public class SerializedStage {

		/**
		 * A unique key 
		 */
		[DataMember(IsRequired=true)]
		public string stageKey;

		/**
		 * The full name of an IPipeable class which this SerializedStage represents
		 */
		[DataMember(IsRequired = true)]
		public string stageClass;

		/**
		 * Arguments (should only be primative types) which are supplied to the IPipeable upon instatiation
		 */
		[DataMember(IsRequired = true)]
		public object[] arguments;
	}


	/**
	 * A class which represents a connection graph for the pipeline
	 */
	[DataContract]
	public class SerializedConnectionInfo {
		
		/**
		 * Which stage object should be used
		 */
		[DataMember(IsRequired = true)]
		public string stageKey;

		/**
		 * Whether or not outputs should be mirrored
		 * @see IPipeable.Connect for details about what this means
		 */
		[DataMember(IsRequired = true)]
		public bool mirrorOutputs;

		/**
		 * An array of stageKeys's which this stage should be connected to upon instatiation
		 */
		[DataMember(IsRequired = true)]
		public string[] outputs;
	}

	/**
	 * A static class to read SerializedStage files from a file
	 */
	public static class PipelineSerializer {

		static readonly DataContractJsonSerializer _serializer = new DataContractJsonSerializer(typeof(SerializedPipeline), new Type[] {
			typeof(SerializedStage),
			typeof(SerializedConnectionInfo)
		});

		/**
		 * Attempt to read a file (in JSON) and create the corresponding set of IPipeable's
		 * The IPipeables have already been connected but not started
		 */
		public static IPipeable[] CreateFromFile(string fileName, Dictionary<string, object> scope) {
			var stream = new System.IO.FileStream(fileName, System.IO.FileMode.Open);
			using (stream) {
				var pipeline = (SerializedPipeline)_serializer.ReadObject(stream);
				// throw an exception here if something is a-miss in incoming data
				CheckSerializedPipeline(pipeline);
				var allStages = CreateFromSerialized(pipeline, scope);
				foreach (var node in pipeline.stageConnections) {
					//Logger.Log("Connecting node {0} to {1} outputs with mirror={2}", node.stageKey, node.outputs.Count(), node.mirrorOutputs);
					ConnectPipeables(allStages, node);
				}
				return allStages.Values.ToArray();
			}
		}

		static void CheckSerializedPipeline(SerializedPipeline pipeline) {
			// condition stages != null && stageConnections != null
			if (pipeline == null || pipeline.stages == null || pipeline.stageConnections == null)
				throw new Exception("Cannot create pipeline: pipeline, stages, or stageConnections was null");

			// condition: all stages.stageKey != null
			if (pipeline.stages.Select((x) => x.stageKey).Any((x) => string.IsNullOrEmpty(x)))
				throw new Exception("Cannot create pipeline: stages.stageKey was null or empty");

			// condition: all stages.className != null
			if (pipeline.stages.Select((x) => x.stageClass).Any((x) => string.IsNullOrEmpty(x)))
				throw new Exception("Cannot create pipeline: stages.stageClass was null or empty");

			// condition: all stages.arguments != null
			if (pipeline.stages.Select((x) => x.arguments).Any((x) => x == null))
				throw new Exception("Cannot create pipeline: stages.arguments was null");

			// condition: All keys in pipeline.stages should be unique
			if (pipeline.stages.Select((x) => x.stageKey).Distinct().Count() != pipeline.stages.Length)
				throw new Exception("Cannot create pipeline: stages.stageKey are not unique");

			// condition: all stageConnections.stageKey != null
			if (pipeline.stageConnections.Select((x) => x.stageKey).Any((x) => string.IsNullOrEmpty(x)))
				throw new Exception("Cannot create pipeline: stageConnections.stageKey was null or empty");

			// condition: all stageConnections.outputs != null
			if (pipeline.stageConnections.Select((x) => x.outputs).Any((x) => x == null))
				throw new Exception("Cannot create pipeline: stageConnections.outputs was null");

			// condition: All keys in stageConnections should be unique
			if (pipeline.stageConnections.Select((x) => x.stageKey).Distinct().Count() != pipeline.stageConnections.Length)
				throw new Exception("Cannot create pipeline: stages.stageKey are not unique");

			// condition: all stageKeys in stageConnections exist in stages
			// checked in ConnectPipeables by stages dictionary?

			// condition: all stages are used by at least one stageConnection
			// TODO warning maybe?
		}

		static void ConnectPipeables(Dictionary<string, IPipeable> stages, SerializedConnectionInfo node) {
			var stage = stages[node.stageKey];
			foreach (var outKey in node.outputs) {
				var output = stages[outKey];
				stage.Connect(output, node.mirrorOutputs);
			}
		}

		static Dictionary<string, IPipeable> CreateFromSerialized(SerializedPipeline pipeline, Dictionary<string, object> scope) {
			var allStages = new Dictionary<string, IPipeable>();
			foreach (var stage in pipeline.stages) {
				allStages.Add(stage.stageKey, CreatePipeableInstance(stage, scope));
			}
			return allStages;
		}

		static IPipeable CreatePipeableInstance(SerializedStage stage, Dictionary<string, object> scope) {
			var pipeableType = Type.GetType(stage.stageClass, true, false);
			if (!pipeableType.GetInterfaces().Contains(typeof(IPipeable)))
				throw new ArgumentException("stageClass: " + pipeableType.FullName + " must implement SharpBCI.IPipeable");

			for (int i = 0; i < stage.arguments.Length; i++) {
				var arg = stage.arguments[i];
				if (arg is string && scope.ContainsKey(((string)arg))) {
					stage.arguments[i] = scope[(string)arg];
				}
				// kludge b/c it parses doubles as decimals
				else if (arg is decimal) {
					stage.arguments[i] = Convert.ToDouble(arg);
				}
			}

			return (IPipeable)CreateFromParams(pipeableType, stage.arguments);
		}

		static object CreateFromParams(Type type, object[] arguments) {
			//Logger.Log("Trying to create {0} from arguments {1}", type, string.Join(",", arguments));

			var allConstructors = type.GetConstructors();

			var sameArrity = allConstructors
				.Where((x) => x.GetParameters().Length == arguments.Length);

			//Logger.Log(sameArrity.Count());

			var canCoerce = sameArrity
				.Where((x) => x.GetParameters().All((p) => CanConvert(arguments[p.Position], p.ParameterType)));

			//Logger.Log(canCoerce.Count());

			var constructor = canCoerce.OrderBy((x) => RankConstructor(x, arguments)).FirstOrDefault();

			//Logger.Log(constructor);

			// use Activator as last resort, will throw MissingMethodException if it fails to find one too
			if (constructor == null) {
				Logger.Warning("PipelineSerializer: Could not find matching constructor, falling back to Activator.CreateInstance");
				return Activator.CreateInstance(type, arguments);
			} else {
				return constructor.Invoke(Coerce(constructor, arguments));
			}
		}

		static object[] Coerce(ConstructorInfo constructor, object[] args) {
			var parameters = constructor.GetParameters();
			int n = parameters.Length;
			for (int i = 0; i < n; i++) {
				var p = parameters[i];
				var pType = p.ParameterType;
				var j = p.Position;
				args[j] = pType == args[j].GetType() ? args[j] : Convert.ChangeType(args[j], pType);
			}
			return args;
		}

		static int RankConstructor(ConstructorInfo constr, object[] args) {
			// return the number of conversions this constructor requires from args
			return constr.GetParameters().Count((x) => args[x.Position].GetType() != x.ParameterType);
		}

		static bool CanConvert(object arg, Type toType) {
			if (toType == typeof(object)) {
				//Logger.Log("CanConvert, toObj");
				return true;
			} else if (arg.GetType() == toType) {
				//Logger.Log("CanConvert, same type");
				return true;
			} else {
				return converters.ContainsKey(arg.GetType()) && converters[arg.GetType()](arg, toType);
			}
		}

		public delegate bool Converter(object arg, Type type);

		readonly static Dictionary<Type, Converter> converters = new Dictionary<Type, Converter> {
			{ typeof(decimal), ConvertNumber },
			{ typeof(double), ConvertNumber },
			{ typeof(float), ConvertNumber },
			{ typeof(byte), ConvertNumber },
			{ typeof(sbyte), ConvertNumber },
			{ typeof(short), ConvertNumber },
			{ typeof(ushort), ConvertNumber },
			{ typeof(int), ConvertNumber },
			{ typeof(uint), ConvertNumber },
			{ typeof(long), ConvertNumber },
			{ typeof(ulong), ConvertNumber },
		};

		static bool ConvertNumber(object arg, Type toType) {
			// TODO only allow cast if information won't be lost here
			return toType == typeof(decimal) 
				|| toType == typeof(double) 
				|| toType == typeof(float)
				|| toType == typeof(sbyte)
				|| toType == typeof(byte)
				|| toType == typeof(short)
				|| toType == typeof(ushort)
				|| toType == typeof(int)
				|| toType == typeof(uint)
				|| toType == typeof(long)
				|| toType == typeof(ulong);
		}
	}
}
