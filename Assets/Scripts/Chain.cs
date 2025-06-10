using UnityEngine;

namespace Kylii.Rope
{
	public class Chain : VerletCollideBase
	{
		[Header("Chain Parameters")]
		[SerializeField] private Transform anchor;

		protected override void Start()
		{
			base.Start();

			Node[] nodes = new Node[nodeCount];
			for (int i = 0; i < nodeCount; i++)
			{
				Node n = nodes[i];
				Vector3 p = transform.position;
				n.position = n.previousPosition = p;
				n.decay = 1f;
				n.collisionIndexes = 0U;
				nodes[i] = n;
			}

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
		}

		protected override void Update()
		{
			base.Update();
			simulator.FixOne(computeShader, 0, anchor.position);
		}
	}
}
