using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Kylii.Rope
{
	public class Rope : VerletCollideBase
	{
		[Header("Rope Parameters")]
		[SerializeField] private Transform startPoint;
		[SerializeField] private Transform endPoint;
		[SerializeField] private bool startPointKinematic = true;
		[SerializeField] private bool endPointKinematic = true;

		private Node[] nodes;

		public Transform StartPoint { get { return startPoint; } }

		private void PreAwake(Node[] newNodes)
		{
			nodes = newNodes;
		}

		protected override void Start()
		{
			if (nodes != null)
			{
				nodeCount = (uint)nodes.Length;
			}
			else
			{
				nodes = new Node[nodeCount];
				for (int i = 0; i < nodeCount; i++)
				{
					Node n = nodes[i];
					Vector3 p = Vector3.Lerp(startPoint.position, endPoint.position, (float)i / ( nodeCount - 1 ));
					n.position = n.previousPosition = p;
					n.decay = 1f;
					n.collisionIndexes = 0U;
					nodes[i] = n;
				}
			}
			edgeCount = nodeCount - 1;
			Edge[] edges = new Edge[edgeCount];
			for (int i = 0; i < edgeCount; i++)
			{
				Edge e = edges[i];
				e.nodeA = i;
				e.nodeB = i + 1;
				e.length = edgeLength;
				edges[i] = e;
			}

			simulator = new VerletSimulator(nodes, edges);

			base.Start();
		}

		protected override void Update()
		{
			base.Update();

			//if (Input.GetKeyDown(KeyCode.Space) && !isCut)
			//{
			//	Cut();
			//}

			if (startPointKinematic)
			{
				simulator.FixOne(computeShader, 0, startPoint.position);
			}
			else
			{
				simulator.UnfixOne(computeShader, 0);
				Node[] node = new Node[nodeCount];
				simulator.NodeBuffer.GetData(node);
				startPoint.position = node[0].position;
			}

			if (endPointKinematic)
			{
				simulator.FixOne(computeShader, (int)nodeCount - 1, endPoint.position);
			}
			else
			{
				simulator.UnfixOne(computeShader, (int)nodeCount - 1);
				Node[] node = new Node[nodeCount];
				simulator.NodeBuffer.GetData(node);
				endPoint.position = node[nodeCount - 1].position;
			}
		}
	}
}
