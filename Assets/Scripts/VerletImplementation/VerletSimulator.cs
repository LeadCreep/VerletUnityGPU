using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Kylii.Rope
{
	public class VerletSimulator : IDisposable
	{
		#region Variables
		protected ComputeBuffer nodeBufferRead, nodeBufferWrite, edgeBuffer, collisionBuffer;
		protected int nodesCount, edgesCount;
		protected Vector3 multiplyDirections;
		#endregion

		#region Properties
		public ComputeBuffer EdgeBuffer { get { return edgeBuffer; } }
		public ComputeBuffer NodeBuffer { get { return nodeBufferRead; } }
		#endregion

		#region Public Methods
		public VerletSimulator(Node[] nodes, Edge[] edges)
		{
			Init(nodes, edges);
		}

		public void Init(Node[] nodes, Edge[] edges)
		{
			nodesCount = nodes.Length;
			edgesCount = edges.Length;

			nodeBufferRead = new ComputeBuffer(nodesCount, Marshal.SizeOf(typeof(Node)));
			nodeBufferWrite = new ComputeBuffer(nodesCount, Marshal.SizeOf(typeof(Node)));
			edgeBuffer = new ComputeBuffer(edgesCount, Marshal.SizeOf(typeof(Edge)));
			collisionBuffer = new ComputeBuffer(VerletCollideBase.MAX_COLLIDERS, Marshal.SizeOf(typeof(CollisionInfo)));

			nodeBufferRead.SetData(nodes);
			nodeBufferWrite.SetData(nodes);
			edgeBuffer.SetData(edges);
		}

		public void SetFreezeDirections(Vector3 dirMultiplier)
		{
			multiplyDirections = dirMultiplier;
		}

		public void SetNodes(Node[] nodes)
		{
			//if (nodes.Length != nodesCount)
			//{
			//	throw new ArgumentException($"Node count mismatch: expected {nodesCount}, got {nodes.Length}");
			//}
			nodeBufferRead.SetData(nodes);
			nodeBufferWrite.SetData(nodes);
		}

		public void Step(ComputeShader compute, float decay = 1f)
		{
			int kernel = compute.FindKernel("Step");
			compute.GetKernelThreadGroupSizes(kernel, out uint tx, out uint ty, out uint tz);

			compute.SetBuffer(kernel, "_Nodes", nodeBufferRead);
			compute.SetInt("_NodesCount", nodesCount);
			compute.SetVector("_DirectionMultiply", multiplyDirections);

			compute.SetFloat("_Decay", decay);

			compute.Dispatch(kernel, Mathf.FloorToInt(nodesCount / (int)tx) + 1, (int)ty, (int)tz);
		}

		public void Solve(ComputeShader compute, bool updateBuffers = true)
		{
			int kernel = compute.FindKernel("Solve");
			compute.GetKernelThreadGroupSizes(kernel, out uint tx, out uint ty, out uint tz);

			if (updateBuffers)
			{
				compute.SetBuffer(kernel, "_NodesRead", nodeBufferRead);
				compute.SetBuffer(kernel, "_Nodes", nodeBufferWrite);
				compute.SetInt("_NodesCount", nodesCount);

				compute.SetBuffer(kernel, "_Edges", edgeBuffer);
				compute.SetInt("_EdgesCount", edgesCount);

				compute.SetVector("_DirectionMultiply", multiplyDirections);
			}

			compute.Dispatch(kernel, Mathf.FloorToInt(nodesCount / (int)tx) + 1, (int)ty, (int)tz);

			SwapBuffer(ref nodeBufferRead, ref nodeBufferWrite);
		}

		public void FixOne(ComputeShader compute, int id, Vector3 point)
		{
			int kernel = compute.FindKernel("FixOne");
			compute.GetKernelThreadGroupSizes(kernel, out uint tx, out uint ty, out uint tz);

			compute.SetBuffer(kernel, "_Nodes", nodeBufferRead);
			compute.SetInt("_FixedID", id);
			compute.SetVector("_FixedPoint", point);

			compute.Dispatch(kernel, Mathf.FloorToInt(nodesCount / (int)tx) + 1, (int)ty, (int)tz);
		}

		public void UnfixOne(ComputeShader compute, int id)
		{
			int kernel = compute.FindKernel("UnfixOne");
			compute.GetKernelThreadGroupSizes(kernel, out uint tx, out uint ty, out uint tz);

			compute.SetBuffer(kernel, "_Nodes", nodeBufferRead);
			compute.SetInt("_FixedID", id);

			compute.Dispatch(kernel, Mathf.FloorToInt(nodesCount / (int)tx) + 1, (int)ty, (int)tz);
		}

		public void Gravity(ComputeShader compute, Vector3 gravity, float dt)
		{
			int kernel = compute.FindKernel("Gravity");
			compute.GetKernelThreadGroupSizes(kernel, out uint tx, out uint ty, out uint tz);

			compute.SetBuffer(kernel, "_Nodes", nodeBufferRead);
			compute.SetInt("_NodesCount", nodesCount);

			compute.SetVector("_Gravity", gravity);
			compute.SetFloat("_DT", dt);

			compute.SetVector("_DirectionMultiply", multiplyDirections);

			compute.Dispatch(kernel, Mathf.FloorToInt(nodesCount / (int)tx) + 1, (int)ty, (int)tz);
		}

		public void AdjustCollisions(ComputeShader compute, CollisionInfo[] collisions, float dt, float nodeRadius)
		{
			int kernel = compute.FindKernel("AdjustCollisions");
			compute.GetKernelThreadGroupSizes(kernel, out uint tx, out uint ty, out uint tz);

			compute.SetBuffer(kernel, "_Nodes", nodeBufferRead);
			compute.SetInt("_NodesCount", nodesCount);

			collisionBuffer.SetData(collisions);

			compute.SetInt("_CollisionsCount", collisions.Length);
			compute.SetBuffer(kernel, "_Collisions", collisionBuffer);
			compute.SetFloat("_DT", dt);
			compute.SetFloat("_NodeRadius", nodeRadius);

			compute.SetVector("_DirectionMultiply", multiplyDirections);

			compute.Dispatch(kernel, Mathf.FloorToInt(nodesCount / (int)tx) + 1, (int)ty, (int)tz);
		}

		public void UpdateEdgesBuffer(Edge[] edges)
		{
			edgesCount = edges.Length;
			edgeBuffer.SetData(edges);
		}

		public void Dispose()
		{
			ReleaseBuffer(ref nodeBufferRead);
			ReleaseBuffer(ref nodeBufferWrite);
			ReleaseBuffer(ref edgeBuffer);
		}
		#endregion

		#region Private Methods
		protected void SwapBuffer(ref ComputeBuffer a, ref ComputeBuffer b)
		{
			(b, a) = (a, b);
		}

		protected void ReleaseBuffer(ref ComputeBuffer buffer)
		{
			if (buffer != null)
			{
				buffer.Release();
			}
			buffer = null;
		}
		#endregion
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct Node
	{
		public Vector3 position;
		public Vector3 previousPosition;
		public float decay;
		public uint stable;
		public uint collisionIndexes;
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct Edge
	{
		public int nodeA;
		public int nodeB;
		public float length;

		public Edge(int a, int b, float len)
		{
			nodeA = a;
			nodeB = b;
			length = len;
		}
	}
}
