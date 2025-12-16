using System;
using System.Collections.Generic;
using Engine;
using Game;

namespace SurvivalcraftTerrainScanner {
    public class TerrainScanner {
        public SubsystemTerrain m_subsystemTerrain;
        public Terrain m_terrain;

        public TerrainScanner(SubsystemTerrain subsystemTerrain) {
            m_subsystemTerrain = subsystemTerrain;
            m_terrain = subsystemTerrain.Terrain;
        }

        public Point3 FindNearestNotAirBlock(Point3 start) {
            HashSet<Point3> hashSet = new();
            Queue<Point3> queue = new();
            queue.Enqueue(start);
            hashSet.Add(start);
            while (queue.Count > 0) {
                Point3 point = queue.Dequeue();
                TerrainChunk chunk = m_terrain.GetChunkAtCell(point.X, point.Z);
                if (chunk == null
                    || chunk.ThreadState < TerrainChunkState.InvalidLight) {
                    continue;
                }
                if (chunk.GetCellContentsFast(point.X & 15, point.Y, point.Z & 15) == 0) {
                    if (point.Y > 0) {
                        Point3 down = new(point.X, point.Y - 1, point.Z);
                        if (hashSet.Add(down)) {
                            queue.Enqueue(down);
                        }
                    }
                    if (point.Y < TerrainChunk.HeightMinusOne) {
                        Point3 up = new(point.X, point.Y + 1, point.Z);
                        if (hashSet.Add(up)) {
                            queue.Enqueue(up);
                        }
                    }
                    Point3 north = new(point.X + 1, point.Y, point.Z);
                    if (hashSet.Add(north)) {
                        queue.Enqueue(north);
                    }
                    Point3 south = new(point.X - 1, point.Y, point.Z);
                    if (hashSet.Add(south)) {
                        queue.Enqueue(south);
                    }
                    Point3 east = new(point.X, point.Y, point.Z + 1);
                    if (hashSet.Add(east)) {
                        queue.Enqueue(east);
                    }
                    Point3 west = new(point.X, point.Y, point.Z - 1);
                    if (hashSet.Add(west)) {
                        queue.Enqueue(west);
                    }
                }
                else {
                    return point;
                }
            }
            return new Point3(int.MinValue);
        }

        public int FindTopmostHeight(int x, int z) {
            TerrainChunk chunk = m_terrain.GetChunkAtCell(x, z);
            if (chunk == null
                || chunk.ThreadState < TerrainChunkState.InvalidLight) {
                return int.MinValue;
            }
            int start = ((x & 15) << 8) + ((z & 15) << 12);
            for (int y = TerrainChunk.HeightMinusOne; y >= 0; y--) {
                if (Terrain.ExtractContents(chunk.Cells[start + y]) != 0) {
                    return y;
                }
            }
            return int.MinValue;
        }

        // 针对每个法线方向（0-5），其垂直的4个切线方向在 CellFace 的 faceToPoint3 中的索引
        // 例如：法线是 +X (0)，切线可以是 Y+, Y-, Z+, Z-
        static readonly int[][] Tangents = [
            [1, 3, 4, 5], // Normal +Z
            [0, 2, 4, 5], // Normal +X
            [1, 3, 4, 5], // Normal -Z
            [0, 2, 4, 5], // Normal -X
            [0, 1, 2, 3], // Normal +Y
            [0, 1, 2, 3] // Normal -Y
        ];

        // 基本由Gemini完成
        public void DigBlocks(Point3 start, HashSet<int> targets, Action<int, Point3> afterDig, float range = float.PositiveInfinity) {
            HashSet<CellFace> visited = new(); // 记录访问过的表面（防止在非目标块上死循环）
            Queue<CellFace> queue = new();
            Vector3 startVector3 = new(start);
            float rangeSquared = range * range;
            // 1. 初始化：找到 start 初始暴露的所有面
            for (int i = 0; i < 6; i++) {
                Point3 neighbor = start + CellFace.FaceToPoint3(i);
                // 只有当邻居是空气时，这一面才是有效的起始表面
                if (IsValidAir(neighbor)) {
                    queue.Enqueue(new CellFace(start.X, start.Y, start.Z, i));
                }
            }
            while (queue.Count > 0) {
                CellFace current = queue.Dequeue();
                Point3 currentPoint3 = current.Point;
                int currentBlockValue = m_terrain.GetCellValueFast(currentPoint3.X, currentPoint3.Y, currentPoint3.Z);
                // 如果这个方块已经被别人挖了（变成空气了），这个面就不存在了，跳过
                if (currentBlockValue == 0) {
                    continue;
                }
                // 如果这个面已经爬过，跳过
                if (visited.Contains(current)) {
                    continue;
                }
                // === 情况 A: 当前方块是目标，需要挖掘 ===
                if (targets.Contains(currentBlockValue)) {
                    DigContinuousBlocks(currentPoint3, currentBlockValue, afterDig, queue);
                }
                // === 情况 B: 当前方块是其他实体，不能挖，只能爬行 ===
                else {
                    visited.Add(current);
                    Point3 direction = CellFace.FaceToPoint3(current.Face);
                    // 尝试向4个切线方向爬行
                    foreach (int tangent in Tangents[current.Face]) {
                        Point3 tangentPoint3 = CellFace.FaceToPoint3(tangent);
                        // 我们需要判断与当前面相邻的下一个面在哪里。
                        // 只有三种几何情况：
                        Point3 neighbor = currentPoint3 + tangentPoint3;
                        if (rangeSquared != float.PositiveInfinity
                            && Vector3.DistanceSquared(neighbor, startVector3) > rangeSquared) {
                            continue;
                        }
                        Point3 diagonal = neighbor + direction;
                        // 1. 凹角 (Concave/Inner Corner)
                        // 如果“前上方”（对角线位置）有方块，说明墙角拐上去了
                        if (IsValidNotAir(diagonal)) {
                            // 新的表面在 diagonal 上，法线指向当前行进方向的反方向
                            queue.Enqueue(new CellFace(diagonal.X, diagonal.Y, diagonal.Z, CellFace.OppositeFace(tangent)));
                            continue;
                        }
                        if (!m_terrain.IsCellValid(neighbor.X, neighbor.Y, neighbor.Z)) {
                            continue;
                        }
                        TerrainChunk neighborChunk = m_terrain.GetChunkAtCell(neighbor.X, neighbor.Z);
                        if (neighborChunk == null
                            || neighborChunk.ThreadState < TerrainChunkState.InvalidLight) {
                            continue;
                        }
                        queue.Enqueue(
                            neighborChunk.GetCellContentsFast(neighbor.X & 15, neighbor.Y, neighbor.Z & 15) == 0
                                // 2. 凸角 (Convex/Outer Corner)
                                // 如果“前上方”是空气，且“前方”是空气
                                // 表面延续到邻居，法线方向不变
                                ? new CellFace(currentPoint3.X, currentPoint3.Y, currentPoint3.Z, tangent)
                                // 3. 平面 (Flat)
                                // 如果“前上方”是空气，且“前方”是方块
                                : new CellFace(neighbor.X, neighbor.Y, neighbor.Z, current.Face)
                        );
                    }
                }
            }
        }

        public void DigContinuousBlocks(Point3 start, int target, Action<int, Point3> afterDig, Queue<CellFace> surfaceQueue) {
            Queue<Point3> veinQueue = new();
            veinQueue.Enqueue(start);
            // 入队时立即设为空气，既代表“已挖除”，又防止重复入队，因此也不再需要用一个HashSet来记录已挖掘位置
            m_terrain.SetCellValueFast(start.X, start.Y, start.Z, 0);
            afterDig(target, start);
            while (veinQueue.Count > 0) {
                Point3 current = veinQueue.Dequeue();
                // 检查 current 的 6 个邻居
                for (int i = 0; i < 6; i++) {
                    Point3 neighbor = current + CellFace.FaceToPoint3(i);
                    if (!m_terrain.IsCellValid(neighbor.X, neighbor.Y, neighbor.Z)) {
                        continue;
                    }
                    TerrainChunk neighborChunk = m_terrain.GetChunkAtCell(neighbor.X, neighbor.Z);
                    if (neighborChunk == null
                        || neighborChunk.ThreadState < TerrainChunkState.InvalidLight) {
                        continue;
                    }
                    int neighborBlock = neighborChunk.GetCellValueFast(neighbor.X & 15, neighbor.Y, neighbor.Z & 15);
                    if (neighborBlock == target) {
                        // 1. 发现相连的同类目标，挖掉，继续挖掘
                        neighborChunk.SetCellValueFast(neighbor.X & 15, neighbor.Y, neighbor.Z & 15, 0);
                        afterDig(target, neighbor);
                        veinQueue.Enqueue(neighbor);
                    }
                    else if (neighborBlock != 0) {
                        // 2. 发现相连的其他实体 (非空气，非目标) -> 暴露出新表面
                        // neighbor 是实体，它面向 current (现在是空气) 的面暴露了
                        // 该面的法线方向指向 current，即 i 的反方向
                        surfaceQueue.Enqueue(new CellFace(neighbor.X, neighbor.Y, neighbor.Z, CellFace.OppositeFace(i)));
                    }
                }
            }
        }

        // 在实体方块表面爬行，寻找并统计连续的目标方块，但不破坏地形
        public void ScanBlocks(Point3 start, HashSet<int> targets, Action<int, Point3, int> callback, float range = float.PositiveInfinity) {
            HashSet<CellFace> visitedFaces = new(); // 记录访问过的表面（用于爬行逻辑）
            HashSet<Point3> scannedBlocks = new(); // 记录已经统计过的目标方块坐标（防止重复计数）
            Queue<CellFace> queue = new();
            Vector3 startVector3 = new(start);
            float rangeSquared = range * range;
            // 1. 初始化：找到 start 初始暴露的所有面
            for (int i = 0; i < 6; i++) {
                Point3 neighbor = start + CellFace.FaceToPoint3(i);
                if (IsValidAir(neighbor)) {
                    queue.Enqueue(new CellFace(start.X, start.Y, start.Z, i));
                }
            }
            while (queue.Count > 0) {
                CellFace current = queue.Dequeue();
                Point3 currentPoint3 = current.Point;
                // 检查方块是否有效
                int currentBlockValue = m_terrain.GetCellValueFast(currentPoint3.X, currentPoint3.Y, currentPoint3.Z);
                if (currentBlockValue == 0) {
                    continue; // 如果变成了空气（可能被其他逻辑修改），跳过
                }
                // 检查面是否爬过
                if (visitedFaces.Contains(current)) {
                    continue;
                }
                // 如果是目标方块，且从未被任何一次扫描统计过
                if (targets.Contains(currentBlockValue)
                    && !scannedBlocks.Contains(currentPoint3)) {
                    // 启动泛洪搜索，统计这一“脉”矿的数量，并标记所有相关方块为已扫描
                    int count = CountContinuousBlocks(currentPoint3, currentBlockValue, scannedBlocks);
                    // 回调：返回 值, 起始位置, 数量
                    callback(currentBlockValue, currentPoint3, count);
                }
                // 无论是目标方块还是普通方块，在 Scan 模式下它依然是实体，
                // 我们需要标记这个面为已访问，并继续在其表面爬行以寻找更多目标。
                visitedFaces.Add(current);
                // === 以下是通用的爬行逻辑 (与 DigBlocks 保持一致) ===
                Point3 direction = CellFace.FaceToPoint3(current.Face);
                foreach (int tangent in Tangents[current.Face]) {
                    Point3 tangentPoint3 = CellFace.FaceToPoint3(tangent);
                    Point3 neighbor = currentPoint3 + tangentPoint3;
                    // 距离检查
                    if (rangeSquared != float.PositiveInfinity
                        && Vector3.DistanceSquared(neighbor, startVector3) > rangeSquared) {
                        continue;
                    }
                    Point3 diagonal = neighbor + direction;
                    // 1. 凹角
                    if (IsValidNotAir(diagonal)) {
                        queue.Enqueue(new CellFace(diagonal.X, diagonal.Y, diagonal.Z, CellFace.OppositeFace(tangent)));
                        continue;
                    }
                    if (!m_terrain.IsCellValid(neighbor.X, neighbor.Y, neighbor.Z)) {
                        continue;
                    }
                    TerrainChunk neighborChunk = m_terrain.GetChunkAtCell(neighbor.X, neighbor.Z);
                    if (neighborChunk == null
                        || neighborChunk.ThreadState < TerrainChunkState.InvalidLight) {
                        continue;
                    }
                    queue.Enqueue(
                        neighborChunk.GetCellContentsFast(neighbor.X & 15, neighbor.Y, neighbor.Z & 15) == 0
                            // 2. 凸角
                            ? new CellFace(currentPoint3.X, currentPoint3.Y, currentPoint3.Z, tangent)
                            // 3. 平面
                            : new CellFace(neighbor.X, neighbor.Y, neighbor.Z, current.Face)
                    );
                }
            }
        }

        public int CountContinuousBlocks(Point3 start, int target, HashSet<Point3> scannedBlocks) {
            Queue<Point3> veinQueue = new();
            veinQueue.Enqueue(start);
            // 立即标记起始点
            scannedBlocks.Add(start);
            int count = 0;
            while (veinQueue.Count > 0) {
                Point3 current = veinQueue.Dequeue();
                count++;
                // 检查 6 个邻居
                for (int i = 0; i < 6; i++) {
                    Point3 neighbor = current + CellFace.FaceToPoint3(i);
                    // 基础检查
                    if (!m_terrain.IsCellValid(neighbor.X, neighbor.Y, neighbor.Z)) {
                        continue;
                    }
                    // 已经在全局扫描列表中，跳过（避免重复计数和死循环）
                    if (scannedBlocks.Contains(neighbor)) {
                        continue;
                    }
                    TerrainChunk neighborChunk = m_terrain.GetChunkAtCell(neighbor.X, neighbor.Z);
                    if (neighborChunk == null
                        || neighborChunk.ThreadState < TerrainChunkState.InvalidLight) {
                        continue;
                    }
                    int neighborBlock = neighborChunk.GetCellValueFast(neighbor.X & 15, neighbor.Y, neighbor.Z & 15);
                    // 只有当邻居也是目标方块时，才加入队列继续寻找
                    if (neighborBlock == target) {
                        scannedBlocks.Add(neighbor); // 入队前立即标记，防止重复入队
                        veinQueue.Enqueue(neighbor);
                    }
                    // 注意：这里不需要像 DigBlocks 那样处理 "非目标实体"，
                    // 因为我们不进行挖掘，不会暴露内部的新面，所以不需要关心内部的非目标方块。
                }
            }
            return count;
        }

        bool IsValidNotAir(Point3 p) {
            if (!m_terrain.IsCellValid(p.X, p.Y, p.Z)) {
                return false;
            }
            TerrainChunk chunk = m_terrain.GetChunkAtCell(p.X, p.Z);
            if (chunk == null
                || chunk.ThreadState < TerrainChunkState.InvalidLight) {
                return false;
            }
            return chunk.GetCellContentsFast(p.X & 15, p.Y, p.Z & 15) != 0;
        }

        bool IsValidAir(Point3 p) {
            if (!m_terrain.IsCellValid(p.X, p.Y, p.Z)) {
                return false;
            }
            TerrainChunk chunk = m_terrain.GetChunkAtCell(p.X, p.Z);
            if (chunk == null
                || chunk.ThreadState < TerrainChunkState.InvalidLight) {
                return false;
            }
            return chunk.GetCellContentsFast(p.X & 15, p.Y, p.Z & 15) == 0;
        }
    }
}