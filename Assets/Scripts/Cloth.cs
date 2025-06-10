using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Kylii.Rope
{
	public class Cloth : VerletCollideBase
	{
		[Header("Cloth Parameters")]
		[SerializeField, Range(8, 256)] protected int rows = 64;
		[SerializeField, Range(8, 256)] protected int columns = 64;

		[SerializeField] protected Transform anchor;

		protected override void Start()
		{
			if (edgeLength < 2f * nodeRadius)
				Debug.LogWarning("edgeLength est trop petit");

			nodeCount = (uint)( rows * columns );

			Node[] nodes = new Node[nodeCount];

			float hCols = columns * 0.5f;

			for (int y = 0; y < rows; y++)
			{
				bool stable = ( y == 0 );
				int yoff = y * columns;

				for (int x = 0; x < columns; x++)
				{
					int idx = yoff + x;
					Node n = nodes[idx];
					n.position = n.previousPosition = transform.position + ( Vector3.down * y * edgeLength ) + ( Vector3.right * ( x - hCols ) * edgeLength );
					n.decay = 1f;
					n.stable = (uint)( stable ? 1 : 0 );
					n.collisionIndexes = 0U;
					nodes[idx] = n;
				}
			}

			List<Edge> edges = new List<Edge>();
			for (int y = 0; y < rows; y++)
			{
				int yoff = y * columns;
				if (y != rows - 1)
				{
					for (int x = 0; x < columns; x++)
					{
						int idx = yoff + x;
						if (x != columns - 1)
						{
							int right = idx + 1;
							edges.Add(new Edge(idx, right, edgeLength));
						}
						int down = idx + columns;
						edges.Add(new Edge(idx, down, edgeLength));
					}
				}
				else
				{
					for (int x = 0; x < columns - 1; x++)
					{
						int idx = yoff + x;
						int right = idx + 1;
						edges.Add(new Edge(idx, right, edgeLength));
					}
				}
			}

			edgeCount = (uint)edges.Count;

			simulator = new VerletSimulator(nodes, edges.ToArray());
		}

		protected override void Update()
		{
			base.Update();
		}
	}
}
