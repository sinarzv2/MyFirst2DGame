using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Level
{
    // Where a ground tile sits in its block: grass on top, fill, or the outlined bottom row...
    public enum GroundRow { Top, Fill, Bottom }

    // ...and the left end, the middle, or the right end.
    public enum GroundColumn { Left, Middle, Right }

    // Which of the nine ground tiles a cell needs.
    public readonly struct GroundPiece : IEquatable<GroundPiece>
    {
        public readonly GroundRow Row;
        public readonly GroundColumn Column;

        public GroundPiece(GroundRow row, GroundColumn column)
        {
            Row = row;
            Column = column;
        }

        public bool Equals(GroundPiece other) => Row == other.Row && Column == other.Column;
        public override bool Equals(object obj) => obj is GroundPiece other && Equals(other);
        public override int GetHashCode() => (int)Row * 3 + (int)Column;
        public override string ToString() => $"{Row} {Column}";
    }

    // A level drawn as text, one character per Tilemap cell, top row first:
    //
    //   origin -4 -8        <- the cell of the bottom-left character
    //   .....BBBB....
    //   AAAAABBBB.CCC
    //   AAAAABBBB.CCC
    //
    // '.' is empty; any other character is ground. A run of the same character is one block, so two
    // blocks side by side need different characters, and each gets its own outline (as when painted by
    // hand). Which tile each cell needs follows from its neighbours of the same block, so the edges
    // never have to be picked by hand. Only text and numbers here: Edit Mode tests use it directly,
    // and the Tools > Level menu (LevelLayoutMenu) turns it into tiles and back.
    public class LevelLayout
    {
        public const char Empty = '.';
        public const char Coin = '$';   // a coin in the middle of the cell; not ground
        private const string OriginWord = "origin";

        private static readonly string[] Header =
        {
            "// One character per Tilemap cell (2 x 2 units), top row first. '.' is empty, '$' a coin; any",
            "// other character is ground. A run of the same character is one block: blocks side by side need",
            "// different characters. Each block must be at least 2 cells wide and 2 tall.",
            "// The bottom-left character is the cell at \"origin x y\".",
            "// Tools > Level > Build From Layout paints this; Save Layout From Level writes it from the tiles.",
        };

        private readonly string[] _rows;   // _rows[0] is the top row

        public Vector2Int Origin { get; }
        public int Width => _rows[0].Length;
        public int Height => _rows.Length;

        public LevelLayout(Vector2Int origin, IReadOnlyList<string> rowsTopFirst)
        {
            if (rowsTopFirst.Count == 0 || rowsTopFirst.Any(r => r.Length != rowsTopFirst[0].Length || r.Length == 0))
            {
                throw new ArgumentException("rows must be non-empty and all as long");
            }

            Origin = origin;
            _rows = rowsTopFirst.ToArray();
        }

        // The character at a Tilemap cell; Empty outside the map.
        public char At(int x, int y)
        {
            var column = x - Origin.x;
            var row = Height - 1 - (y - Origin.y);
            if (column < 0 || column >= Width || row < 0 || row >= Height)
            {
                return Empty;
            }

            return _rows[row][column];
        }

        public static LevelLayout Parse(string text)
        {
            Vector2Int? origin = null;
            var rows = new List<string>();
            var lineNumber = 0;
            foreach (var raw in text.Split('\n'))
            {
                lineNumber++;
                var line = raw.TrimEnd();   // also drops the '\r' of Windows line ends
                if (line.Length == 0 || line.StartsWith("//"))
                {
                    continue;
                }

                if (line.StartsWith(OriginWord + " "))
                {
                    var parts = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length != 3 || !TryInt(parts[1], out var x) || !TryInt(parts[2], out var y))
                    {
                        throw new FormatException($"line {lineNumber}: write the origin as \"origin x y\", e.g. \"origin -4 -8\"");
                    }

                    origin = new Vector2Int(x, y);
                    continue;
                }

                if (line.Any(char.IsWhiteSpace))
                {
                    throw new FormatException($"line {lineNumber}: a space in a row; write '{Empty}' for an empty cell");
                }

                if (rows.Count > 0 && line.Length != rows[0].Length)
                {
                    throw new FormatException($"line {lineNumber}: {line.Length} characters, but the first row has {rows[0].Length}: every row must be as long");
                }

                rows.Add(line);
            }

            if (origin == null)
            {
                throw new FormatException("no \"origin x y\" line: it says which cell the bottom-left character is");
            }

            if (rows.Count == 0)
            {
                throw new FormatException("no rows");
            }

            return new LevelLayout(origin.Value, rows);
        }

        private static bool TryInt(string s, out int value) =>
            int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

        public string ToText()
        {
            var text = new StringBuilder();
            foreach (var line in Header)
            {
                text.Append(line).Append('\n');
            }

            text.Append($"{OriginWord} {Origin.x} {Origin.y}\n");
            foreach (var row in _rows)
            {
                text.Append(row).Append('\n');
            }

            return text.ToString();
        }

        // Every cell holding one of `wanted`'s kind, bottom row first.
        private IEnumerable<Vector2Int> Cells(Func<char, bool> wanted)
        {
            for (var y = Origin.y; y < Origin.y + Height; y++)
            {
                for (var x = Origin.x; x < Origin.x + Width; x++)
                {
                    if (wanted(At(x, y)))
                    {
                        yield return new Vector2Int(x, y);
                    }
                }
            }
        }

        private IEnumerable<Vector2Int> GroundCells() => Cells(c => c != Empty && c != Coin);

        public List<Vector2Int> CoinCells() => Cells(c => c == Coin).ToList();

        // The tile a ground cell needs, from which of its four neighbours are of the same block.
        // A block one cell tall would need grass with the outline under it, and one cell wide both side
        // outlines: there are no such tiles, so those are problems.
        private GroundPiece PieceAt(Vector2Int cell, out string problem)
        {
            var c = At(cell.x, cell.y);
            bool Same(int dx, int dy) => At(cell.x + dx, cell.y + dy) == c;
            var top = !Same(0, 1);
            var bottom = !Same(0, -1);
            var left = !Same(-1, 0);
            var right = !Same(1, 0);

            problem = null;
            if (top && bottom)
            {
                problem = $"cell ({cell.x}, {cell.y}): block '{c}' is one cell tall there; a block needs 2 rows (grass, outlined bottom)";
            }
            else if (left && right)
            {
                problem = $"cell ({cell.x}, {cell.y}): block '{c}' is one cell wide there; a block needs 2 columns (left end, right end)";
            }

            var row = top ? GroundRow.Top : bottom ? GroundRow.Bottom : GroundRow.Fill;
            var column = left ? GroundColumn.Left : right ? GroundColumn.Right : GroundColumn.Middle;
            return new GroundPiece(row, column);
        }

        public List<string> Problems()
        {
            var problems = new List<string>();
            foreach (var cell in GroundCells())
            {
                PieceAt(cell, out var problem);
                if (problem != null)
                {
                    problems.Add(problem);
                }
            }

            return problems;
        }

        // The tile piece of every ground cell. Only for a layout without problems.
        public Dictionary<Vector2Int, GroundPiece> Pieces()
        {
            var pieces = new Dictionary<Vector2Int, GroundPiece>();
            foreach (var cell in GroundCells())
            {
                pieces[cell] = PieceAt(cell, out var problem);
                if (problem != null)
                {
                    throw new InvalidOperationException(problem);
                }
            }

            return pieces;
        }

        // The other way: the layout of painted tiles (and coins). Two cells side by side are one block
        // unless an edge lies between them (a right end, then a left end); the same up and down (a
        // bottom row under grass). Blocks are lettered A, B, C... from the left, never the same as a
        // neighbour.
        public static LevelLayout FromPieces(IReadOnlyDictionary<Vector2Int, GroundPiece> pieces,
            IEnumerable<Vector2Int> coins = null)
        {
            if (pieces.Count == 0)
            {
                throw new ArgumentException("no tiles");
            }

            var coinCells = (coins ?? Enumerable.Empty<Vector2Int>()).Distinct().ToList();
            var buried = coinCells.Where(pieces.ContainsKey).ToList();
            if (buried.Count > 0)
            {
                throw new ArgumentException($"a coin inside the ground, at cell ({buried[0].x}, {buried[0].y})");
            }

            // Union-find: every cell starts as its own block; joining two cells joins their blocks.
            var parent = pieces.Keys.ToDictionary(c => c, c => c);

            Vector2Int Find(Vector2Int c)
            {
                while (parent[c] != c)
                {
                    c = parent[c] = parent[parent[c]];
                }

                return c;
            }

            foreach (var pair in pieces)
            {
                var cell = pair.Key;
                var piece = pair.Value;
                if (piece.Column != GroundColumn.Right && pieces.TryGetValue(cell + Vector2Int.right, out var right)
                    && right.Column != GroundColumn.Left)
                {
                    parent[Find(cell)] = Find(cell + Vector2Int.right);
                }

                if (piece.Row != GroundRow.Top && pieces.TryGetValue(cell + Vector2Int.up, out var above)
                    && above.Row != GroundRow.Bottom)
                {
                    parent[Find(cell)] = Find(cell + Vector2Int.up);
                }
            }

            var blocks = pieces.Keys.GroupBy(Find)
                .OrderBy(block => block.Min(c => c.x))
                .ThenByDescending(block => block.Max(c => c.y))
                .ToList();
            var letters = new Dictionary<Vector2Int, char>();
            var next = 0;
            var sides = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
            foreach (var block in blocks)
            {
                var taken = new HashSet<char>();
                foreach (var cell in block)
                {
                    foreach (var side in sides)
                    {
                        if (letters.TryGetValue(cell + side, out var letter))
                        {
                            taken.Add(letter);
                        }
                    }
                }

                var chosen = 'A';
                for (var tries = 0; tries < 26; tries++)
                {
                    chosen = (char)('A' + next++ % 26);
                    if (!taken.Contains(chosen))
                    {
                        break;
                    }
                }

                foreach (var cell in block)
                {
                    letters[cell] = chosen;
                }
            }

            foreach (var cell in coinCells)
            {
                letters[cell] = Coin;
            }

            var all = pieces.Keys.Concat(coinCells).ToList();
            var minX = all.Min(c => c.x);
            var maxX = all.Max(c => c.x);
            var minY = all.Min(c => c.y);
            var maxY = all.Max(c => c.y);
            var rows = new List<string>();
            for (var y = maxY; y >= minY; y--)
            {
                var row = new StringBuilder();
                for (var x = minX; x <= maxX; x++)
                {
                    row.Append(letters.TryGetValue(new Vector2Int(x, y), out var letter) ? letter : Empty);
                }

                rows.Add(row.ToString());
            }

            return new LevelLayout(new Vector2Int(minX, minY), rows);
        }
    }
}
