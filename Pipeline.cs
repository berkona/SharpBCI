﻿using System;

using System.Threading;
using System.Threading.Tasks;

using System.Collections.Generic;
using System.Collections.Concurrent;

namespace SharpBCI {

	public interface IPipeable {
		/**
		 * Set the input on this IPipeable to param input
		 */
		void SetInput(BlockingCollection<object> input);

		/**
		 * Connect the input of other to our output and allow for control of mirror data
		 */
		void Connect(IPipeable other, bool mirror);

		/**
		 * Connect the input of other to our output
		 * Note: mirror == false using this override
		 */
		void Connect(IPipeable other);

		/**
		 * Actually start doing work (i.e., promise to eventually start pushing data to connected pipeables)
		 */
		void Start(TaskFactory taskFactory, CancellationTokenSource cts);

		/**
		 * Require this IPipeable to stop, blocking until actually stopped
		 */
		void Stop();
	}

	public abstract class Pipeable : IPipeable {
		Task runningTask;
		CancellationTokenSource cts;
		CancellationToken token;

		BlockingCollection<object> input;

		List<BlockingCollection<object>> allOutputs = new List<BlockingCollection<object>>();

		// TODO do we need to limit the size of this buffer due to memory concerns?
		// BlockingCollection<object> output = new BlockingCollection<object>();

		public void SetInput(BlockingCollection<object> input) {
			this.input = input;
		}

		public void Connect(IPipeable other) {
			Connect(other, false);
		}

		public void Connect(IPipeable other, bool mirror) {
			BlockingCollection<object> output;
			if (mirror) {
				output = new BlockingCollection<object>();
				allOutputs.Add(output);
			} else {
				if (allOutputs.Count == 0) {
					allOutputs.Add(new BlockingCollection<object>());
				}
				output = allOutputs[0];
			}
			other.SetInput(output);
		}

		public virtual void Start(TaskFactory taskFactory, CancellationTokenSource cts) {
			this.cts = cts;
			this.token = cts.Token;
			runningTask = taskFactory.StartNew(Run);
		}

		public virtual void Stop() {
			// TODO do cooperative stopping here, or above us?
			runningTask.Wait();
		}

		void Run() {
			try {
				// case: producer
				if (input == null) {
					do {
						if (token.IsCancellationRequested) break;
					} while (Process(null));
				}
				// case: consumer (possibly a filter)
				else {
					foreach (var item in input.GetConsumingEnumerable(token)) {
						if (token.IsCancellationRequested || !Process(item))
							break;
					}
				}
			} catch (Exception e) {
				cts.Cancel();
				if (!(e is OperationCanceledException))
					throw;
			} finally {
				foreach (var output in allOutputs) { 
					output.CompleteAdding();
				}
			}
		}

		protected void Add(object item) {
			// TODO this will delay adding if using bounded buffers: explore benefits of spin waiting here
			foreach (var output in allOutputs) {
				output.Add(item, token);
			}
		}

		protected abstract bool Process(object item);
	}
}