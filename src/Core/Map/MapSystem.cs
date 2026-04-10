using RogueCardGame.Core.Deck;

namespace RogueCardGame.Core.Map;

/// <summary>
/// Types of rooms on the map.
/// </summary>
public enum RoomType
{
    Combat,        // Normal enemy encounter
    EliteCombat,   // Elite enemy encounter
    RestSite,      // Heal / upgrade cards / craft implants
    Shop,          // Buy cards, relics, potions, implants
    Event,         // Random event
    Boss,          // Act boss
    Treasure,      // Chest with relic
    ModStation,    // Hack & Salvage modification station
    DataTerminal,  // Scan upcoming rooms / gain intel
    Start,
    Victory
}

/// <summary>
/// A single node on the map graph.
/// </summary>
public class MapNode
{
    public int Id { get; }
    public int Row { get; }       // Y position (0 = start)
    public int Column { get; }    // X position within row
    public RoomType Type { get; set; }
    public List<int> ConnectedTo { get; } = []; // IDs of nodes this connects to
    public bool IsVisited { get; set; }
    public bool IsRevealed { get; set; }
    public string? EncounterId { get; set; }    // Enemy group ID for combat rooms
    public string? EventId { get; set; }        // Event ID for event rooms

    public MapNode(int id, int row, int column, RoomType type)
    {
        Id = id;
        Row = row;
        Column = column;
        Type = type;
    }
}

/// <summary>
/// A complete act map with branching paths.
/// </summary>
public class ActMap
{
    public int ActNumber { get; }
    public List<MapNode> Nodes { get; } = [];
    public MapNode? CurrentNode { get; set; }
    public int TotalRows { get; }

    public ActMap(int actNumber, int totalRows)
    {
        ActNumber = actNumber;
        TotalRows = totalRows;
    }

    public MapNode? GetNode(int id) => Nodes.FirstOrDefault(n => n.Id == id);

    public List<MapNode> GetRow(int row) => Nodes.Where(n => n.Row == row).ToList();

    public List<MapNode> GetReachableNodes()
    {
        if (CurrentNode == null)
            return Nodes.Where(n => n.Row == 0).ToList();
        return CurrentNode.ConnectedTo.Select(id => GetNode(id)!).ToList();
    }

    public bool CanMoveTo(int nodeId)
    {
        return GetReachableNodes().Any(n => n.Id == nodeId);
    }

    public void MoveTo(int nodeId)
    {
        var node = GetNode(nodeId);
        if (node == null) return;
        CurrentNode = node;
        node.IsVisited = true;
        node.IsRevealed = true;

        // Reveal adjacent future nodes
        foreach (var nextId in node.ConnectedTo)
        {
            var next = GetNode(nextId);
            if (next != null) next.IsRevealed = true;
        }
    }
}

/// <summary>
/// Generates procedural act maps with branching paths.
/// </summary>
public class MapGenerator
{
    private readonly SeededRandom _random;

    // Map layout constants
    private const int NodesPerRow = 4;   // Max width
    private const int MinPaths = 2;
    private const int MaxPaths = 4;

    public MapGenerator(SeededRandom random)
    {
        _random = random;
    }

    /// <summary>
    /// Generate a complete act map.
    /// </summary>
    public ActMap Generate(int actNumber, int rows = 15)
    {
        var map = new ActMap(actNumber, rows);
        int nextId = 0;

        // Row 0: Start (single node)
        var startNode = new MapNode(nextId++, -1, 1, RoomType.Start);
        map.Nodes.Add(startNode);

        // Generate rows 0..rows-2 (normal floors)
        List<MapNode> previousRow = [startNode];

        for (int row = 0; row < rows - 1; row++)
        {
            int nodeCount = CalculateNodeCount(row, rows);
            var currentRow = new List<MapNode>();

            for (int col = 0; col < nodeCount; col++)
            {
                var type = DetermineRoomType(row, rows, actNumber);
                var node = new MapNode(nextId++, row, col, type);
                currentRow.Add(node);
                map.Nodes.Add(node);
            }

            // Connect previous row to current row
            ConnectRows(previousRow, currentRow);
            previousRow = currentRow;
        }

        // Final row: Boss
        var bossNode = new MapNode(nextId++, rows - 1, 1, RoomType.Boss);
        map.Nodes.Add(bossNode);
        foreach (var prev in previousRow)
            prev.ConnectedTo.Add(bossNode.Id);

        // Reveal starting nodes
        startNode.IsRevealed = true;
        foreach (var nodeId in startNode.ConnectedTo)
        {
            var node = map.GetNode(nodeId);
            if (node != null) node.IsRevealed = true;
        }

        return map;
    }

    private int CalculateNodeCount(int row, int totalRows)
    {
        // Wider in middle, narrower at start/end
        float progress = (float)row / totalRows;
        if (progress < 0.15f) return _random.Next(2, 3);
        if (progress > 0.85f) return _random.Next(2, 3);
        return _random.Next(MinPaths, MaxPaths + 1);
    }

    private RoomType DetermineRoomType(int row, int totalRows, int act)
    {
        float progress = (float)row / totalRows;

        // Fixed room types at specific rows
        if (row == 0) return RoomType.Combat;
        if (row == totalRows / 2) return RoomType.RestSite; // Midpoint rest

        // Weighted random for other rows
        float roll = (float)_random.NextDouble();

        // Elite fights more common in later rows
        float eliteChance = progress * 0.2f;
        if (roll < eliteChance && row > 3) return RoomType.EliteCombat;
        roll -= eliteChance;

        // Room type distribution
        if (roll < 0.35f) return RoomType.Combat;
        if (roll < 0.50f) return RoomType.Event;
        if (roll < 0.60f) return RoomType.Shop;
        if (roll < 0.70f) return RoomType.RestSite;
        if (roll < 0.78f) return RoomType.ModStation;
        if (roll < 0.85f) return RoomType.DataTerminal;
        if (roll < 0.92f) return RoomType.Treasure;

        return RoomType.Combat;
    }

    private void ConnectRows(List<MapNode> previous, List<MapNode> current)
    {
        if (previous.Count == 0 || current.Count == 0) return;

        // Every node must have at least one connection forward
        foreach (var prev in previous)
        {
            // Connect to nearest column(s) in next row
            float relativeCol = previous.Count > 1
                ? (float)previous.IndexOf(prev) / (previous.Count - 1) * (current.Count - 1)
                : (current.Count - 1) / 2f;

            int primaryTarget = Math.Clamp((int)Math.Round(relativeCol), 0, current.Count - 1);
            prev.ConnectedTo.Add(current[primaryTarget].Id);

            // Random chance of additional branch
            if (_random.NextDouble() < 0.4 && current.Count > 1)
            {
                int alt = primaryTarget + (_random.Next(2) == 0 ? 1 : -1);
                alt = Math.Clamp(alt, 0, current.Count - 1);
                if (alt != primaryTarget && !prev.ConnectedTo.Contains(current[alt].Id))
                    prev.ConnectedTo.Add(current[alt].Id);
            }
        }

        // Ensure every current node has at least one incoming connection
        foreach (var cur in current)
        {
            bool hasIncoming = previous.Any(p => p.ConnectedTo.Contains(cur.Id));
            if (!hasIncoming)
            {
                var closest = previous
                    .OrderBy(p => Math.Abs(previous.IndexOf(p) * current.Count / previous.Count - current.IndexOf(cur)))
                    .First();
                closest.ConnectedTo.Add(cur.Id);
            }
        }
    }
}
