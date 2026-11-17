using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Enemies;
using Level;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

// Editor-only (this folder has its own assembly, MyFirst2DGame.Editor, built for the Editor only:
// a game build has no UnityEditor). Adds Tools > Level to Unity's menu bar, with two ways between
// the level's text map (Assets/Levels/<scene>.txt, see LevelLayout) and the Tilemap of the open scene.
namespace LevelTools
{
    public static class LevelLayoutMenu
    {
        // The ground tile for each piece. Ground.png and GroundFill.png were sliced left to right, top
        // row first: Ground_0..2 = grass left, middle, right; Ground_3..5 = outlined bottom row.
        public static readonly IReadOnlyDictionary<GroundPiece, string> TilePaths = new Dictionary<GroundPiece, string>
        {
            [new GroundPiece(GroundRow.Top, GroundColumn.Left)] = "Assets/Tiles/Ground_0.asset",
            [new GroundPiece(GroundRow.Top, GroundColumn.Middle)] = "Assets/Tiles/Ground_1.asset",
            [new GroundPiece(GroundRow.Top, GroundColumn.Right)] = "Assets/Tiles/Ground_2.asset",
            [new GroundPiece(GroundRow.Fill, GroundColumn.Left)] = "Assets/Tiles/GroundFill_0.asset",
            [new GroundPiece(GroundRow.Fill, GroundColumn.Middle)] = "Assets/Tiles/GroundFill_1.asset",
            [new GroundPiece(GroundRow.Fill, GroundColumn.Right)] = "Assets/Tiles/GroundFill_2.asset",
            [new GroundPiece(GroundRow.Bottom, GroundColumn.Left)] = "Assets/Tiles/Ground_3.asset",
            [new GroundPiece(GroundRow.Bottom, GroundColumn.Middle)] = "Assets/Tiles/Ground_4.asset",
            [new GroundPiece(GroundRow.Bottom, GroundColumn.Right)] = "Assets/Tiles/Ground_5.asset",
        };

        public static string LayoutPath(string sceneName) => $"Assets/Levels/{sceneName}.txt";

        // Put wherever the layout has a '$' (a coin) or a '@' (an enemy), under a GameObject of this name.
        public const string CoinPrefabPath = "Assets/Prefabs/Coin.prefab";
        private const string CoinsParentName = "Coins";
        public const string EnemyPrefabPath = "Assets/Prefabs/Enemy.prefab";
        private const string EnemiesParentName = "Enemies";

        private const string Title = "Level Layout";

        // Text -> tiles. Replaces every tile of the Tilemap, then fits LevelBounds; one Undo step.
        [MenuItem("Tools/Level/Build From Layout")]
        private static void Build()
        {
            if (!TryFindTilemap(out var tilemap) || !TryLoadTiles(out var tiles))
            {
                return;
            }

            var path = LayoutPath(tilemap.gameObject.scene.name);
            if (!File.Exists(path))
            {
                Fail($"There is no layout file at {path}.\n\nUse Tools > Level > Save Layout From Level to write it from the tiles.");
                return;
            }

            Dictionary<Vector2Int, GroundPiece> wanted;
            List<Vector2Int> wantedCoins;
            List<Vector2Int> wantedEnemies;
            try
            {
                var layout = LevelLayout.Parse(File.ReadAllText(path));
                var problems = layout.Problems();
                if (problems.Count > 0)
                {
                    Fail($"{path} has {problems.Count} problem(s):\n" + string.Join("\n", problems.Take(8)));
                    return;
                }

                wanted = layout.Pieces();
                wantedCoins = layout.CoinCells();
                wantedEnemies = layout.EnemyCells();
            }
            catch (FormatException e)
            {
                Fail($"{path}: {e.Message}");
                return;
            }

            if (!TryLoadPrefab(CoinPrefabPath, wantedCoins, path, out var coinPrefab) ||
                !TryLoadPrefab(EnemyPrefabPath, wantedEnemies, path, out var enemyPrefab))
            {
                return;
            }

            // Painting where there is nothing loses nothing; changing or removing a tile, a coin or an
            // enemy does. Ask first, in case those were placed by hand and never saved to the text.
            ReadPieces(tilemap, tiles, out var current, out var unknown);
            var lost = current.Where(pair => !wanted.TryGetValue(pair.Key, out var piece) || !piece.Equals(pair.Value))
                .Select(pair => pair.Key).Concat(unknown)
                .Concat(CoinCellsInScene(tilemap).Where(cell => !wantedCoins.Contains(cell)))
                .Concat(EnemyCellsInScene(tilemap).Where(cell => !wantedEnemies.Contains(cell)))
                .ToList();
            if (lost.Count > 0 && !EditorUtility.DisplayDialog(Title,
                    $"{lost.Count} tile(s), coin(s) or enemies now in the scene are not in {path} and will be changed or removed, " +
                    $"for example at {Cells(lost)}.\n\nIf you placed them by hand, Cancel, then use Save Layout From Level first. " +
                    "(Edit > Undo can bring them back either way.)",
                    "Build anyway", "Cancel"))
            {
                return;
            }

            var group = Undo.GetCurrentGroup();
            Undo.RegisterCompleteObjectUndo(tilemap, "Build Level From Layout");
            tilemap.ClearAllTiles();
            tilemap.SetTiles(
                wanted.Keys.Select(cell => new Vector3Int(cell.x, cell.y, 0)).ToArray(),
                wanted.Values.Select(piece => tiles[piece]).ToArray());   // a Dictionary lists Keys and Values in the same order
            tilemap.CompressBounds();
            Place<Coin>(coinPrefab, CoinsParentName, wantedCoins, cell => CoinPosition(tilemap, cell));
            Place<Enemy>(enemyPrefab, EnemiesParentName, wantedEnemies, cell => EnemyPosition(tilemap, cell));

            var bounds = Object.FindObjectOfType<LevelBounds>();
            if (bounds != null)
            {
                bounds.FitInEditor();
            }

            Undo.CollapseUndoOperations(group);   // one Ctrl+Z undoes the tiles and the bounds together
            EditorSceneManager.MarkSceneDirty(tilemap.gameObject.scene);
            Debug.Log($"Level: built {wanted.Count} tiles, {wantedCoins.Count} coins and {wantedEnemies.Count} enemies from {path}" +
                      (bounds != null ? " and fitted LevelBounds. Save the scene to keep them." : ". No LevelBounds in the scene to fit."));
        }

        // Tiles -> text. Writes the layout of the tiles now in the Tilemap.
        [MenuItem("Tools/Level/Save Layout From Level")]
        private static void Save()
        {
            if (!TryFindTilemap(out var tilemap) || !TryLoadTiles(out var tiles))
            {
                return;
            }

            ReadPieces(tilemap, tiles, out var pieces, out var unknown);
            if (unknown.Count > 0)
            {
                Fail($"{unknown.Count} tile(s) are not one of the nine ground tiles, for example at {Cells(unknown)}: the layout can't hold them.");
                return;
            }

            if (pieces.Count == 0)
            {
                Fail("The Tilemap is empty: there is nothing to save.");
                return;
            }

            // A wrong tile inside a block (say an outlined bottom tile mid-column) is saved as the tile
            // the block needs there, so the next build will replace it. Say so before saving.
            LevelLayout layout;
            try
            {
                layout = LevelLayout.FromPieces(pieces, CoinCellsInScene(tilemap), EnemyCellsInScene(tilemap));
            }
            catch (ArgumentException e)
            {
                Fail(e.Message);
                return;
            }

            var problems = layout.Problems();
            var misfits = problems.Count > 0
                ? new List<Vector2Int>()
                : layout.Pieces().Where(pair => !pieces[pair.Key].Equals(pair.Value)).Select(pair => pair.Key).ToList();
            if (problems.Count > 0)
            {
                Fail("The tiles make blocks no layout can hold:\n" + string.Join("\n", problems.Take(8)));
                return;
            }

            if (misfits.Count > 0 && !EditorUtility.DisplayDialog(Title,
                    $"{misfits.Count} tile(s) don't match their block (an edge tile in its middle?), for example at {Cells(misfits)}. " +
                    "The layout will have the tile the block needs there, and the next build will paint it.",
                    "Save anyway", "Cancel"))
            {
                return;
            }

            var path = LayoutPath(tilemap.gameObject.scene.name);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, layout.ToText());
            AssetDatabase.ImportAsset(path);
            Debug.Log($"Level: saved {pieces.Count} tiles, {layout.CoinCells().Count} coins and {layout.EnemyCells().Count} enemies to {path}");
        }

        // A coin sits in the middle of its cell; an enemy stands on the cell's bottom edge, the top of
        // the ground under it (its pivot is at its feet).
        private static Vector3 CoinPosition(Tilemap tilemap, Vector2Int cell) =>
            tilemap.GetCellCenterWorld(new Vector3Int(cell.x, cell.y, 0));

        private static Vector3 EnemyPosition(Tilemap tilemap, Vector2Int cell) =>
            CoinPosition(tilemap, cell) + Vector3.down * (tilemap.layoutGrid.cellSize.y / 2f);

        // The cell of every coin or enemy in the open scene, hidden ones too (one taken or defeated in
        // Play Mode is hidden, but Play Mode changes are undone when it ends). An enemy's feet are on
        // its cell's bottom edge: half a unit up is safely inside the cell.
        private static List<Vector2Int> CoinCellsInScene(Tilemap tilemap) =>
            Object.FindObjectsOfType<Coin>(true)
                .Select(coin => (Vector2Int)tilemap.WorldToCell(coin.transform.position))
                .ToList();

        private static List<Vector2Int> EnemyCellsInScene(Tilemap tilemap) =>
            Object.FindObjectsOfType<Enemy>(true)
                .Select(enemy => (Vector2Int)tilemap.WorldToCell(enemy.transform.position + Vector3.up * 0.5f))
                .ToList();

        // The prefab to place, if the layout needs it.
        private static bool TryLoadPrefab(string prefabPath, List<Vector2Int> cells, string layoutPath, out GameObject prefab)
        {
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (cells.Count > 0 && prefab == null)
            {
                Fail($"{layoutPath} places {cells.Count} of {Path.GetFileNameWithoutExtension(prefabPath)}, but there is no prefab at {prefabPath}.");
                return false;
            }

            return true;
        }

        // Replaces every T in the scene (Coin, Enemy) with one prefab instance per cell, under a parent.
        private static void Place<T>(GameObject prefab, string parentName, List<Vector2Int> cells,
            Func<Vector2Int, Vector3> positionOf) where T : Component
        {
            foreach (var old in Object.FindObjectsOfType<T>(true))
            {
                Undo.DestroyObjectImmediate(old.gameObject);
            }

            if (cells.Count == 0)
            {
                return;
            }

            var parent = GameObject.Find(parentName);
            if (parent == null)
            {
                parent = new GameObject(parentName);
                Undo.RegisterCreatedObjectUndo(parent, "Build Level From Layout");
            }

            foreach (var cell in cells)
            {
                // InstantiatePrefab, not Instantiate: the copy stays linked to the prefab, so a change
                // to the prefab (its sprite, say) reaches every copy.
                var made = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.transform);
                made.transform.position = positionOf(cell);
                made.name = $"{prefab.name} ({cell.x}, {cell.y})";
                Undo.RegisterCreatedObjectUndo(made, "Build Level From Layout");
            }
        }

        private static bool TryFindTilemap(out Tilemap tilemap)
        {
            var tilemaps = Object.FindObjectsOfType<Tilemap>();
            tilemap = tilemaps.Length == 1 ? tilemaps[0] : null;
            if (tilemap == null)
            {
                Fail($"The open scene should have exactly one Tilemap; it has {tilemaps.Length}.");
            }

            return tilemap != null;
        }

        private static bool TryLoadTiles(out Dictionary<GroundPiece, TileBase> tiles)
        {
            // A local first: a lambda may not use an out parameter (error CS1628).
            var loaded = TilePaths.ToDictionary(pair => pair.Key, pair => AssetDatabase.LoadAssetAtPath<TileBase>(pair.Value));
            tiles = loaded;
            var missing = TilePaths.Where(pair => loaded[pair.Key] == null).Select(pair => pair.Value).ToList();
            if (missing.Count > 0)
            {
                Fail("These ground tiles are missing:\n" + string.Join("\n", missing));
            }

            return missing.Count == 0;
        }

        // The piece of every painted cell, and the cells whose tile isn't a ground tile.
        private static void ReadPieces(Tilemap tilemap, Dictionary<GroundPiece, TileBase> tiles,
            out Dictionary<Vector2Int, GroundPiece> pieces, out List<Vector2Int> unknown)
        {
            var pieceOf = tiles.ToDictionary(pair => pair.Value, pair => pair.Key);
            pieces = new Dictionary<Vector2Int, GroundPiece>();
            unknown = new List<Vector2Int>();
            tilemap.CompressBounds();
            foreach (var cell in tilemap.cellBounds.allPositionsWithin)
            {
                var tile = tilemap.GetTile(cell);
                if (tile == null)
                {
                    continue;
                }

                if (pieceOf.TryGetValue(tile, out var piece))
                {
                    pieces[(Vector2Int)cell] = piece;
                }
                else
                {
                    unknown.Add((Vector2Int)cell);
                }
            }
        }

        private static string Cells(IEnumerable<Vector2Int> cells) =>
            string.Join(", ", cells.Take(5).Select(c => $"({c.x}, {c.y})"));

        private static void Fail(string message)
        {
            Debug.LogError("Level: " + message);
            EditorUtility.DisplayDialog(Title, message, "OK");
        }
    }
}
