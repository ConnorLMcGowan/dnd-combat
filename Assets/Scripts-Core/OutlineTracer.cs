using System.Collections.Generic;

namespace DnDCombat.Core
{
    // Turns a set of cells into the closed loops that run around their outside edge, so an
    // outline can be drawn as one continuous line instead of separate pieces that overlap.
    //
    // Loops are made of grid CORNERS, not cells. Cell (x, z) has its corners at x and x + 1,
    // and z and z + 1, so corner (2, 3) is the bottom-left corner of cell (2, 3).
    // Each corner's Y is the Y of the cell whose edge starts there (for drawing at the right height).
    //
    // Every loop keeps the region on its LEFT as you walk it (looking down, X right and Z up):
    //   the outside edge goes anticlockwise, and the edge of any hole (a block inside the
    //   region) goes clockwise.
    // Only X and Z matter for what's inside; a cell is in if any cell in its column is.
    public static class OutlineTracer
    {
        public static List<List<GridPosition>> Trace(IEnumerable<GridPosition> cells)
        {
            List<List<GridPosition>> loops = new List<List<GridPosition>>();
            if (cells == null) return loops;

            // ---------- 1. Which columns are in, and the height of each ---------- //
            Dictionary<(int, int), int> columnY = new Dictionary<(int, int), int>();
            foreach (GridPosition c in cells)
            {
                if (!columnY.ContainsKey((c.X, c.Z))) columnY[(c.X, c.Z)] = c.Y;
            }

            // ---------- 2. Every outside side, as an arrow with the region on its left ---------- //
            List<Edge> edges = new List<Edge>();
            foreach (KeyValuePair<(int, int), int> column in columnY)
            {
                int x = column.Key.Item1;
                int z = column.Key.Item2;
                int y = column.Value;

                if (!columnY.ContainsKey((x, z - 1))) edges.Add(new Edge(x, z, x + 1, z, y));             // -Z side, heading +X
                if (!columnY.ContainsKey((x + 1, z))) edges.Add(new Edge(x + 1, z, x + 1, z + 1, y));     // +X side, heading +Z
                if (!columnY.ContainsKey((x, z + 1))) edges.Add(new Edge(x + 1, z + 1, x, z + 1, y));     // +Z side, heading -X
                if (!columnY.ContainsKey((x - 1, z))) edges.Add(new Edge(x, z + 1, x, z, y));             // -X side, heading -Z
            }

            // Which edges leave each corner (usually one, two where cells only touch at a corner)
            Dictionary<(int, int), List<int>> leaving = new Dictionary<(int, int), List<int>>();
            for (int i = 0; i < edges.Count; i++)
            {
                (int, int) start = (edges[i].FromX, edges[i].FromZ);
                if (!leaving.TryGetValue(start, out List<int> list))
                {
                    list = new List<int>();
                    leaving[start] = list;
                }
                list.Add(i);
            }

            // ---------- 3. Follow the arrows round until each loop closes ---------- //
            bool[] used = new bool[edges.Count];
            for (int first = 0; first < edges.Count; first++)
            {
                if (used[first]) continue;

                List<GridPosition> loop = new List<GridPosition>();
                int current = first;
                while (!used[current])
                {
                    used[current] = true;
                    Edge e = edges[current];
                    loop.Add(new GridPosition(e.FromX, e.Y, e.FromZ));
                    current = PickNext(edges, leaving, current);
                }
                loops.Add(loop);
            }

            return loops;
        }

        // Where two cells only touch at a corner, two edges leave that corner. Turning LEFT
        // first keeps each cell's loop separate, so the line never crosses itself.
        // The choice is the same every time round, so arriving back at the start always
        // picks the first edge again, and the loop closes.
        private static int PickNext(List<Edge> edges, Dictionary<(int, int), List<int>> leaving, int current)
        {
            Edge e = edges[current];
            int inX = e.ToX - e.FromX;
            int inZ = e.ToZ - e.FromZ;

            int best = -1;
            int bestRank = int.MaxValue;
            foreach (int candidate in leaving[(e.ToX, e.ToZ)])
            {
                Edge next = edges[candidate];
                int outX = next.ToX - next.FromX;
                int outZ = next.ToZ - next.FromZ;

                // 2D cross product: positive is a left turn, zero is straight on, negative is right
                int cross = inX * outZ - inZ * outX;
                int rank = cross > 0 ? 0 : (cross == 0 ? 1 : 2);
                if (rank < bestRank)
                {
                    best = candidate;
                    bestRank = rank;
                }
            }
            return best;
        }

        private readonly struct Edge
        {
            public readonly int FromX, FromZ, ToX, ToZ, Y;

            public Edge(int fromX, int fromZ, int toX, int toZ, int y)
            {
                FromX = fromX; FromZ = fromZ; ToX = toX; ToZ = toZ; Y = y;
            }
        }
    }
}
