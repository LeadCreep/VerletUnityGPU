using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Kylii.Rope
{
	public abstract class VerletCollideBase : MonoBehaviour
	{
		#region Constants
		public const int MAX_COLLISIONS = 2048;
		public const int MAX_COLLIDERS = 16;
		public const float COLLISION_RADIUS = 0.2f;
		public const int COLLIDER_BUFFER_SIZE = 8;
		#endregion

		#region Vairables
		[Header("Verlet Parameters")]
		[SerializeField] protected ComputeShader computeShader;

		[SerializeField, Range(1, 256)] protected uint nodeCount = 128;
		[SerializeField, Range(0.01f, 1f)] protected float edgeLength = 0.1f;
		protected float oldEdgeLength;

		protected uint edgeCount;

		[SerializeField, Range(1, 32)] protected int iterations = 8;
		[SerializeField, Range(0.85f, 1f)] protected float decay = 1f;
		[SerializeField] protected Vector3 gravity = new Vector3(0f, -1f, 0f);
		[SerializeField] protected bool useGravity = true;
		protected VerletSimulator simulator;
		private int numColliders;
		private bool shouldSnapshotCollision = true;

		[SerializeField] protected bool collide = true;
		[SerializeField, Range(0f, 0.49f)] protected float nodeRadius = 0.1f;

		private CollisionInfo[] collisionInfos;
		private CollisionInfo[] collisionInfosToSend;
		private Collider[] colliderBuffer;

		[SerializeField] private Vector3 multiplyDirections = Vector3.one;

		[Header("Cutting Behaviour")]
		[SerializeField] private bool destroyOnCut = false;
		[SerializeField] private bool cutOnCollide = false;
		private float cutCooldown = 0.2f;
		protected bool isCut = false;
		#endregion

		#region Properties
		public VerletSimulator Simulator { get { return simulator; } }
		#endregion

		#region Unity Methods
		protected void Awake()
		{
			collisionInfos = new CollisionInfo[MAX_COLLIDERS];
			for (int i = 0; i < collisionInfos.Length; i++)
			{
				collisionInfos[i] = new CollisionInfo
				{
					id = i,
					type = CollisionType.None,
					position = Vector3.zero,
					size = Vector3.zero,
					scale = Vector3.zero,
					wtl = Matrix4x4.zero,
					ltw = Matrix4x4.zero,
					numCollisions = 0,
				};
			}
			colliderBuffer = new Collider[COLLIDER_BUFFER_SIZE];
			oldEdgeLength = edgeLength;
		}

		protected virtual void Start()
		{
			edgeCount = nodeCount - 1;
		}

		protected virtual void Update()
		{
			if (nodeCount != simulator.NodeBuffer.count)
			{
				edgeCount = nodeCount - 1;
				simulator.Dispose();
				Start();
			}

			if (oldEdgeLength != edgeLength)
			{
				oldEdgeLength = edgeLength;
				simulator.Dispose();
				Start();
			}

			if (shouldSnapshotCollision && collide)
			{
				SnapshotCollision();

				collisionInfosToSend = new CollisionInfo[numColliders];
				for (int col = 0; col < numColliders; col++)
				{
					collisionInfosToSend[col] = collisionInfos[col];
				}

				simulator.SetFreezeDirections(multiplyDirections);
			}

			if (useGravity)
			{
				simulator.Gravity(computeShader, gravity, Time.deltaTime);
			}
			simulator.Step(computeShader, decay);
			for (int i = 0; i < iterations; i++)
			{
				simulator.Solve(computeShader);
				if (collide)
				{
					simulator.AdjustCollisions(computeShader, collisionInfosToSend, Time.deltaTime, nodeRadius);
				}
			}

			cutCooldown -= Time.deltaTime;
		}

		void FixedUpdate()
		{
			shouldSnapshotCollision = true;
		}

		protected void OnDestroy()
		{
			simulator.Dispose();
		}

		protected void OnDrawGizmos()
		{
			if (!Application.isPlaying || nodeCount == 0) return;

			Gizmos.color = Color.white;

			Edge[] edges = new Edge[edgeCount];
			Node[] nodes = new Node[nodeCount];
			simulator.NodeBuffer.GetData(nodes);
			simulator.EdgeBuffer.GetData(edges);

			for (int i = 0; i < edgeCount; i++)
			{
				Edge e = edges[i];
				if (e.nodeA > nodeCount || e.nodeB > nodeCount)
				{
					continue;
				}
				Node a = nodes[e.nodeA];
				Node b = nodes[e.nodeB];
				Gizmos.DrawLine(a.position, b.position);
			}
		}
		#endregion

		#region Private Methods
		private void SnapshotCollision()
		{
			for (int collInfo = 0; collInfo < collisionInfos.Length; collInfo++)
			{
				collisionInfos[collInfo].numCollisions = 0;
			}

			numColliders = 0;
			Node[] nodes = new Node[nodeCount];
			simulator.NodeBuffer.GetData(nodes);
			for (int i = 0; i < nodes.Length; i++)
			{
				if (nodes[i].position.y < -100f)
				{
					nodes = RemoveNodeFromArray(nodes, i);
					continue;
				}

				int collision = Physics.OverlapSphereNonAlloc(nodes[i].position,
					COLLISION_RADIUS,
					colliderBuffer,
					LayerMask.GetMask("VerletCollide", "VerletCut")
				);

				for (int j = 0; j < collision; j++)
				{
					Collider col = colliderBuffer[j];
					if (cutOnCollide && cutCooldown < 0 && col.gameObject.layer == LayerMask.NameToLayer("VerletCut"))
					{
						Cut(i);
						cutCooldown = 0.5f;
					}
					int id = col.GetInstanceID();

					int idx = -1;
					for (int k = 0; k < numColliders; k++)
					{
						if (collisionInfos[k].id == id)
						{
							idx = k;
							break;
						}
					}

					if (idx < 0)
					{
						CollisionInfo ci = collisionInfos[numColliders];
						ci.id = id;
						ci.position = col.transform.position;
						ci.wtl = col.transform.worldToLocalMatrix;
						ci.ltw = col.transform.localToWorldMatrix;
						ci.scale.x = ci.ltw.GetColumn(0).magnitude;
						ci.scale.y = ci.ltw.GetColumn(1).magnitude;
						ci.scale.z = ci.ltw.GetColumn(2).magnitude;
						ci.numCollisions = 1;
						ChangeBitUInt(ref nodes[i].collisionIndexes, numColliders, true);

						switch (col)
						{
							case SphereCollider s:
								ci.type = CollisionType.Sphere;
								ci.size.x = ci.size.y = ci.size.z = s.radius;
								break;
							case BoxCollider b:
								ci.type = CollisionType.Box;
								ci.size = b.size;
								break;
							default:
								ci.type = CollisionType.None;
								break;
						}

						collisionInfos[numColliders] = ci;
						numColliders++;
						if (numColliders >= MAX_COLLIDERS)
						{
							break;
						}
					}
					else
					{
						CollisionInfo ci = collisionInfos[idx];
						if (ci.numCollisions >= nodeCount)
						{
							continue;
						}

						ChangeBitUInt(ref nodes[i].collisionIndexes, idx, true);
						collisionInfos[idx] = ci;
						ci.numCollisions++;
					}
				}
				if (i >= MAX_COLLISIONS)
				{
					return;
				}
			}
			simulator.SetNodes(nodes);
			shouldSnapshotCollision = false;
		}
		#endregion

		#region Public Methods
		public static void ChangeBitUInt(ref uint value, int index, bool set)
		{
			if (set)
			{
				value |= (1U << index);
			}
			else
			{
				value &= ~(1U << index);
			}
		}

		public static bool GetBit(uint value, int index)
		{
			return (value & (1U << index)) != 0;
		}

		public void Cut(int segment = -1)
		{
			if (destroyOnCut)
			{
				Destroy(this);
				return;
			}

			if (edgeCount <= 0)
			{
				return;
			}

			if (segment == -1)
			{
				segment = (int)edgeCount / 2;
			}

			Edge[] edges = new Edge[edgeCount];
			simulator.EdgeBuffer.GetData(edges);

			for (int i = segment; i < edgeCount - 1; i++)
			{
				edges[i] = edges[i + 1];
			}

			edgeCount--;


			Edge[] newEdges = new Edge[edgeCount];
			Array.Copy(edges, newEdges, edgeCount);

			simulator.UpdateEdgesBuffer(newEdges);

			isCut = true;
		}

		public static Node[] RemoveNodeFromArray(Node[] nodes, int index)
		{
			Node[] newNodes = new Node[nodes.Length - 1];
			for (int i = 0; i < index; i++)
			{
				newNodes[i] = nodes[i];
			}
			for (int i = index + 1; i < nodes.Length; i++)
			{
				newNodes[i - 1] = nodes[i];
			}
			return newNodes;
		}

		#endregion
	}

	public enum CollisionType
	{
		None,
		Sphere,
		Box
	}

	[Serializable]
	[StructLayout(LayoutKind.Sequential)]
	public struct CollisionInfo
	{
		public int id;
		public CollisionType type; // Cf CollisionType
		public Vector3 position;
		public Vector3 size; // Collider Size
		public Vector3 scale; // GameObject Scale
		public Matrix4x4 wtl; // World to Local Transform
		public Matrix4x4 ltw; // Local to World Transform
		public int numCollisions; // Max 32 collisions
	}
}
