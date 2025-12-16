using System.Linq;
using Engine;
using Game;

namespace SurvivalcraftTerrainScanner {
    public class TerrainUpdater {
        public SubsystemTerrain m_subsystemTerrain;
        public Vector2 m_spawnPosition;
        public Terrain m_terrain;

        public Vector2 SpawnPosition {
            get => m_spawnPosition;
            set => m_spawnPosition = value;
        }

        public float GenerateRange {
            get => field;
            set {
                field = value;
                m_generateRangeSquared = value * value;
            }
        }

        public float m_generateRangeSquared;

        public TerrainUpdater(SubsystemTerrain subsystemTerrain) {
            m_subsystemTerrain = subsystemTerrain;
            m_terrain = subsystemTerrain.Terrain;
        }

        public void GenerateChunks(float generateRange, AnsiConsoleProgressBarReporter reporter) {
            GenerateRange = generateRange;
            Point2 point1 = Terrain.ToChunk(m_spawnPosition - new Vector2(GenerateRange));
            Point2 point2 = Terrain.ToChunk(m_spawnPosition + new Vector2(GenerateRange));
            int total1 = (point2.X - point1.X + 1) * (point2.Y - point1.Y + 1);
            reporter.ReportMaxValue(0, total1);
            reporter.ReportStart(0);
            int processed1 = 0;
            for (int i = point1.X; i <= point2.X; i++) {
                for (int j = point1.Y; j <= point2.Y; j++) {
                    Vector2 chunkCenter = new((i + 0.5f) * TerrainChunk.Size, (j + 0.5f) * TerrainChunk.Size);
                    if (Vector2.DistanceSquared(m_spawnPosition, chunkCenter) <= m_generateRangeSquared) {
                        m_terrain.AllocateChunk(i, j);
                    }
                    reporter?.ReportValue(0, ++processed1);
                }
            }
            reporter.ReportStop(0);
            int total2 = m_terrain.AllocatedChunks.Length;
            reporter.ReportMaxValue(1, total2);
            reporter.ReportStart(1);
            int processed2 = 0;
            foreach (TerrainChunk chunk in m_terrain.AllocatedChunks.OrderBy(chunk => Vector2.DistanceSquared(m_spawnPosition, chunk.Center))) {
                while (chunk.ThreadState < TerrainChunkState.InvalidLight) {
                    UpdateChunkSingleStep(chunk);
                }
                reporter.ReportValue(1, ++processed2);
            }
            reporter.ReportStop(1);
        }

        public void GenerateChunks(float generateRange, AnsiConsoleScanMultipleVirtualWorldsStatusReporter reporter) {
            GenerateRange = generateRange;
            Point2 point1 = Terrain.ToChunk(m_spawnPosition - new Vector2(GenerateRange));
            Point2 point2 = Terrain.ToChunk(m_spawnPosition + new Vector2(GenerateRange));
            int total1 = (point2.X - point1.X + 1) * (point2.Y - point1.Y + 1);
            reporter.Progress = $"分配区块 0/{total1}";
            int processed1 = 0;
            for (int i = point1.X; i <= point2.X; i++) {
                for (int j = point1.Y; j <= point2.Y; j++) {
                    Vector2 chunkCenter = new((i + 0.5f) * TerrainChunk.Size, (j + 0.5f) * TerrainChunk.Size);
                    if (Vector2.DistanceSquared(m_spawnPosition, chunkCenter) <= m_generateRangeSquared) {
                        m_terrain.AllocateChunk(i, j);
                    }
                    if (++processed1 % 100 == 1) {
                        reporter.Progress = $"分配区块 {processed1}/{total1}";
                    }
                }
            }
            int total2 = m_terrain.AllocatedChunks.Length;
            reporter.Progress = $"生成区块 0/{total2}";
            int processed2 = 0;
            foreach (TerrainChunk chunk in m_terrain.AllocatedChunks.OrderBy(chunk => Vector2.DistanceSquared(m_spawnPosition, chunk.Center))) {
                while (chunk.ThreadState < TerrainChunkState.InvalidLight) {
                    UpdateChunkSingleStep(chunk);
                }
                if (++processed2 % 30 == 1) {
                    reporter.Progress = $"分配区块 {processed2}/{total2}";
                }
            }
            reporter.Progress = $"分配区块 {processed2}/{total2}";
        }

        public void GenerateChunks(float generateRange) {
            GenerateRange = generateRange;
            Point2 point1 = Terrain.ToChunk(m_spawnPosition - new Vector2(GenerateRange));
            Point2 point2 = Terrain.ToChunk(m_spawnPosition + new Vector2(GenerateRange));
            for (int i = point1.X; i <= point2.X; i++) {
                for (int j = point1.Y; j <= point2.Y; j++) {
                    Vector2 chunkCenter = new((i + 0.5f) * TerrainChunk.Size, (j + 0.5f) * TerrainChunk.Size);
                    if (Vector2.DistanceSquared(m_spawnPosition, chunkCenter) <= m_generateRangeSquared) {
                        m_terrain.AllocateChunk(i, j);
                    }
                }
            }
            foreach (TerrainChunk chunk in m_terrain.AllocatedChunks.OrderBy(chunk => Vector2.DistanceSquared(m_spawnPosition, chunk.Center))) {
                while (chunk.ThreadState < TerrainChunkState.InvalidLight) {
                    UpdateChunkSingleStep(chunk);
                }
            }
        }

        public void UpdateChunkSingleStep(TerrainChunk chunk) {
            switch (chunk.ThreadState) {
                case TerrainChunkState.NotLoaded: {
                    if (m_subsystemTerrain.TerrainSerializer?.LoadChunk(chunk) ?? false) {
                        chunk.ThreadState = TerrainChunkState.InvalidLight;
                        chunk.WasUpgraded = true;
                        chunk.IsLoaded = true;
                    }
                    else {
                        chunk.ThreadState = TerrainChunkState.InvalidContents1;
                        chunk.WasUpgraded = true;
                    }
                    break;
                }
                case TerrainChunkState.InvalidContents1: {
                    m_subsystemTerrain.TerrainContentsGenerator.GenerateChunkContentsPass1(chunk);
                    chunk.ThreadState = TerrainChunkState.InvalidContents2;
                    chunk.WasUpgraded = true;
                    break;
                }
                case TerrainChunkState.InvalidContents2: {
                    m_subsystemTerrain.TerrainContentsGenerator.GenerateChunkContentsPass2(chunk);
                    chunk.ThreadState = TerrainChunkState.InvalidContents3;
                    chunk.WasUpgraded = true;
                    break;
                }
                case TerrainChunkState.InvalidContents3: {
                    m_subsystemTerrain.TerrainContentsGenerator.GenerateChunkContentsPass3(chunk);
                    chunk.ThreadState = TerrainChunkState.InvalidContents4;
                    chunk.WasUpgraded = true;
                    break;
                }
                case TerrainChunkState.InvalidContents4: {
                    m_subsystemTerrain.TerrainContentsGenerator.GenerateChunkContentsPass4(chunk);
                    chunk.ThreadState = TerrainChunkState.InvalidLight;
                    chunk.WasUpgraded = true;
                    break;
                }
            }
        }
    }
}